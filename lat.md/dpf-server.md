# DPF Server

해석이 끝난 `.rst` 를 DPF 로 분석하는 **서버 배포 단위**. SpaceClaim Add-In·Mechanical ACT 와 달리 GUI 가 없고,
라이선스가 잡힌 서버 한 대에서 여러 사용자/LLM 이 REST·MCP 로 호출한다. 코드는 `tools/dpf_server/`, 사용법은 그 폴더 README.

관련: [[postprocess]] (데스크톱 쪽 "Run DPF deep analysis") · [[status]]

## 왜 이 기능부터 서버화했나

2026-09-16 서버화 가능성 조사 결과 (코드 기준 3단계):

| 단계 | 기능 | 서버화 |
|---|---|---|
| ANSYS 불필요 | `calibration/runner.py`, `postprocess/analyzer.py`·`sweep_analyzer.py`, C# 파서(SpecParser/OdbPlusPlusParser/PackageFileParser — SC API 미사용이나 Add-In DLL 에 묶임) | 가능 (파서는 라이브러리 분리 필요) |
| ANSYS 설치 + GUI 불필요 | **`batch/mx_batch.py` (DPF-over-.rst)**, SpaceClaim `/RunScript` 잡 (g4~g16 게이트가 이 방식) | DPF: 헤드리스 실증 완료 → **1순위**. SC: Windows 전용·콜드스타트 90~300s·`/Headless` STEP import 행·병렬 라이선스 미검증 |
| GUI 필수 | Mechanical ACT 전부(Vibration Energy 등), PyMechanical DOE (Student v252 secure-gRPC 벽), PyQt 뷰어 | 어려움 |

DPF 사이드카는 이미 `ansys-dpf-core` 로 `.rst` 만 읽고(PyMechanical 불필요), 입력 1개 → JSON 1개의 순수 배치 형태라
서버 계층만 씌우면 됐다.

## 구조

- `mxdpf/app.py` — FastAPI. REST(`/jobs` 업로드·`/jobs/by-path`·상태·결과·로그·취소·삭제) + `/health[?deep]` + `/mcp`.
- `mxdpf/jobs.py` — `JobManager`. 잡 = 폴더(`job.json`/`input.rst`/`result.json`/`run.log`). 워커 스레드 N 개가
  `python mx_batch.py <rst> result.json [--rst2]` 를 **서브프로세스**로 실행 (DPF·라이선스 오류 격리, kill 로 확실한 취소·타임아웃,
  데스크톱과 동일 CLI → 결과 스키마 동일). asyncio 서브프로세스를 안 쓰는 이유: Windows 에서 uvicorn 이 Selector 루프를 잡으면
  `NotImplementedError`.
- `mxdpf/mcp.py` — SDK 없는 최소 MCP (Streamable HTTP, JSON 응답). 도구 5개 `dpf_health`/`dpf_submit_analysis`/`dpf_get_job`/
  `dpf_list_jobs`/`dpf_cancel_job`. 도구 오류는 JSON-RPC 오류가 아니라 `isError:true` 결과. 파일 업로드는 MCP 로 안 받음(공유 경로만).
  Add-In 의 `McpServer.cs` 와 같은 "최소 표면" 원칙.
- `mxdpf/probe.py` — 딥 헬스. 실제 잡과 같은 `mx_batch.start_server()` → 번들 예제 `static.rst` 읽기까지. 라이선스 실패는 보통
  `read_result` 단계에서 드러난다. 60초 캐시 + 직렬화(라이선스 중복 체크아웃 방지).

## 결정 / 함정

- **uvicorn 워커 1개 강제** — 잡 상태가 프로세스 메모리에 있다. 병렬은 `MXDPF_MAX_CONCURRENCY` (기본 1, DPF 서버를 잡마다 따로
  띄우므로 라이선스 동시성 확인 후 올릴 것).
- **재시작 시 실행 중 잡은 `interrupted`** — 자동 재실행하면 라이선스를 몰래 다시 쓴다.
- **프로세스 트리 kill** — DPF 는 `Ans.Dpf.Grpc` 자식을 띄운다. Windows `taskkill /F /T`, POSIX `start_new_session` + `killpg`.
- **경로 제출 보안** — `realpath` 후 `commonpath` 로 루트 포함 판정 (`..`·심볼릭 링크·`share-evil` 형제 폴더 차단, Windows 드라이브
  불일치 `ValueError` 처리). 루트 미설정이면 경로 제출 자체를 끈다.
- **업로드 크기** — 멀티파트는 핸들러 전에 임시파일로 다 받아지므로 미들웨어가 `Content-Length` 로 먼저 끊고(2파일×한도), 저장 중
  파일별 한도로 한 번 더 끊는다.
- **Origin 차단** — MCP 스펙의 DNS-rebinding 권고. 게이트웨이 서버-서버 호출엔 Origin 이 없어서 영향 없음.
- **mx_batch hotspot 단위** — `eps_mm=2.0` 이 좌표 단위 변환 없이 쓰여 m 단위 결과에서 과대 클러스터링되던 것을 2026-09-16
  수정 (`eps_in_coord_units`, `coordinates_field.unit` 기반; 모르는 단위는 이전 동작 + `eps_unit_assumed`). 데스크톱과 공유 스크립트.

## 검증

- `tests/test_server.py` 30개 + `tests/test_mx_batch_hotspots.py` 11개 (라이선스 불필요, 가짜 `mx_batch.py`·가짜 DPF 객체) — 전부 통과 (Python 3.11).
- 실제 uvicorn 기동 + 공식 MCP Python SDK `streamablehttp_client` 로 initialize/tools/list/tools/call 확인.
- `selftest_live.py` — 라이선스 서버에서 실제 DPF 로 끝까지 (`LIVE_GATE_OK`). 가짜 백엔드로 스크립트 자체 동작은 확인했으나
  **실제 DPF·라이선스 실행은 아직** (라이선스는 운영 서버에서만 잡힘).
