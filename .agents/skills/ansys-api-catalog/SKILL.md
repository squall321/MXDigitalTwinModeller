---
name: ansys-api-catalog
description: ANSYS Mechanical ACT + SpaceClaim API 카탈로그. 새 기능 개발 시 사용 가능한 API 를 빠르게 찾고, 사용 검증된 예제와 함정 (gotchas) 을 확인할 수 있다. 카탈로그는 (1) 이 프로젝트에서 실제 호출된 verified API (~240개), (2) XML 문서로 알려진 documented surface (~2100줄), (3) 검증된 패턴/함정 기록 (~740줄), (4) 즉시 실행 가능한 verification 스크립트로 구성된다. SpaceClaim Add-In (C#) 또는 Mechanical ACT Extension (IronPython) 작업 시 호출.
---

# ANSYS API Catalog Skill

ANSYS Student v252 환경에서 SpaceClaim (Add-In) 과 Mechanical (ACT Extension) 두 사이드의 API 카탈로그.
새 기능 개발 시 "이 API 가 실제로 동작할까?" 를 빠르게 확인하기 위한 reference.

## 사용 시점

이 skill 은 다음 상황에서 호출:

- **새 SpaceClaim 기능 개발**: 형상/메쉬/접촉/Selection/Body 조작 등 필요 시 → `spaceclaim/` 참조
- **새 Mechanical ACT 기능 개발**: Analysis/Load/Contact/NamedSelection/Material 등 → `mechanical/` 참조
- **API 가 실제 동작하는지 모를 때**: `verification/` 의 스크립트를 ANSYS 안에서 실행해서 직접 확인
- **API 함정 확인**: 어떤 메서드 이름이 잘못됐는지 (예: `GetFaceNormal` ❌ → `NormalAtParam` ✓) → `verification/01_api_learnings.md`

## 카탈로그 구조

```
.Codex/skills/ansys-api-catalog/
├── SKILL.md                                  ← 이 파일 (95줄)
├── spaceclaim/
│   ├── 01_used_in_project.md                 ← 검증 ✓ (198줄, ~100 API)
│   └── 02_documented_surface.md              ← XML 문서 (1019줄)
├── mechanical/
│   ├── 01_used_in_project.md                 ← 검증 ✓ (187줄, ~140 API)
│   └── 02_documented_surface.md              ← XML 문서 (1079줄)
├── verification/
│   ├── 01_api_learnings.md                   ← 함정+패턴 (738줄, 20 verified + 11 wrong)
│   ├── verify_spaceclaim_api.py              ← SC Script Editor 실행 (282줄)
│   └── verify_mechanical_api.py              ← Mechanical Console 실행 (348줄)
└── raw/
    ├── SpaceClaim.Api.V252.Scripting.xml     ← XML 원본 (1.6MB, Scripting.* 만)
    ├── SpaceClaim.Api.V252.xml               ← XML 원본 (1.5MB, Modeler/Geometry 포함)
    └── Ansys.ACT.WB1.xml                     ← Mechanical XML (3.6MB)
```

**총 카탈로그**: ~3950줄 + 6.7MB XML 원본 + 2개 실행 가능 verification 스크립트

## 신뢰도 우선순위

API 사용 결정 시 다음 순서로 확인:

1. **`01_used_in_project.md`** ⭐ — 프로젝트에서 실제 호출되고 빌드/실행 성공한 API. 100% 신뢰.
2. **`verification/01_api_learnings.md`** — 검증된 패턴 (20개) + 알려진 함정 (11개 wrong API). 두 번째로 신뢰.
3. **`02_documented_surface.md`** — XML 문서 surface. 시그니처는 정확하지만 ANSYS Student 라이선스/정책상 일부 메서드가 제한될 수 있음. **사용 전 verification 스크립트로 확인 권장**.
4. **XML raw 파일** — 위 카탈로그가 누락한 항목 확인 시 직접 grep.

## 새 API 검증 절차

새 기능 개발 중 새 API 를 사용하려면:

1. `verification/verify_*.py` 의 적절한 위치에 테스트 코드 추가
2. ANSYS 안에서 실행해서 동작 확인 (사용법은 각 스크립트 헤더 주석)
3. 동작하면 `01_used_in_project.md` 에 추가 (verified ✓ 표시)
4. 함정 발견 시 `verification/01_api_learnings.md` 에 기록

## 빠른 참조 — 가장 자주 쓰는 API

### SpaceClaim 측 (C# / IronPython)

| 작업 | API |
|---|---|
| 형상 생성 (extrude) | `Body.ExtrudeProfile(profile, distance)` |
| Boolean 연산 | `body.Subtract(others)`, `body.Unite(...)`, `body.Intersect(...)` |
| Profile (Rectangle) | `new RectangleProfile(Plane.PlaneXY, w, h)` |
| 좌표/방향 | `Point.Create(x,y,z)`, `Direction.DirX/Y/Z`, `Frame.Create(origin, dirX, dirY)` |
| Named Selection 생성 | `Group.Create(part, name, IDocObject[])` |
| 활성 윈도우 | `Window.ActiveWindow.Document` |
| 트랜잭션 | `WriteBlock.ExecuteTask("label", () => { ... })` — **모든 mutation 필수** |
| 단위 | **모든 입력은 미터 (m)**. mm 입력은 `GeometryUtils.MmToMeters(mm)` 로 변환 |

### Mechanical 측 (Python ACT)

| 작업 | API |
|---|---|
| 모델 루트 | `ExtAPI.DataModel.Project.Model` |
| 바디 순회 | `model.Geometry.GetChildren(DataModelObjectCategory.Body, True)` |
| **면 법선** | `face.NormalAtParam(u, v)` — **NOT `GetFaceNormal`** |
| Selection 생성 | `ExtAPI.SelectionManager.CreateSelectionInfo(SelectionTypeEnum.GeometryEntities)` |
| Selection 채우기 | `sel.Entities = [face1, face2]` — **list 할당, `.Add()` 아님** |
| NS 추가 | `Model.AddNamedSelection()` + `ns.Generate()` |
| Analysis 추가 | `Model.AddModalAnalysis()`, `AddTransientStructuralAnalysis()`, `AddStaticStructuralAnalysis()` |
| **Modal range** | `settings.ModalRangeMaximum = Quantity('2000 [Hz]')` — **NOT `RangeMaximum`** |
| **Transient step time** | `settings.SetStepEndTime(step, Quantity(...))` — **메서드, 1-based** |
| STEP 임포트 | `geometry.AddGeometryImport().Import(path, Format.Automatic, pref)` |
| Force 추가 | `analysis.Activate()` 후 `analysis.AddForce()` |
| Force tabular | `force.XComponent.Inputs[0].DiscreteValues = [Quantity(...)...]` + `.Output.DiscreteValues = [...]` |
| Material 추가 | `eng_data.AddMaterial()` + `mat.SetPropertyByName("Young's Modulus", Quantity(E, "MPa"))` |
| 트랜잭션 | `with Transaction(True): ...` (선택, 일괄 mutation 시) |

**전체 함정 목록 + 검증된 패턴 20개**: [verification/01_api_learnings.md](verification/01_api_learnings.md)

## verification 스크립트 사용법

### SpaceClaim 측 `verify_spaceclaim_api.py`
1. SpaceClaim 실행 → 빈 document 생성
2. Design 탭 → Script Editor (Ctrl+M)
3. `verify_spaceclaim_api.py` 내용 붙여넣기 → Run
4. 콘솔의 PASS/FAIL/N/A 표 확인
5. 테스트용 body/group 은 자동 cleanup

### Mechanical 측 `verify_mechanical_api.py`
1. ANSYS Workbench → Mechanical 열기
2. (옵션) 모델 로드 — face/body 관련 테스트 활성화 시
3. File → Scripting → Open Script → `verify_mechanical_api.py` → Run
4. 콘솔의 PASS/FAIL/N/A 표 확인
5. 테스트용 NS/Analysis 는 자동 cleanup (기존 analysis 있으면 modal 테스트 skip)

## 환경

- **ANSYS Student v252** 설치 경로: `D:\Program Files\ANSYS Inc\ANSYS Student\v252\`
- **SpaceClaim XML** 원본 위치:
  - `...\Discovery\SpaceClaim.Api.V252\SpaceClaim.Api.V252.xml` (Body/Face/Plane 등 주력 클래스)
  - `...\Discovery\SpaceClaim.Api.V252\SpaceClaim.Api.V252.Scripting.xml` (Scripting commands 만)
- **Mechanical XML** 원본 위치: `...\aisol\bin\winx64\Ansys.ACT.WB1.xml`
- 세 XML 모두 `raw/` 폴더에 사본 보존 (XML 업데이트 시 동기 필요)

## 한계 / 주의사항

- **ANSYS Student v252** 기준. Commercial 버전의 추가 API 는 미반영.
- XML 의 `internal` / `obsolete` 멤버는 제외.
- **Project-verified API는 100% 신뢰**. Documented surface 의 API 는 시그니처 정확하지만 동작은 verification 스크립트로 확인 필요.
- Student 라이선스 제약 (element count 등) 으로 같은 API 라도 commercial 과 결과가 다를 수 있음.
- 카탈로그는 SpaceClaim/Mechanical 의 모든 API 가 아닌 **이 프로젝트 도메인** (시편 모델링, 메쉬, 접촉, 진동/모달 해석, 재료 캘리브레이션) 에 관련된 API 중심.

## 새 카테고리 확장

새 API 그룹 카탈로그 추가 시:
- `spaceclaim/` 또는 `mechanical/` 에 `03_<category>.md` 같은 식으로 추가
- 또는 `01_used_in_project.md` 에 새 섹션 추가
- raw XML 에서 grep 으로 추출하거나, ANSYS 안에서 `dir(obj)` 로 직접 확인

## 관련 자료

- [lat.md/api-learnings.md](../../../lat.md/api-learnings.md) — memory-resident short-form gotchas
- [Mechanical/MXSimulator/ANSYS_Mechanical_API_Reference.txt](../../../Mechanical/MXSimulator/ANSYS_Mechanical_API_Reference.txt) — exhaustive 1016-line notes
- [Mechanical/MXSimulator/TROUBLESHOOTING.md](../../../Mechanical/MXSimulator/TROUBLESHOOTING.md) — Material Twin pipeline troubleshooting
- 카탈로그 본문의 cross-reference 들
