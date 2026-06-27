# API Learnings

ANSYS ACT (Mechanical) 와 SpaceClaim API 를 다루며 발견된 함정과 검증된 호출 패턴을 기록한다. 문서가 부정확하거나 IDE 자동완성이 잘못된 메서드를 제시하는 경우가 많아, 실제 런타임에서 확인한 사실만 여기에 남긴다.

## SpaceClaim Face Normal — `NormalAtParam` 만 동작

**잘못된 API**: `face.GetFaceNormal()` — 문서에 자주 등장하지만 SpaceClaim V252 에서는 존재하지 않거나 동작하지 않음.

**올바른 API**: `face.NormalAtParam(u, v)` — 파라미터 공간 `(u, v)` 에서 면의 법선 벡터 반환.

```csharp
var bbox = face.GetBoundingBox(Matrix.Identity);
var center = bbox.Center;
var (u, v) = face.GetParam(center);
Vector normal = face.NormalAtParam(u, v);
```

면 중심에서의 법선을 얻으려면 `GetParam(center)` 로 파라미터 좌표를 먼저 변환해야 한다. `u=0.5, v=0.5` 은 NURBS 면이 아니면 잘못된 위치가 될 수 있어 권장하지 않는다.

## Mechanical Modal Range — `ModalRangeMaximum`

`AnalysisSettings` 의 모달 주파수 상한 속성은 `ModalRangeMaximum` 이다. `RangeMaximum` (단축형) 은 컴파일은 되지만 잘못된 속성을 건드린다.

```python
modal_analysis.AnalysisSettings.ModalRangeMaximum = Quantity('2000 [Hz]')
```

## Mechanical Transient — `SetStepEndTime`

Transient Analysis 의 step 별 종료 시간은 직접 속성으로 못 set 하고 메서드를 써야 한다:

```python
settings = transient_analysis.AnalysisSettings
settings.SetStepEndTime(1, Quantity(end_time, 's'))
```

`settings.StepEndTime` 같은 속성 set 은 step index 를 받지 못해 멀티스텝에서는 사용 불가.

## 핵심 API 참조 XML

API 자동완성보다 빠르게 정확한 시그니처를 찾을 수 있는 1차 자료:

| 파일 | 용도 |
|---|---|
| `D:\Program Files\ANSYS Inc\ANSYS Student\v252\aisol\bin\winx64\Ansys.ACT.WB1.xml` | Mechanical ACT 의 모든 `Add*` 메서드 시그니처 (Force, NamedSelection, Analysis 등) |
| `D:\Program Files\ANSYS Inc\ANSYS Student\v252\Discovery\SpaceClaim.Api.V252\SpaceClaim.Api.V252.Scripting.xml` | SpaceClaim 스크립팅 API |

별도의 상세 노트는 `[[Mechanical/MXSimulator/ANSYS_Mechanical_API_Reference.txt]]` 에 정리되어 있다.

## SpaceClaim GeoBody 접근

`Body` 객체 자체로는 Faces 컬렉션을 직접 인덱싱할 수 없고, `GetGeoBody()` 로 변환해서 `IGeoBody.Faces[i]` 로 접근한다:

```python
geo_body = body.GetGeoBody()
for face_idx in range(geo_body.Faces.Count):
    face = geo_body.Faces[face_idx]
```

임포트된 바디만 골라내려면 `body.IsImported` 플래그를 사용. `Model.Geometry.GetChildren(DataModelObjectCategory.Body, True)` 로 전체 바디 트리 순회.

## STEP Import — `GeometryImport`

```python
geometry = ExtAPI.DataModel.Project.Model.Geometry
geom_import = geometry.AddGeometryImport()

import_pref = Ansys.ACT.Mechanical.Utilities.GeometryImportPreference()
import_pref.ProcessNamedSelections = True

geom_import.Import(
    step_path,
    GeometryImportPreference.Format.Automatic,
    import_pref
)
```

`Format.Automatic` 가 `.stp`/`.step` 양쪽을 처리한다.

## WinForms — DataGridView in GroupBox

`Dock = DockStyle.Fill` 이 GroupBox 안의 DataGridView 에서는 의도대로 동작하지 않는다. 명시적 `Size` 와 `Location` 을 지정할 것. 이 함정은 [[face-pair-ns]] 와 [[mesh]] 의 여러 다이얼로그에서 반복적으로 부딪힌 문제.

## ACT Extension 글로벌

Mechanical ACT 런타임에서는 다음 글로벌이 자동 주입된다:

- `ExtAPI` — 모든 API 의 진입점
- `ExtAPI.DataModel.Project.Model` — 모델 트리 루트
- `ExtAPI.SelectionManager` — 사용자 선택/프로그래매틱 선택

IDE 에서 "Import could not be resolved" 경고가 떠도 무시. 실제 런타임에서는 정상.

## Shared DLL 동적 로드 (IronPython)

```python
import clr, os
dll_path = os.path.join(os.path.dirname(__file__), "bin", "MXDigitalTwinModeller.Core.dll")
if os.path.exists(dll_path):
    clr.AddReferenceToFileAndPath(dll_path)
    from MXDigitalTwinModeller.Core.Spatial import SpatialIndex
```

DLL 이 없어도 main.py 가 죽지 않도록 `if os.path.exists` 가드. [[architecture#공용 Core DLL]] 참조.

## SpaceClaim DLL 잠금

빌드 시 SpaceClaim 이 실행 중이면 `MXDigitalTwinModeller.dll` 이 잠겨 빌드 실패. 빌드 전 SpaceClaim 종료 필수. `[[Mechanical/kill_ansys.bat]]` 가 ANSYS 프로세스 일괄 종료 스크립트.

---

# CAD-Modification (Mod) 자동화 — API 함정 & 검증

> 출처: Mod 돌파 세션 (Cycle 35+, 2026-06-10~17). 전체 맥락은 `[[MOD_BREAKTHROUGH_PLAN.md]]` §8(Phase 4), 커널 지뢰는 `[[sc-kernel-landmines.md]]`(메모리), 검증 모듈은 `[[Test/RE_SelfTest/kernel_truth.py]]`.

## Kernel-truth 검증 — 추출기 좌표를 믿지 마라 (가장 중요)

`FeatureExtractor` 가 산출하는 feature 좌표는 **곡면/dirty STEP part 에서 신뢰 불가**다 (11752 H1 앵커가 실제 bore 에서 48mm off — 추출 axis 가 solid 벽을 관통). 모든 oracle 이 재추출에 의존하면 거짓을 상속한다. 견고한 검증은 **live 커널 B-rep 에서 형상을 직접** 읽는다 (`[[Test/RE_SelfTest/kernel_truth.py]]`):

- **canonical face fingerprint**:
  - Cylinder = foot-of-perpendicular 앵커(world origin → axis line 수선의 발) + `Cylinder.Radius` + face 의 축방향 extent (`cylinder_record()`)
  - Plane = sign-canonical 법선(`_canon_dir`: 첫 유의 성분을 양수로) + signed offset + area (`face_sig()`)
  - 전체 body = face-sig multiset + `Body.Shape.Volume` + bbox, **전부 양자화** (`fingerprint()`)
- **oracle primitive** = `diff(fp_before, fp_after)` → 추가/제거된 face-sig multiset + dvol. 추출기 무관 L1 oracle.
- **mutation test 로 검증**: nist `ChangeHoleDiameter` 25→30 을 **정확히 cylinder radius 한 개 12.5→15mm 이동**으로 검출 (그 외 delta 0) = stable + sensitive + specific = `KERNEL_TRUTH_VALIDATED`.
- **규칙: oracle 은 mutation test 를 통과하기 전까지 신뢰 금지.** ("근원적 검증 먼저" 원칙)

## Feature 오분류 발견 — "곡면 커널 한계" 는 신화였다

11752 의 "12개 실패 cell" 은 곡면 커널 한계가 아니라 **`FeatureExtractor` 가 solid PIN 을 hole 로 오분류**한 것이었다 (dirty STEP import 에서 orientation/`IsReversed` 가 뒤집힘). 4중 가설 제거로 규명: ~~위치~~(relocation 이 참 cylinder r=8.573mm 정위) · ~~tolerance~~(1000× scale-trick 무효) · ~~cap tangency~~(돌출 50% 무효) · ~~poison~~(fresh body 무효). 결정타 = **kernel-truth pin-probe**: relocated cylinder 축 중심에 `ContainsPoint` → solid=pin, void=hole. 결과 `axis_centre_solid=true` 확정.

- **수정**: `CylinderRoleClassifier.ProbeSolidCore` — `ContainsPoint` material-side 로 hole↔boss(`IsReversed`)를 교정. clean import 무회귀, flipped import 만 교정. → 11752 H1 이 boss B1 로 재분류되어 hole-op poison-실패 → boss-op 정상 처리.
- **독립 입증**: 624ZZ 베어링(진짜 곡면)의 Move/Remove/RotateHole 은 전부 **VERIFIED** → hole 머신은 곡면에서 정상. "곡면 한계" 가 아니라 분류 오류임을 교차검증.

## IronPython API 바인딩 함정 (Mod 드라이버)

1. **`CircleProfile`** — IronPython 은 4-arg ctor `CircleProfile(Plane, double radius, PointUV location, double angle)` 를 바인딩한다. C# 의 2-arg 형은 기본값을 쓰지만 IronPython 은 4개 전부 필요:
   ```python
   CircleProfile(plane, R, PointUV.Create(0, 0), 0.0)
   ```
2. **`Body.Unite` / `Body.Subtract`** — Python list 가 아니라 .NET `ICollection[Body]` 를 요구:
   ```python
   from System import Array
   target.Unite(Array[Body]([tool]))
   ```
3. **`DesignBody`** 는 `SpaceClaim.Api.V252` (최상위 namespace) 에 있다 — `.Modeler` 가 **아니다**.
4. **`Cylinder.Radius`** 는 런타임에 존재하지만 API XML 카탈로그 dump 에는 **없다**. 카탈로그 부재 ≠ 런타임 부재.
