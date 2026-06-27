# Architecture

MX Digital Twin Modeller 는 ANSYS 의 두 클라이언트 (SpaceClaim, Mechanical) 양쪽에 동시에 설치되는 듀얼 사이드 패키지다. 한쪽은 형상/메쉬 전처리를, 한쪽은 해석 설정/후처리를 담당하며, 두 사이드는 공용 DLL 로 핵심 알고리즘을 공유한다.

## 듀얼 사이드 구조

- **SpaceClaim Add-In** — `[[AddIn.cs#MXAddIn]]` 가 진입점. `MX Modeller` 리본 탭 등록. 자세한 기능 목록은 [[spaceclaim-addin]] 참조.
- **Mechanical ACT Extension (MXSimulator)** — `[[Mechanical/MXSimulator/main.py]]` 가 진입점. `MXSimulator` 단일 탭에 모든 기능 배치. 자세한 기능 목록은 [[mechanical-act]] 참조.

두 사이드는 동시에 사용되지 않는다 — SpaceClaim 에서 형상/메쉬를 만든 뒤 `.k` 파일이나 Workbench Project 로 넘기면, Mechanical 에서 그 결과를 받아 해석을 진행하는 단방향 워크플로우를 따른다.

## 공용 Core DLL

`Shared/MXDigitalTwinModeller.Core/MXDigitalTwinModeller.Core.csproj` 가 두 사이드에서 모두 참조되는 라이브러리다.

### 포함 클래스

- `[[Shared/MXDigitalTwinModeller.Core/Spatial/SpatialIndex.cs#SpatialIndex]]` — bounding box 기반 공간 인덱스. Conformal Mesh 의 계면 검출에 사용됨.
- `[[Shared/MXDigitalTwinModeller.Core/Spatial/BodyBounds.cs#BodyBounds]]` — 바디별 AABB.
- `[[Shared/MXDigitalTwinModeller.Core/Geometry/GeometryUtils.cs#GeometryUtils]]` — 거리/법선 유틸.

### 배포 방식

- SpaceClaim 측 빌드 시 `Libs/` 에 출력 → AddIn 과 함께 로드
- Mechanical 측은 `Mechanical/MXSimulator/bin/MXDigitalTwinModeller.Core.dll` 로 복사. `[[Mechanical/MXSimulator/main.py]]` 가 `clr.AddReferenceToFileAndPath()` 로 동적 로드.

## 멀티버전 지원

SpaceClaim **V251** 과 **V252** 두 버전을 동시에 지원한다. Mechanical 은 V252 만 지원.

### 빌드 구성

| Configuration | 대상 | 출력 |
|---|---|---|
| Debug-V251 / Release-V251 | SpaceClaim V251 | `ADDIN_OUTPUT_PATH_V251` |
| Debug-V252 / Release-V252 | SpaceClaim V252 | `ADDIN_OUTPUT_PATH_V252` |

기본 구성은 **Debug-V252**. 자세한 빌드 절차는 [[build-deploy]].

### 컴파일 심볼

`[[AddIn.cs]]` 의 `#if V251 / #elif V252` 분기로 namespace 와 API 선택. `MXDigitalTwinModeller.csproj` 에서 Configuration 별로 `DefineConstants` 설정.

## 디렉토리 레이아웃

```
MXDigitalTwinModeller/
├── AddIn.cs                       # SpaceClaim 진입점 (MXAddIn)
├── Commands/                      # 리본 버튼 → 다이얼로그 트리거
│   ├── TensileTest/  DMA/  CAI/  Fatigue/  Joint/  Compression/
│   ├── BendingFixture/  Laminate/  VoidCut/
│   ├── Mesh/  Contact/  Simplify/  Material/  Load/  Simulation/  Export/
│   ├── Pipeline/  ConformalMesh/  GmshMesher/
├── Services/                      # 비즈니스 로직 (Command 와 1:1)
├── Models/                        # 데이터 모델 (Parameters, Result)
├── UI/Dialogs/                    # WinForms 다이얼로그
├── Core/                          # SpaceClaim 측 공통 모듈
│   ├── Commands/  Geometry/  IO/  UI/
├── Shared/MXDigitalTwinModeller.Core/  # 공용 .NET 라이브러리
├── Mechanical/MXSimulator/        # Mechanical ACT Extension
│   ├── main.py                    # IronPython + WPF 진입점
│   ├── extension.xml              # ACT 정의 (사용 안 함, MXSimulator.xml 사용)
│   ├── calibration/               # Material Calibrator (PyInstaller)
│   └── postprocess/               # 결과 시각화
├── Mechanical/MXSimulator.xml     # 실제 사용되는 ACT 정의
├── Scripts/                       # IronPython 16개 + pipeline.py
├── Installer/                     # WiX MSI
└── Docs/LSDyna/                   # LS-DYNA 키워드 참조
```

## 데이터 흐름 (전형적인 워크플로우)

1. **SpaceClaim**: 시편 모델링 ([[specimens]]) → Simplify → Material → Contact 검출 ([[mesh]]) → Mesh 설정 → `.k` 내보내기 또는 STEP 익스포트
2. **Mechanical** (선택): STEP 임포트 → 면 분석 ([[face-analysis]]) → Face Pair NS ([[face-pair-ns]]) → Tied Check ([[tied-check]]) → Modal/Scenario ([[scenarios]]) → Post-process ([[postprocess]])
3. **외부 캘리브레이션**: Material Calibrator ([[material-calibrator]]) 가 실험 데이터를 받아 재료 파라미터를 역산

## CAD-Modification (Mod) 자동화 — 커널 지뢰 & 운영

> Mod 돌파 세션(2026-06-10~17)의 아키텍처/운영 학습. API 차원 함정은 [[api-learnings#CAD-Modification (Mod) 자동화 — API 함정 & 검증]], 전체 계획은 [[MOD_BREAKTHROUGH_PLAN.md]] §8.

### Boolean / OffsetFaces 지뢰 — 실패는 body 를 poison 한다

실패한 Boolean (solid 에 Unite / OffsetFaces "General Failure"·"Operation failed") 은 **body 를 영구 오염(poison)** 시킨다. 같은 프로세스 안에서의 모든 재시도(scale-trick 포함)는 이후 `"object is deleted"` 를 던진다. **in-process 복구 불가** — 회복은 harness 레벨의 **fresh re-import** 뿐이다 (per-cell SC 격리가 정답인 근거). 따라서 forceScale 류 tolerance 회복은 실패 후 reactive 가 아니라 **import 직후 proactive** 로만 유효하다.

- OffsetFaces 는 일부 boss cap 에서 silent no-op / 실패한다. 검증된 **대체 전략 = 동축-cylinder Boolean**: free CAP 단(端)에서 키우려면 Unite, 줄이려면 Subtract. CAP 단은 `ContainsPoint` 로 "끝 너머 = air" 를 찾아 식별. 624ZZ 에서 gate-validated.
- fill-Unite 는 반드시 `DesignBody.Create` 래퍼 위에서 — raw `Body.Copy()` 직접 op 은 detached-body 지뢰(프로세스 crash ×3 재현).

### 곡면 feature 배치 (curved placement)

- feature 크기 `minDim/8` 은 thin part(베어링 5mm → 0.63mm)에서 sub-mm 으로 붕괴 → cut 이 engage 안 되거나 재인식 실패. **`minDim/3` + 인식 floor 1.2mm 사용**, 단 `topFaceMm/6` 으로 상한해 소형 면 overflow 방지.
- **annular(ring) 면**: bbox-중심 투영이 중앙 bore void 로 떨어진다 → 8방향×2반경 **in-plane 다점 샘플링**으로 ring 재질 위 anchor 확보. 624ZZ Add* primitive 가 0V → VERIFIED 로 전환.

### 운영 — SC cold-launch 변동성

빌드 직후 SpaceClaim cold-launch 가 **>240s 변동**한다. per-cell force-kill 이 cold cache 를 반복 무효화하면 모든 cell 이 타임아웃(빈 결과)으로 떨어진다. 완화: **SC 를 한 번 warm 한 뒤 `PerCellTimeoutSec ≥ 300`**. add-in import 없는 smoke 스크립트(89s 통과)로 "코드 회귀" vs "환경 cold-start" 를 격리 판별.
