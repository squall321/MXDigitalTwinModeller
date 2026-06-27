# MXDigitalTwinModeller — Experience Log

CAD reverse-engineering + AI-driven design 시스템 구축의 전 여정 기록.
SpaceClaim Add-In(C#, ANSYS Student v252) 위에서 **CAD 수정 자동화 → MCP 노출 → 무에서 폰-메탈 생성**까지.

목적: 무엇이 통했고, 무엇이 헛다리였고, 어떤 방법론이 반복적으로 효과적이었는지를 한 곳에 — 다음 사이클·다음 사람이 같은 함정을 피하도록.

---

## 0. 한눈에 보는 도달점

| 능력 | 상태 | 증명 |
|---|---|---|
| **CAD 수정 (18 primitive)** | trustworthy **82.0%** (138→141 V / 173 verifiable, 12모델 matrix) | kernel-truth oracle, 회귀 0 |
| **MCP 서버 (mod 도구 노출)** | 작동 | 외부 HTTP → 마샬링 → SC 지오메트리 변경 (change_hole_diameter H1→12mm) |
| **무에서 폰 생성** | 작동 | 빈 SC → `generate_phone` → uniform-wall hollow shell → `set_camera_height` regen |

핵심 명제 증명: **"자동 설계"는 (1) 기존 CAD 수정 + (2) 무에서 생성 둘 다, 네이티브 SpaceClaim + MCP 로 자연어 구동된다.** 외부 커널(build123d) 없이.

---

## 1. 방법론 — 반복적으로 효과적이었던 패턴

이 프로젝트가 헛다리를 피한 핵심은 **검증된 방법론의 일관 적용**이었다.

### 1.1 gate → kernel-truth oracle → implement → no-regression
모든 본질 작업의 표준 절차:
1. **GATE 실험 먼저** — 가장 큰 미지수를 최소 비용으로 de-risk. 코드 한 줄 쓰기 전에 "이게 되는가?"를 실측.
2. **kernel-truth oracle** — 추출기(FeatureExtractor)가 아니라 **live B-rep 을 직접** 읽어 검증 (Volume, ContainsPoint, Cylinder.Radius/Axis).
3. **implement** — 게이트 통과 후에만.
4. **no-regression** — 변경이 기존 통과 케이스를 깨지 않는지 표적/전체 재실행.

### 1.2 적대적 검증 워크플로우 (multi-agent)
본질 결정마다 **독립 설계안 N개 → 적대적 심사 → 합성** 워크플로우를 돌렸다. 이게 **빌드 전에 헛다리를 막은** 사례가 반복됐다:
- **W5-2**: PLAN 의 "body 경계 횡단" 가설을 코드+geometry 로 반박 → 진짜 원인은 thin-wall tangency. 가설대로 multi-body 기계를 만들었으면 as1-oc 를 전혀 못 고쳤을 것.
- **P2 곡면 셸**: Thicken 이 틀린 verb(XML: sheet 만 walling)임을 빌드 전에 규명 → Boolean-inset cavity 로 직행.
- **from-scratch 로드맵**: build123d 외부 생성기가 82% mod 도구 + MCP 를 버리고 STEP 왕복(#1 취약점)을 강제함을 규명 → 네이티브 SC 로 방향 전환.

교훈: **워크플로우/적대검증의 "회복 가능" 추정조차 실측 앞에서 틀릴 수 있다** (클러스터 A 의 +5→+4→실제 +2). 분석은 방향을 좁히고, **probe/실측이 진실을 준다.**

### 1.3 probe-by-측정 (모호할 때 코드 고치기 전에 측정)
오라클이 IC/F 를 줄 때 "진짜 버그 vs false-positive" 가 모호하면 — 추측·LLM분석 대신 **SC 내부 probe 로 직접 측정**. 단, 측정 도구 자체의 함정도 실측으로 드러났다 (아래 ContainsPoint).

---

## 2. 결정적 기술 발견 (재사용 가치 높은 함정·진실)

### 2.1 sign-only 오라클의 착시 — 82% 는 magnitude 검증이 아니었다
가장 충격적 발견. 12모델 matrix 의 82% 는 **dV 부호 + face-count 만** 검사했고, **magnitude/breach/closed-solid 는 한 번도 검증 안 했다.** P0(from-empty composition, 첫 magnitude 오라클)가 IL 디스어셈블리로 잠복 버그 2개를 노출:
- **AddPocket/AddSlit cutter 법선 반전** — `Frame.Create(center, shortAxis, longAxis)` → DirZ=−nAxis → cutter 가 묻혀서 face 관통 안 함(dV=0). 묻힌 cavity 도 dV<0 라 sign 오라클은 통과.
- **AddBoss embed 0.5mm 너무 얕음** — partial Unite(+75.7 vs +169.6). dV>0 면 통과라 안 걸림.

**교훈: "성공률"은 오라클이 무엇을 보느냐에 달렸다. sign-only 는 묻힌/부분 연산을 통과시킨다. 생성에는 magnitude + closed-solid 검증 필수.** 수정 후 imported-CAD 24셀 무회귀 + 품질(정확도) 향상.

### 2.2 ContainsPoint 는 cylinder 경계점에서 비결정적
single-point ContainsPoint 를 oracle ground-truth 로 쓰면 false-IC 양산. 실측: 클린 슬랩의 단일 홀이 dV=−92.99(정확)인데 `ContainsPoint(축중심)=True`(solid 오판). bore void 중심은 표면이 아닌데도 틀린다. **해법: axis 따라 ray-march(연속 두 점의 transition 은 신뢰 가능) 또는 volume magnitude.** single-point 금지.

### 2.3 FeatureExtractor 는 신뢰 불가 → kernel-truth 가 crown jewel
- 11752: PIN 을 hole 로 오분류(orientation flip). ContainsPoint 축중심 = ground truth.
- 곡면 flange: hole anchor 가 48mm off.
- 그래서 모든 oracle 을 FE-only 에서 **live-body kernel-truth** 로 전환 (find_live_cylinder_near, count_live_cylinders, contains_mm).

### 2.4 단위 규약 — ModificationService 는 mm 를 받는다
`Add*/Change*` 의 positionMm 은 **밀리미터**(내부에서 MmToMeters). P0 probe 의 `arr()/1000` 은 단위버그였고 FindPlanarFaceAtZ 의 2mm tol 로 우연히 통과. **GenerationService 가 올바른 레퍼런스(true mm 전달).**

### 2.5 IronPython ↔ C# 바인딩 함정
- `CircleProfile/RectangleProfile` 은 IronPython 에서 **5-arg**(plane, w, h, PointUV.Create(0,0), 0.0) 필수. C# 3-arg 오버로드는 IronPython 에 안 통함.
- `Body` → `...Modeler` 네임스페이스, `DesignBody` → `...V252` top (분리).
- `Unite/Subtract` 는 `System.Array[Body]([x])` 필요.

### 2.6 SpaceClaim 스레드 어피니티 + WriteBlock
모든 지오메트리 mutation 은 **SC API 스레드의 `WriteBlock.ExecuteTask` 안에서.** `Component.Content` 접근(어셈블리 body bind)도 WriteBlock 필수. 백그라운드 스레드(MCP 콜백)는 `Application.ExecuteOnMainThread(Task)`(xml:5501)로 마샬링 — ⚠️ **이걸 메인스레드에서 호출하면 데드락**(펌프 정지). HttpListener 워커는 별도라 무관.

### 2.7 Boolean poison + 곡면 셸
- 실패한 Boolean/OffsetFaces 가 body 를 poison("object is deleted") → fresh re-import 만 복구.
- **Thicken 은 hollow 가 아니다** (XML: sheet 를 solid 로 walling). 닫힌 solid hollow 는 **Boolean-inset cavity Subtract** 가 정답 (Unsupported 0개, P0/P1 검증 idiom).
- thin-wall(<1mm)에서 OffsetFaces 는 kernel reject + cascade-delete.

### 2.8 공유 함수 가드는 opt-in 필수
W5-2 벽두께 가드를 AddHole 에 무차별 적용 → MirrorFeature(AddHole 호출)의 twin 위치를 클램프해 회귀. → `clampToWall` opt-in 으로 격리. **cross-cutting 동작은 공유 함수의 모든 호출자에 영향.**

### 2.9 feature ID drift → axis-aware handle
positional ID(`H`+i)는 face-order 라 재생성마다 renumber. **stable handle = 물리 anchor(position+axis+size).** resolve 는 **axis 에 수직인 거리**로 (hole 은 점이 아니라 축선; anchor Z 와 extracted Z 가 달라도 매칭).

---

## 3. 운영 교훈 (헤드리스 matrix / 빌드)

- **matrix stall**: 셀당 SC 재기동 시간이 누적증가(74→169s)하다 hang. `-Resume` 스위치(완료 셀 skip + JSON 보존)로 fresh-proc 재시작 → 누수 리셋. per-cell JSON 저장이라 결과 안 날아감.
- **warm-up pre-pass**: 첫 셀 cold-launch 타임아웃(±3 MISSING 비결정성) → throwaway SC 1회 launch 로 OS 캐시 데움.
- **디스크 100%-full = matrix false-FAILED**: 결과 JSON 못 써서 `status='-'`. 작업 전 `df` 확인. 안전 정리: `Installer/*.msi`(빌드마다 재생성, 196M×2), `*.log`(결과는 matrix_results/ 별도).
- **빌드**: SpaceClaim 이 DLL 락 → 빌드 전 SC 종료. csproj 가 **명시적 `<Compile Include>`**(와일드카드 아님) → 새 .cs 는 csproj 에 직접 등록.
- **AddChamfer IC↔F flip**: 곡면 NURBS no-op 셀은 dV~1e-21(완전0) 부호 노이즈로 IC/F 비결정. 회귀 오판 말 것.

---

## 4. 아키텍처 결정 기록

### 4.1 MCP 서버 = Approach C (Add-In 내장 HttpListener)
3개 통합 아키텍처 평가 후. Add-In 이 127.0.0.1 HttpListener + JSON-RPC2.0 MCP 직접 호스팅. 대안(별도 Node/Python shim)보다 단일 코드베이스 + live 도구 스키마(drift 0). B(프로세스 재기동)는 poison 최후수단만.
- `LlmToolDispatcher.Dispatch(body, graph, toolName, inputJson)` = 단일 진입점(20→22 도구).
- `LlmToolRegistry.ToToolsArrayJson()` → MCP `inputSchema`(input_schema 만 치환) = single source of truth.

### 4.2 from-scratch = 네이티브 SC (Strategy 2, NOT build123d)
build123d 외부 생성기 폐기 이유: (1) `TestModelGenerator.cs` 가 이미 네이티브 from-scratch 생성, (2) build123d 는 82% 검증 mod 도구 + MCP 를 버리고 STEP 왕복(feature-ID drift·48mm anchor·pin/hole 오분류 = #1 취약점) 강제. → **단일 커널, mod 도구+MCP 를 SAME live body 에 재사용, STEP 은 FEA-freeze 때 1회만.**
- `PhoneParameters`(C#) = single source of truth + Validate(설계의도 규칙).
- `GenerationService` = CreateBaseSolid(bbox-from-empty) + deterministic stage replay S00→S00b(hollow)→S04→S05→S06.
- MCP 편집은 set_parameter→regenerate (geometry→params inverse 안 씀 — extractor fragile).

---

## 5. 빌드된 컴포넌트 지도

```
Models/ReverseEngineer/
  PhoneParameters.cs        — 파라메트릭 single source of truth + Validate
Services/ReverseEngineer/
  ModificationService.cs    — 18 mod primitive (Change*/Add*/Move*/Remove*/Rotate*/Mirror*)
  LlmToolRegistry.cs        — 22 도구 스키마, LlmToolDispatcher.cs — 단일 dispatch
  Generation/
    GenerationService.cs    — from-empty 생성 + stage pipeline
    CurvedShellBuilder.cs   — P2 hollow-to-tray (Boolean-inset cavity)
    FeatureHandle.cs        — P3 stable handle + axis-aware resolver
  Mcp/
    McpServer.cs            — HttpListener + JSON-RPC2.0 MCP
    ApiThreadMarshaller.cs  — bg→API 스레드 마샬링 (ExecuteOnMainThread)
    SessionContext.cs       — 영속 세션 (Body+Graph+Params+Handles), GeneratePhone
    McpToolAdapter.cs / JsonLite.cs
Commands/ReverseEngineer/
  AskClaudeCommand.cs / GateMarshalTestCommand.cs
```

권위 PLAN 문서: `MOD_BREAKTHROUGH_PLAN.md`(mod 사이클), `MCP_SERVER_PLAN.md`(MCP), `FROM_SCRATCH_ROADMAP.md`(P0-P7 생성 로드맵).

---

## 6. 남은 길 (feasibility 아닌 breadth)

- **P4** 곡면-face 타겟팅 — deferred (v1 tray all-planar; v2 곡면 envelope 에만 필요)
- **P5** 전체 stage S01/02/03/07-12 + 패턴 라이브러리(camera_island, speaker_grille_array, port_cutout...)
- **P6 Tier-2** 생성 후 kernel-truth 검증(min-wall, self-intersection)
- **v2** 진짜 곡면 envelope(RevolveTrimmedCurves spec-63 검증됨 / faceted Z-stack) + 단일 FEA-freeze hop
- **P7 SpecParser** — LLM 이 자연어 스펙 → generate_phone 인자(front-door); Validate 가 Tier-1 guardrail

핵심 척추(spec→params→generate→validate→regen→MCP)는 검증 완료. 남은 건 같은 idiom 의 반복 확장.

---

## 7. 가장 큰 메타-교훈

1. **"성공률"을 의심하라.** 82% 는 오라클이 본 것만큼만 진실이었다. magnitude/closed-solid 로 다시 재면 도구에 버그가 있었다. 더 엄격한 오라클이 더 정직한 숫자를 준다 (trustworthy > inflated).
2. **적대적 검증이 빌드를 아낀다.** Thicken·build123d·body-경계 가설 — 전부 코드/XML 실측으로 빌드 전에 폐기. 하지만 적대검증의 추정조차 probe 로 최종 확인.
3. **kernel-truth 가 전부다.** 추출기·single-point·sign 오라클 모두 거짓을 줬다. live B-rep 직접 측정(magnitude, axis-aware, ray-march)만 신뢰.
4. **재사용이 moat 다.** 30+ 사이클의 mod 도구 + MCP 를 버리는 모든 전략(build123d)은 비쌌다. 네이티브 SC 가 그것들을 SAME body 에 그대로 쓴다.
