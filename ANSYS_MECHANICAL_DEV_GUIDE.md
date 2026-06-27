# ANSYS Mechanical 기능 개발 가이드

ANSYS Mechanical(Workbench) 에 커스텀 기능을 **ACT Extension** 으로 추가하는 실전 가이드.
이 프로젝트(MXDigitalTwinModeller / MXSimulator)에서 **실제로 빌드·실행·디버깅하며 검증한** 지식만 담았다. 다른 ANSYS 프로젝트에 그대로 이식 가능하도록 자립적으로 작성.

- 대상: ANSYS Student/Commercial v252 (v2025 R2). 다른 버전은 버전 번호만 치환.
- 언어: **IronPython 2.7** (ACT 스크립트 엔진). C# 도 가능하나 이 가이드는 Python ACT 중심.
- 핵심 함정은 ⚠️ 로 표시 — 전부 이 프로젝트에서 실제로 터진 버그다.

---

## 0. 큰 그림 — ACT Extension 이란

ANSYS Mechanical 의 기능 확장 = **ACT(Application Customization Toolkit) Extension**.
구성:

```
extensions/
  MyExtension.xml          ← manifest (툴바/콜백 정의) — extensions/ 부모에 위치
  MyExtension/
    main.py                ← IronPython 진입점 (콜백 함수들)
    images/                ← 툴바 아이콘 (png)
    bin/                   ← (선택) 공용 C# DLL
```

Mechanical 이 시작될 때 manifest 를 읽어 툴바를 그리고, 버튼 클릭 → manifest 의 콜백 이름 → main.py 의 동명 함수를 호출한다. 함수 안에서 `ExtAPI` 루트로 모델 트리 전체에 접근·조작한다.

**개발 루프:** 코드 수정 → extensions 폴더에 배포 → Mechanical 재시작(또는 Extension reload) → 툴바 버튼 클릭 → 동작 확인 → ACT Console 로 디버깅.

---

## 1. Manifest (`MyExtension.xml`)

extensions **부모 디렉토리**에 둔다 (extension 폴더 안이 아니라 그 옆). 검증된 최소 구조:

```xml
<?xml version="1.0" encoding="utf-8"?>
<extension version="2025" minorversion="2" name="My Feature">
  <guid shortid="MyExtension">8F7A9B2C-3D4E-5F6A-7B8C-9D0E1F2A3B4C</guid>
  <author>...</author>
  <description>...</description>
  <script src="main.py" />
  <interface context="Mechanical">
    <images>images</images>
    <callbacks>
      <oninit>on_init</oninit>          <!-- 로드 시 1회 호출 -->
    </callbacks>
    <toolbar name="MyToolbar" caption="My Feature">
      <entry name="DoThing" caption="Do Thing" icon="do_thing">
        <callbacks>
          <onclick>show_my_dialog</onclick>   <!-- → main.py 의 show_my_dialog(analysis) -->
        </callbacks>
      </entry>
      <separator />
      <!-- 더 많은 entry ... -->
    </toolbar>
  </interface>
</extension>
```

- `version`/`minorversion` = ANSYS 버전 (2025 / 2). 안 맞으면 로드 거부.
- `guid` 는 extension 마다 **고유**해야 함. 복붙 시 새로 생성.
- `icon="do_thing"` → `images/do_thing.png` 를 찾음 (확장자 없이).
- `onclick` 콜백은 main.py 에서 **`def show_my_dialog(analysis):`** 형태 (인자 1개 = 현재 analysis context, 안 쓰면 무시).

---

## 2. main.py 진입점

IronPython 2.7. 상단에서 .NET 어셈블리 + Ansys 네임스페이스를 로드한다.

```python
# encoding: utf-8
import os, clr

# WPF/WinForms UI 를 쓰려면 (대화상자)
clr.AddReference("PresentationFramework")
clr.AddReference("PresentationCore")
clr.AddReference("WindowsBase")
clr.AddReference("System.Xaml")
clr.AddReference("System.Windows.Forms")
import System
from System.Windows.Forms import OpenFileDialog, FolderBrowserDialog, DialogResult

# Ansys ACT API
import Ansys
from Ansys.Mechanical.DataModel.Enums import DataModelObjectCategory, ContactType, LoadDefineBy
from Ansys.Core.Units import Quantity

def on_init(context):
    pass   # 로드 시 1회 (전역 초기화)

def show_my_dialog(analysis):
    # 버튼 클릭 핸들러. ExtAPI 로 모델 조작.
    model = ExtAPI.DataModel.Project.Model
    bodies = model.Geometry.GetChildren(DataModelObjectCategory.Body, True)
    # ... 작업 ...
```

⚠️ **`ExtAPI` 는 전역으로 주입됨** — import 불필요, ACT 런타임이 넣어준다. 콜백 함수 안에서 바로 쓴다.

⚠️ **단일 파일이 관리하기 편하다.** 이 프로젝트는 main.py 한 파일에 모든 콜백 + 헬퍼를 둔다(수천 줄). 여러 파일로 쪼개면 IronPython import 경로가 까다로워진다 — 정말 필요할 때만 분리.

---

## 3. ExtAPI — 모델 트리 접근 (검증된 API 맵)

`ExtAPI.DataModel.Project.Model` 이 루트. 아래는 **이 프로젝트에서 실제 호출·검증된** API (line-ref 는 우리 main.py 기준이지만 패턴은 보편적).

### 3.1 Geometry / Body / Face

```python
model = ExtAPI.DataModel.Project.Model
bodies = model.Geometry.GetChildren(DataModelObjectCategory.Body, True)   # 모든 body (재귀)
for body in bodies:
    geo = body.GetGeoBody()              # GeoBody (기하 질의용)
    for face in geo.Faces:
        n = face.NormalAtParam(u, v)     # 면 법선 → n.X, n.Y, n.Z
        area = face.Area
```

⚠️ **면 법선은 `face.NormalAtParam(u, v)`** — `GetFaceNormal(u,v)` 아님. GetFaceNormal 은 다른 face 객체(DesignFace)용이라 GeoFace 엔 쓰면 안 됨. (이 프로젝트의 실제 버그.)

### 3.2 Selection (면/바디 선택 → 작업 대상 지정)

```python
sel = ExtAPI.SelectionManager.CreateSelectionInfo(
    Ansys.ACT.Interfaces.Common.SelectionTypeEnum.GeometryEntities)
sel.Entities = [face1, face2]            # ⚠️ list 할당, .Add() 아님
```

### 3.3 Named Selection

```python
ns = model.AddNamedSelection()
ns.Name = "MyFaces"
ns.Location = sel                        # 위 SelectionInfo
ns.Generate()                            # ⚠️ 반드시 호출해야 실제 반영
# 조회:
existing = model.NamedSelections.GetChildren(DataModelObjectCategory.NamedSelection, True)
```

### 3.4 Analysis 추가/활성화

```python
modal     = model.AddModalAnalysis()                  # 프로젝트 검증됨
transient = model.AddTransientStructuralAnalysis()    # 프로젝트 검증됨
static    = model.AddStaticStructuralAnalysis()       # documented (이 프로젝트 미사용)
analyses  = model.GetChildren(DataModelObjectCategory.Analysis, True)

analysis.Activate()                      # ⚠️ Load/Force 추가 전 활성화 필수
analysis.Delete()
```

### 3.5 AnalysisSettings (해석 설정)

```python
# Modal 주파수 범위
modal.AnalysisSettings.ModalRangeMaximum = Quantity(2000, "Hz")   # ⚠️ RangeMaximum 아님!

# Transient 시간 스텝 — ⚠️ 프로퍼티 아니라 METHOD, step 번호 1-based
ts = transient.AnalysisSettings
ts.SetStepEndTime(1, Quantity(0.01, "sec"))
ts.SetInitialTimeStep(1, Quantity(1e-5, "sec"))
ts.SetMinimumTimeStep(1, Quantity(1e-6, "sec"))
ts.SetMaximumTimeStep(1, Quantity(1e-4, "sec"))
```

### 3.6 Loads (하중)

```python
analysis.Activate()
force = analysis.AddForce()
force.DefineBy = LoadDefineBy.Components
force.Location = sel
# tabular(시간-힘): Inputs[0] = 시간, Output = 값
force.XComponent.Inputs[0].DiscreteValues = [Quantity(t, "sec") for t in times]
force.XComponent.Output.DiscreteValues     = [Quantity(f, "N")   for f in forces]
```

### 3.7 Contact / Connections

```python
connections = model.Connections   # 또는 GetChildren(DataModelObjectCategory.ConnectionGroup, True)
contacts = connections.GetChildren(DataModelObjectCategory.ContactRegion, True)
for c in contacts:
    c.ContactType = ContactType.Bonded
```

### 3.8 Engineering Data / Materials

```python
eng_data = model.Materials   # 또는 Project 의 EngineeringData
# ⚠️ Material 필터는 C# generic 타입 명시 필요:
mats = eng_data.GetChildren[Ansys.ACT.Automation.Mechanical.Material](True)

mat = eng_data.AddMaterial()
mat.Name = "MySteel"
mat.SetPropertyByName("Young's Modulus", Quantity(200000, "MPa"))
mat.SetPropertyByName("Poisson's Ratio", Quantity(0.3, ""))        # ⚠️ 무차원은 빈 단위 ""
mat.SetPropertyByName("Density",          Quantity(7850, "kg m^-3"))
```

### 3.9 Solve & Results

```python
analysis.Solution.Solve(True)            # True = 동기(완료까지 대기)
# 결과 추가: AddTotalDeformation / AddEquivalentStress 등
# MeshData (풀린 메시): analysis.MeshData.NodeCount, .Nodes, .Elements
```

### 3.10 STEP 지오메트리 임포트 — documented, 이 프로젝트 미검증

카탈로그에 문서화돼 있으나 이 프로젝트 main.py 는 미사용(지오메트리는 Workbench Component 로 받음). 사용 전 ACT Console 로 시그니처 확인:

```python
geo_import = model.Geometry.AddGeometryImport()
geo_import.Import(path, Format.Automatic, pref)   # documented surface — 실측 확인 필요
```

---

## 4. Transaction (일괄 mutation) — documented, 이 프로젝트 미검증

여러 모델 변경을 묶을 때 (성능 + 일관성) ACT 가 `Transaction` 을 제공한다고 문서화돼 있으나 **이 프로젝트는 실제로 쓰지 않았다.** 시그니처는 ACT Console `dir()` 로 확인 후 사용(§6). 이 프로젝트의 수천 줄 main.py 는 Transaction 없이 동작하므로 대량 변경이 아니면 없어도 무방.

---

## 5. 자동 배포 (빌드 → extensions 폴더)

수동 복사 대신 빌드 스크립트로 자동화. 배포 경로:

```
%APPDATA%\ANSYS\v252\ACT\extensions\          ← MyExtension.xml (manifest)
%APPDATA%\ANSYS\v252\ACT\extensions\MyExtension\   ← main.py, images/, bin/, ...
```

MSBuild 타깃 예 (이 프로젝트의 csproj `DeployMechanicalExtension`):

```xml
<Target Name="DeployMechanicalExtension" AfterTargets="Build">
  <PropertyGroup>
    <ExtDir>$(APPDATA)\ANSYS\v252\ACT\extensions\MyExtension</ExtDir>
    <ExtParent>$(APPDATA)\ANSYS\v252\ACT\extensions</ExtParent>
  </PropertyGroup>
  <MakeDir Directories="$(ExtDir);$(ExtDir)\bin;$(ExtDir)\images" />
  <!-- manifest 는 부모로 -->
  <Copy SourceFiles="$(ProjectDir)Mechanical\MyExtension.xml"
        DestinationFiles="$(ExtParent)\MyExtension.xml" SkipUnchangedFiles="true" />
  <!-- main.py + 아이콘 -->
  <Copy SourceFiles="$(ProjectDir)Mechanical\MyExtension\main.py"
        DestinationFolder="$(ExtDir)" SkipUnchangedFiles="true" />
  <Copy SourceFiles="@(IconFiles)" DestinationFolder="$(ExtDir)\images" />
</Target>
```

배포 후 **Workbench 의 ribbon 캐시를 지워야** 새 아이콘이 보일 때가 있다:
`%APPDATA%\Ansys\v252\Applets\DSApplet\en-us` 삭제 (이 프로젝트가 빌드 시 자동으로 함).

⚠️ **Extension 재로드:** Mechanical 재시작이 가장 확실. 또는 Workbench → Extensions → Manage 에서 unload/load. ACT Console 의 `ExtAPI.ExtensionMgr` 로도 가능하나 재시작이 안전.

---

## 6. 디버깅 — ACT Console

Mechanical 의 **Automation → Scripting → Open Command Window** (ACT Console)에서 IronPython 을 직접 실행. 핵심 디버깅 도구.

```python
# 트리 탐색
model = ExtAPI.DataModel.Project.Model
print(model.Geometry.GetChildren(DataModelObjectCategory.Body, True).Count)

# 객체의 멤버 확인 (어떤 메서드/프로퍼티가 있나)
b = model.Geometry.GetChildren(DataModelObjectCategory.Body, True)[0]
print([m for m in dir(b) if not m.startswith('_')])

# 설치된 extension 목록 / 충돌 검사
for ext in ExtAPI.ExtensionMgr.Extensions:
    print(ext.Name, ext.Version)
```

⚠️ **`dir(obj)` 가 진실의 원천.** XML 문서나 메모리에 있는 시그니처가 틀릴 수 있다 (라이선스/버전 차이). 새 API 는 ACT Console 에서 `dir()` 로 실재 확인 후 사용.

⚠️ **에러는 조용히 삼켜질 수 있다.** 콜백 함수에서 예외가 나면 ACT 가 메시지박스 없이 무시할 때가 있다. 핵심 단계마다 `print()` 또는 파일 로그를 남겨 어디서 죽는지 추적 (이 프로젝트의 표준 디버깅 패턴).

---

## 7. 검증 스크립트 패턴 (새 API de-risk)

새 기능을 짜기 전, 쓰려는 API 가 **이 버전/라이선스에서 실제로 도는지** 작은 스크립트로 먼저 확인. ACT Console 에 붙여넣고 PASS/FAIL 표를 본다:

```python
def t(name, fn):
    try:
        fn(); print("PASS", name)
    except Exception as e:
        print("FAIL", name, ":", e)

model = ExtAPI.DataModel.Project.Model
t("AddModalAnalysis", lambda: model.AddModalAnalysis().Delete())
t("face NormalAtParam", lambda: model.Geometry.GetChildren(DataModelObjectCategory.Body,True)[0].GetGeoBody().Faces[0].NormalAtParam(0.5,0.5))
t("Material generic filter", lambda: model.Materials.GetChildren[Ansys.ACT.Automation.Mechanical.Material](True))
# ... 쓰려는 API 마다 한 줄 ...
```

테스트용으로 만든 NS/Analysis 는 끝에 Delete 로 정리. (이 프로젝트의 `verify_all_apis.py` 패턴.)

---

## 8. 외부 Python 연동 (venv + subprocess)

ACT 의 IronPython 2.7 은 numpy/scipy 가 없다. 무거운 계산(최적화, 데이터 처리)은 **별도 CPython venv 에 위임**:

```python
import subprocess, json, os
# main.py 에서 입력을 json 으로 쓰고, CPython 스크립트를 호출
venv_py = os.path.join(ext_dir, "calibration", "venv", "Scripts", "python.exe")
subprocess.call([venv_py, script, input_json, output_json])
result = json.load(open(output_json))
```

이 프로젝트의 Material Calibrator(탄성 계수 역산)가 이 구조: ACT UI 가 CSV/스펙 수집 → json → CPython(scipy) 캘리브레이션 → json 결과 → ACT 가 Material 생성. venv 는 배포 시 `setup_venv.bat` 로 1회 구성.

⚠️ **IronPython 2.7 문법 제약:** f-string 없음, `print` 는 문/함수 혼용 주의, `unicode` 존재. CPython 3 코드를 그대로 ACT 에 붙이면 파싱 에러. 무거운 건 venv 로.

---

## 9. 실전 함정 모음 (전부 이 프로젝트에서 터진 것)

| 함정 | 올바른 방법 |
|---|---|
| `GetFaceNormal(u,v)` on GeoFace | `face.NormalAtParam(u,v)` |
| `AnalysisSettings.RangeMaximum` | `ModalRangeMaximum` |
| Transient 시간을 프로퍼티로 set | `SetStepEndTime(step, Quantity)` **메서드**, step 1-based |
| `sel.Entities.Add(face)` | `sel.Entities = [face1, face2]` (list 할당) |
| NS 만들고 안 보임 | `ns.Generate()` 호출 필수 |
| Force 추가가 엉뚱한 analysis 에 | `analysis.Activate()` 먼저 |
| `GetChildren(Material, True)` 빈 결과 | `GetChildren[Ansys.ACT.Automation.Mechanical.Material](True)` generic 명시 |
| Poisson 비에 단위 에러 | `Quantity(0.3, "")` 무차원은 빈 문자열 |
| 콜백 예외가 조용히 사라짐 | 단계마다 print/파일로그, ACT Console 에서 재현 |
| 새 아이콘 안 보임 | ribbon 캐시(`Applets\DSApplet`) 삭제 + Mechanical 재시작 |
| CPython3 코드가 ACT 에서 파싱 에러 | IronPython 2.7 문법 준수 or venv 위임 |
| XML 문서의 API 가 안 됨 | ACT Console `dir(obj)` 로 실재 확인 (라이선스/버전차) |

---

## 10. 개발 워크플로우 요약

1. **manifest** 에 toolbar entry + onclick 콜백 추가.
2. **main.py** 에 동명 `def callback(analysis):` 작성. ExtAPI 로 모델 조작.
3. 쓰려는 새 API 는 **ACT Console 에서 `dir()`/검증스크립트로 먼저 확인** (de-risk).
4. **배포** (MSBuild 타깃 또는 수동 복사) → extensions 폴더.
5. **Mechanical 재시작** → 버튼 클릭 → 동작 확인.
6. 안 되면 **ACT Console 에서 콜백 내용을 한 줄씩 실행**하며 어디서 죽는지 추적 (조용한 예외 주의).
7. 무거운 계산은 **CPython venv + subprocess + json** 으로 분리.

---

## 11. 참고 자료 (이 프로젝트 내)

- `.claude/skills/ansys-api-catalog/mechanical/01_used_in_project.md` — 검증된 ~140 API + line refs
- `.claude/skills/ansys-api-catalog/mechanical/02_documented_surface.md` — XML 문서 surface (검증 전 참고)
- `.claude/skills/ansys-api-catalog/verification/verify_mechanical_api.py` — 실행 가능한 검증 스크립트
- `Mechanical/MXSimulator/main.py` — 실제 ACT extension (수천 줄, 모든 패턴의 레퍼런스)
- `Mechanical/MXSimulator.xml` — manifest 예
- `Mechanical/MXSimulator/TROUBLESHOOTING.md` — 도메인별 트러블슈팅
- `Mechanical/MXSimulator/ANSYS_Mechanical_API_Reference.txt` — 1000줄 상세 노트
- 핵심 XML 원본: `<ANSYS>\v252\aisol\bin\winx64\Ansys.ACT.WB1.xml` (Add* 메서드 전부)

---

## 12. 핵심 원칙 (메타)

1. **`dir()` 가 진실이다.** 문서·메모리의 시그니처보다 ACT Console 의 실측을 믿어라. 라이선스/버전이 API 를 가린다.
2. **작은 검증 먼저.** 큰 기능 짜기 전에 쓰려는 API 한 줄씩 PASS/FAIL 로 de-risk. 헛심을 아낀다.
3. **조용한 실패를 의심하라.** ACT 콜백 예외는 소리 없이 사라진다 — 로그를 심어라.
4. **무거운 건 venv 로.** IronPython 2.7 의 한계(문법·라이브러리)를 CPython subprocess 로 우회.
5. **단일 파일 + 자동 배포.** main.py 한 파일 + MSBuild 배포 타깃이 반복 루프를 빠르게 한다.
