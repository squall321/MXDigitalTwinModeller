# MX DPF Server — `.rst` 후처리를 서버로

해석이 끝난 ANSYS 결과 파일(`.rst`)을 받아 **DPF**로 분석하고 JSON으로 돌려주는 서버입니다.
REST로도 쓸 수 있고, **MCP 도구**로도 노출돼서 포털 MCP 게이트웨이나 Claude 같은 LLM 클라이언트에 그대로 붙습니다.

분석 엔진은 데스크톱 ACT의 "Run DPF deep analysis"가 쓰는
`Mechanical/MXSimulator/batch/mx_batch.py`와 **같은 스크립트**입니다. 서버는 이 스크립트를 잡마다
서브프로세스로 실행하고 큐, 타임아웃, 취소, 인증, 결과 보관을 맡습니다. 그래서 결과 JSON 스키마가 데스크톱과 같습니다.

```
 클라이언트 (REST / MCP)                    MX DPF server (python -m mxdpf)
 ─────────────────────────                 ─────────────────────────────────────────────
 POST /jobs  (.rst 업로드)       ─┐          FastAPI ── JobManager ── 워커 N개(기본 1)
 POST /jobs/by-path (공유 경로)  ─┼──▶        │              │
 POST /mcp   dpf_submit_analysis ─┘          │              └─ subprocess: python mx_batch.py in.rst result.json
 GET  /jobs/{id}[?wait=]          ◀──────────┘                              └─ Ans.Dpf.Grpc (라이선스 체크아웃)
 GET  /jobs/{id}/result                      잡 폴더: <MXDPF_WORK_DIR>/<job_id>/{job.json,input.rst,result.json,run.log}
```

## 계산하는 것

| 블록 | 내용 | 조건 |
|---|---|---|
| `modal` | 고유진동수 목록 (`freqs_hz`, `n_modes`) | 모달 결과 |
| `participation` | 방향별 유효질량·누적 비율 (`method` = `lumped_mass` 또는 `unit_mass`) | 모달 결과 |
| `mac` | self-MAC (대각 최소, 비대각 최대) 또는 `rst2`를 주면 cross-MAC 행렬 | 모달 결과 |
| `strain_energy` | 정적: 요소 변형에너지 총합·상위 요소 / 모달: 모드별 상대 SED | 공통 |
| `hotspots` | von Mises 상위 1% 노드 공간 클러스터 (피크 노드·값·크기) | 공통 |
| `errors` | 단계별 실패 목록. 일부 단계가 실패해도 나머지 결과는 남음 | — |

잡 상태의 `stage_errors`는 `errors` 개수입니다. `succeeded`이면서 `stage_errors > 0`이면 일부 블록만 계산된 것입니다.

## 요구 사항

- **ANSYS v252 설치 + 라이선스** (DPF premium 컨텍스트): 라이선스가 잡히는 서버에서만 실제 분석이 됩니다.
- Python 3.9+ (검증: 3.11 서버 계층, 데스크톱 프로브는 3.13)
- Windows 또는 Linux. DPF 서버(`Ans.Dpf.Grpc`)가 설치된 OS면 됩니다.
- PyMechanical은 **필요 없습니다**. Student v252의 secure-gRPC 제약과도 무관하게 `.rst`만 읽습니다.

## 설치

```powershell
# Windows
cd tools\dpf_server
py -3 -m venv .venv
.venv\Scripts\pip install -r requirements.txt
```

```bash
# Linux
cd tools/dpf_server
python3 -m venv .venv
.venv/bin/pip install -r requirements.txt
```

`requirements.txt`는 웹 계층(FastAPI/uvicorn)과 `mx_batch` 요구사항(`ansys-dpf-core==0.16.1`, numpy)을 함께 설치합니다.
`ansys-dpf-core` 버전은 서버의 ANSYS 버전과 호환돼야 합니다. 0.16.1은 v252(DPF 서버 10.0)에서 검증됐습니다.

## 실행

```powershell
run_server.bat              # Windows (환경변수는 파일 안에서 지정)
```
```bash
./run_server.sh             # Linux
# 서비스로: deploy/mxdpf.service + deploy/mxdpf.env.example 참고
```

직접 실행: `python -m mxdpf --host 0.0.0.0 --port 8770`
API 문서는 `http://<서버>:8770/docs`에서 볼 수 있습니다.

> 잡 상태가 프로세스 메모리에 있으므로 uvicorn 워커는 **1개**여야 합니다 (`python -m mxdpf`가 강제).
> 병렬 처리는 `MXDPF_MAX_CONCURRENCY`로 조절합니다.

### 환경변수

| 변수 | 기본값 | 의미 |
|---|---|---|
| `MXDPF_HOST` | `127.0.0.1` | 바인드 주소. 게이트웨이 뒤라면 `0.0.0.0` |
| `MXDPF_PORT` | `8770` | 포트 |
| `MXDPF_TOKEN` | 없음 | 설정하면 기본 `/health` 외 모든 요청에 `Authorization: Bearer <토큰>` 필요 |
| `MXDPF_WORK_DIR` | `~/.mxdtm/dpf_jobs` | 잡 폴더 루트 |
| `MXDPF_PYTHON` | 서버 인터프리터 | `mx_batch.py`를 돌릴 python. DPF를 별도 venv에 둘 때 지정 |
| `MXDPF_BATCH_SCRIPT` | `<repo>/Mechanical/MXSimulator/batch/mx_batch.py` | 분석 스크립트 |
| `MXDPF_MAX_CONCURRENCY` | `1` | 동시 실행 잡 수 |
| `MXDPF_JOB_TIMEOUT_SEC` | `1800` | 잡 1건 최대 실행 시간 (초과 시 프로세스 트리 종료 → `timeout`) |
| `MXDPF_MAX_UPLOAD_MB` | `4096` | 업로드 파일 1개 최대 크기 |
| `MXDPF_JOB_TTL_HOURS` | `72` | 끝난 잡 폴더 자동 삭제 (0 = 보관) |
| `MXDPF_ALLOWED_ROOTS` | 없음 | 경로 제출을 허용할 루트들 (`os.pathsep` 구분: Windows `;`, Linux `:`). 비어 있으면 경로 제출 금지 |
| `MXDPF_ALLOWED_ORIGINS` | 없음 | 브라우저 `Origin` 허용 목록 (쉼표 구분). Origin이 있는데 목록에 없으면 403 |
| `AWP_ROOT252` | — | ANSYS v252 경로. 자식 프로세스로 전달 (Linux에서는 반드시 지정) |
| `ANSYSLMD_LICENSE_FILE` | — | 라이선스 서버 (`포트@호스트`). 자식 프로세스로 전달 |

## 설치 확인 (라이선스 서버에서)

```bash
# 1) DPF 기동 + 라이선스까지 한 번에 확인 — deep.ok 가 true 여야 함
curl -H "Authorization: Bearer $MXDPF_TOKEN" "http://127.0.0.1:8770/health?deep=true"

# 2) 실제 DPF 로 잡을 끝까지 — 마지막 줄 LIVE_GATE_OK
.venv/bin/python selftest_live.py --url http://127.0.0.1:8770 --token $MXDPF_TOKEN
#    번들 예제 static.rst 를 업로드한다. 모달 경로까지: --modal-example (예제 다운로드에 네트워크 필요)
#    실제 결과로:  --rst my_modal.rst   또는   --path /mnt/cae/job1/file.rst
```

`deep` 응답의 `stages`에서 어디서 막혔는지 보입니다.

| 단계 | 실패 의미 |
|---|---|
| `import` | venv에 `ansys-dpf-core`가 없음 |
| `server` | DPF 서버 기동 실패 (`AWP_ROOT252` 경로, 설치 확인) |
| `read_result` | 서버는 떴지만 결과 읽기 실패. **라이선스 문제가 보통 여기서** 드러남 |

## REST 사용 예

```bash
T="Authorization: Bearer $MXDPF_TOKEN"
# 업로드 제출 (rst2 는 cross-MAC 용, 선택)
curl -H "$T" -F rst=@modal_a.rst -F rst2=@modal_b.rst http://srv:8770/jobs
# 공유 스토리지 경로 제출 (MXDPF_ALLOWED_ROOTS 안)
curl -H "$T" -H "Content-Type: application/json" -d '{"rst_path":"/mnt/cae/p1/file.rst"}' http://srv:8770/jobs/by-path
# 끝날 때까지 최대 120초 대기하며 상태 조회
curl -H "$T" "http://srv:8770/jobs/<job_id>?wait=120"
curl -H "$T" http://srv:8770/jobs/<job_id>/result      # 결과 JSON
curl -H "$T" http://srv:8770/jobs/<job_id>/log         # 실행 로그 (실패 원인)
curl -H "$T" -X POST http://srv:8770/jobs/<job_id>/cancel
curl -H "$T" -X DELETE http://srv:8770/jobs/<job_id>   # 끝난 잡 폴더 삭제
```

잡 상태: `queued` → `running` → `succeeded` / `failed` / `timeout` / `cancelled` / `interrupted`
(`interrupted`는 서버가 실행 중에 재시작된 경우입니다. 라이선스를 몰래 쓰지 않도록 자동 재실행은 하지 않습니다.)

## MCP 사용

엔드포인트는 `POST http://<서버>:8770/mcp`입니다 (Streamable HTTP, JSON 응답, SSE 없음).
공식 MCP Python SDK 클라이언트(`streamablehttp_client`)로 initialize, tools/list, tools/call을 검증했습니다.

| 도구 | 용도 |
|---|---|
| `dpf_submit_analysis` | `rst_path`(서버 기준 공유 경로), `rst2_path?`, `wait_seconds?`로 제출. 끝났으면 결과까지 반환 |
| `dpf_get_job` | 상태, 결과 조회 (`wait_seconds`로 대기 가능) |
| `dpf_list_jobs` | 최근 잡 목록 |
| `dpf_cancel_job` | 취소 |
| `dpf_health` | 상태 (`deep=true`면 DPF와 라이선스까지 확인) |

- **파일 업로드는 MCP로 받지 않습니다.** LLM 흐름에서는 공유 스토리지 경로를 넘기거나, REST로 올린 뒤 `dpf_get_job`으로 조회하세요.
- 도구 실행 오류(경로 거부, 잡 없음 등)는 `isError: true` 결과로 돌아가서 LLM이 읽고 고칠 수 있습니다.
- 포털 게이트웨이: 이 서버를 네임스페이스 하나로 등록하고 `Authorization: Bearer` 헤더를 게이트웨이가 붙이면 됩니다.
- Claude Code: `claude mcp add --transport http mxdpf http://srv:8770/mcp --header "Authorization: Bearer <토큰>"`

## 운영 메모

- **동시성:** 잡마다 DPF 서버 프로세스가 따로 뜹니다. 여러 개를 동시에 돌릴 때의 라이선스 동작은 아직 검증되지 않았으니,
  `MXDPF_MAX_CONCURRENCY=1`로 시작해서 라이선스 좌석을 확인한 뒤 올리세요.
- **업로드 디스크:** 멀티파트 업로드는 OS 임시 폴더에 먼저 받은 뒤 잡 폴더로 복사됩니다. 수 GB `.rst`를 올린다면
  임시 폴더(`TMP`/`TMPDIR`)가 있는 디스크에도 여유가 필요합니다. 큰 파일은 공유 경로 제출이 낫습니다.
- **보안:** 기본 바인드는 `127.0.0.1`입니다. `0.0.0.0`으로 열 때 `MXDPF_TOKEN`이 없으면 시작 로그에 경고가 뜹니다.
  업로드 파일은 `.rst` 확장자만 받고 잡 폴더에 `input.rst`로 저장합니다. 경로 제출은 심볼릭 링크와 `..`를 풀어서
  허용 루트 안인지 확인합니다.
- **취소, 타임아웃:** Windows는 `taskkill /T`, Linux는 프로세스 그룹 kill로 DPF 자식 프로세스까지 정리합니다.

## 알려진 한계 (mx_batch 쪽)

- `hotspots` 클러스터 반경은 `eps_mm=2.0`(mm) 을 결과 좌표 단위(`coordinates_field.unit`)로 환산해 쓴다. 결과의 `coord_unit`,
  `eps_coord_units` 로 확인할 수 있고, 단위를 인식 못 하면 mm 로 가정하고 `eps_unit_assumed: true` 를 남긴다.
- `participation` 은 `elemental_mass` 연산자가 없으면 단위질량(`unit_mass`)으로 떨어진다 — 값의 스케일이 달라지므로 `method` 를 함께 볼 것.

## 개발 / 테스트 (라이선스 불필요)

```bash
pip install -r requirements-dev.txt     # 또는 fastapi uvicorn python-multipart pytest httpx
python -m pytest -q tests               # 41 tests (저장소 안에서 실행 — mx_batch.py 를 직접 테스트)
```

`tests/fake/`의 가짜 `mx_batch.py`와 가짜 `ansys.dpf.core`로 다음을 검증합니다.

- 업로드, 경로 제출
- 경로 차단 (루트 밖, `..`, 심볼릭 링크, 형제 폴더)
- 크기, 확장자 제한
- 실패 유형 (fatal, crash, 깨진 JSON, 단계 오류)
- 타임아웃, 취소, 대기열
- 토큰, Origin
- 딥 헬스 (정상, 라이선스 실패, DPF 미설치)
- MCP 프로토콜 (협상, 알림, 배치, 오류 코드)
- 재시작 복구, TTL
- 실제 `mx_batch.py` 의 hotspot 단위 환산 (m, mm, cm, in 좌표 × scipy/numpy 경로)
