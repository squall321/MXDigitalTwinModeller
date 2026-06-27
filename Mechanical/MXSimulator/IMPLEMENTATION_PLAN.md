# Cap Vibration Time Force - 구현 계획서

## 목표

STEP 파일 임포트 → 접촉면 검출 (방향별) → Named Selection 생성 → Time Force 적용 (CSV/Table) → Modal Superposition Transient Analysis 자동 설정

---

## API 가용성 검증 결과

### ✅ 확인된 API (실존)

| 기능 | API | 출처 |
|------|-----|------|
| STEP 임포트 | `GeometryImport.Import(path, format, pref)` | [GeometryImport API](https://scripting.mechanical.docs.pyansys.com/version/stable/api/ansys/mechanical/stubs/v242/Ansys/ACT/Automation/Mechanical/GeometryImport.html) |
| 바디 접근 | `body.GetGeoBody()` → `IGeoBody` | [GetGeoBody](https://storage.ansys.com/corp/ACT_Reference_Guide_doc_v180/Mechanical/Reference.methode.Ansys.ACT.Automation.Mechanical.Body.GetGeoBody.html) |
| 면 법선 | `face.GetFaceNormal(u, v)` → `Vector` | [Get Normal of Face](https://ansyshelp.ansys.com/public/Views/Secured/corp/v251/en/act_script/act_script_examples_get_normal_of_a_face.html) |
| Named Selection 생성 | `Model.AddNamedSelection()` | 표준 API |
| Force 추가 | `analysis.AddForce()` | [Analysis.AddForce](https://ansyshelp.ansys.com/public///Views/Secured/corp/v242/en/act_script/mech_apis_AnalysisObject.html) |
| Tabular Data | `force.XComponent.Output.DiscreteValues = [...]` | 표준 API |
| Modal Analysis | `Model.Analyses.Add(AnalysisType)` | PyMechanical 예제 참조 |

### ⚠️ 불확실한 API (문서 미확인)

| 기능 | 추정 API | 대안 |
|------|----------|------|
| Transient Modal Super. | `AddTransientStructuralAnalysis()` + 설정 | Workbench 템플릿 드래그 방식 대신 수동 설정 필요 가능성 |
| GeoBody.Faces 순회 | `geobody.Faces[i]` | `geobody.Faces.Count` + 인덱싱 |
| 접촉면 거리 계산 | 면 중심점 간 거리 (`GetFaceCenter()`) | 간단한 유클리드 거리 |

---

## 단계별 구현 계획

### Phase 1: STEP 임포트 + 면 분석 (핵심 로직)

**목표**: STEP 파일을 임포트하고 각 면의 법선 방향 분석

**API 시퀀스**:
```python
# 1. STEP 임포트
geometry = ExtAPI.DataModel.Project.Model.Geometry
geom_import = geometry.AddGeometryImport()

import_pref = Ansys.ACT.Mechanical.Utilities.GeometryImportPreference()
import_pref.ProcessNamedSelections = True
geom_import.Import(step_path, GeometryImportPreference.Format.Automatic, import_pref)

# 2. 임포트된 바디 가져오기
all_bodies = Model.Geometry.GetChildren(DataModelObjectCategory.Body, True)
imported_bodies = [b for b in all_bodies if b.IsImported]  # 필터링

# 3. 각 바디의 면 순회 + 법선 분석
face_data_list = []
for body in imported_bodies:
    geo_body = body.GetGeoBody()  # IGeoBody

    for face_idx in range(geo_body.Faces.Count):
        face = geo_body.Faces[face_idx]

        # 면 중심에서 법선 계산 (u=0.5, v=0.5는 파라미터 공간 중심)
        normal_vec = face.GetFaceNormal(0.5, 0.5)

        # 법선을 주요 축으로 분류
        direction = classify_normal_direction(normal_vec)  # '+Z', '-Z', ...

        face_data_list.append({
            'body': body,
            'face': face,
            'face_idx': face_idx,
            'normal': (normal_vec.X, normal_vec.Y, normal_vec.Z),
            'direction': direction
        })

def classify_normal_direction(normal):
    """법선 벡터를 주요 축 방향으로 분류"""
    nx, ny, nz = abs(normal.X), abs(normal.Y), abs(normal.Z)

    # 가장 큰 성분
    if nz > nx and nz > ny:
        return '+Z' if normal.Z > 0 else '-Z'
    elif ny > nx and ny > nz:
        return '+Y' if normal.Y > 0 else '-Y'
    else:
        return '+X' if normal.X > 0 else '-X'
```

**검증 포인트**:
- ✅ `GeometryImport.Import()` - 확인됨
- ✅ `body.GetGeoBody()` - 확인됨
- ⚠️ `geo_body.Faces[i]` - 문서 미확인, 시도 필요
- ✅ `face.GetFaceNormal(u, v)` - 확인됨

**위험도**: **중간** (Faces 컬렉션 접근 방식 확인 필요)

---

### Phase 2: 접촉면 검출

**목표**: 임포트된 STEP 면과 기존 모델의 접촉면 찾기

**알고리즘**:
```python
def detect_contact_faces(imported_faces, tolerance_mm=1.0):
    """
    임포트된 면과 기존 바디의 접촉면 검출
    - 거리 < tolerance
    - 법선 반대 방향 (dot < -0.8)
    """
    tol_m = tolerance_mm / 1000.0

    # 기존 바디 (임포트 전부터 있던 것)
    all_bodies = Model.Geometry.GetChildren(DataModelObjectCategory.Body, True)
    existing_bodies = [b for b in all_bodies if not b.IsImported]

    contact_pairs = []

    for imp_data in imported_faces:
        imp_center = imp_data['face'].GetFaceCenter()  # Point
        imp_normal = imp_data['normal']

        for exist_body in existing_bodies:
            geo_body = exist_body.GetGeoBody()

            for j in range(geo_body.Faces.Count):
                exist_face = geo_body.Faces[j]
                exist_center = exist_face.GetFaceCenter()
                exist_normal = exist_face.GetFaceNormal(0.5, 0.5)

                # 거리 계산
                dx = imp_center.X - exist_center.X
                dy = imp_center.Y - exist_center.Y
                dz = imp_center.Z - exist_center.Z
                dist = (dx**2 + dy**2 + dz**2)**0.5

                if dist < tol_m:
                    # 법선 반대 방향 확인
                    dot = (imp_normal[0] * exist_normal.X +
                           imp_normal[1] * exist_normal.Y +
                           imp_normal[2] * exist_normal.Z)

                    if dot < -0.8:  # 거의 반대 (cos(180°) = -1)
                        contact_pairs.append({
                            'imported_face_data': imp_data,
                            'existing_face': exist_face,
                            'direction': imp_data['direction']
                        })

    return contact_pairs
```

**검증 포인트**:
- ⚠️ `face.GetFaceCenter()` - 문서 미확인, 일반적으로 있을 것으로 예상
- ✅ `body.IsImported` - 속성 존재 가능성 높음 (또는 타임스탬프로 구분)

**위험도**: **낮음** (간단한 기하 계산)

**대안**: `GetFaceCenter()`가 없으면 `GetFaceNormal()`을 여러 점에서 호출해서 평균 계산

---

### Phase 3: Named Selection 생성

**목표**: 방향별로 접촉면을 Named Selection으로 그룹화

**API 시퀀스**:
```python
def create_directional_named_selections(contact_pairs):
    """
    Contact_+Z, Contact_-Z 등 방향별 NS 생성
    """
    model = ExtAPI.DataModel.Project.Model

    # 방향별 그룹화
    by_direction = {}
    for pair in contact_pairs:
        direction = pair['direction']
        if direction not in by_direction:
            by_direction[direction] = []
        by_direction[direction].append(pair['imported_face_data']['face'])

    ns_dict = {}
    for direction, faces in by_direction.items():
        # Named Selection 생성
        ns = model.AddNamedSelection()
        ns.Name = "Contact_" + direction
        ns.ScopingMethod = GeometryDefineByType.Worksheet

        # 면 선택
        selection = ExtAPI.SelectionManager.CreateSelectionInfo(
            SelectionTypeEnum.GeometryEntities)

        for face in faces:
            selection.Entities.Add(face)

        ns.Location = selection
        ns_dict[direction] = ns

    return ns_dict
```

**검증 포인트**:
- ✅ `Model.AddNamedSelection()` - 표준 API
- ✅ `SelectionManager.CreateSelectionInfo()` - 표준 API

**위험도**: **낮음**

---

### Phase 4: Time Force 적용 (CSV/Table)

**목표**: 시간-하중 데이터를 읽고 Force 객체에 적용

**CSV 형식**:
```csv
Time(s),Force(N)
0.0,0
0.001,100
0.002,200
...
```

**API 시퀀스**:
```python
def parse_csv_force_data(csv_path):
    """CSV에서 시간-하중 데이터 읽기"""
    import csv

    time_vals = []
    force_vals = []

    with open(csv_path, 'r') as f:
        reader = csv.reader(f)
        next(reader)  # 헤더 스킵

        for row in reader:
            time_vals.append(float(row[0]))
            force_vals.append(float(row[1]))

    return time_vals, force_vals

def apply_directional_forces(ns_dict, csv_path, analysis):
    """
    방향별 Named Selection에 Force 적용
    +Z는 양수, -Z는 음수 (반대 방향)
    """
    time_vals, force_vals = parse_csv_force_data(csv_path)

    # +Z 방향 Force
    if 'Contact_+Z' in ns_dict:
        force_plus = analysis.AddForce()
        force_plus.Location = ns_dict['Contact_+Z']
        force_plus.DefineBy = LoadDefineBy.Components

        # Z 방향 양수
        discrete_vals = [Quantity(f, "N") for f in force_vals]
        force_plus.ZComponent.Output.DiscreteValues = discrete_vals

    # -Z 방향 Force (부호 반전)
    if 'Contact_-Z' in ns_dict:
        force_minus = analysis.AddForce()
        force_minus.Location = ns_dict['Contact_-Z']
        force_minus.DefineBy = LoadDefineBy.Components

        # Z 방향 음수
        discrete_vals = [Quantity(-f, "N") for f in force_vals]
        force_minus.ZComponent.Output.DiscreteValues = discrete_vals

    # X, Y 방향도 동일하게...
```

**검증 포인트**:
- ✅ `analysis.AddForce()` - 확인됨
- ✅ `force.DefineBy = LoadDefineBy.Components` - 표준 API
- ✅ `force.ZComponent.Output.DiscreteValues = [...]` - 표준 Tabular Data 방식

**위험도**: **낮음**

---

### Phase 5: Modal + Transient Analysis 설정

**목표**: Modal Analysis → Transient Structural (Modal Superposition) 생성

**API 시퀀스** (불확실, 시도 필요):
```python
def setup_modal_superposition_analysis():
    """
    Modal Analysis + Transient Structural (Mode Superposition) 생성
    """
    model = ExtAPI.DataModel.Project.Model

    # 방법 1: Analysis 추가 (확인 필요)
    try:
        modal = model.Analyses.Add(AnalysisType.Modal)
        modal.SolverType = SolverType.ProgramControlled

        transient = model.Analyses.Add(AnalysisType.TransientStructural)
        transient.SolverType = SolverType.ModalSuperposition  # 확인 필요

        return modal, transient
    except:
        pass

    # 방법 2: Workbench 템플릿 방식 (수동)
    # - Modal Analysis를 먼저 생성
    # - Solution에서 Transient를 드래그하여 연결
    # → ACT에서 자동화 어려움, 수동 설정 안내 필요

    MessageBox.Show(
        "Please manually:\n" +
        "1. Add Modal Analysis\n" +
        "2. Solve Modal\n" +
        "3. Add Transient Structural linked to Modal (drag to Solution)",
        "Manual Setup Required"
    )

    return None, None
```

**검증 포인트**:
- ⚠️ `model.Analyses.Add(AnalysisType.Modal)` - PyMechanical 예제에서 유사 패턴 확인, 정확한 메서드명 불확실
- ❌ `transient.SolverType = SolverType.ModalSuperposition` - **문서 미확인, 가장 불확실**

**위험도**: **높음** (Modal Superposition 자동 설정은 불확실)

**대안**:
1. **Semi-Auto 모드**: Modal Analysis만 생성 후, 사용자에게 Transient 수동 연결 안내
2. **Force만 적용**: 기존 분석이 있다고 가정하고 Force만 추가

---

## 최종 UI 설계

```
┌─ Cap Vibration Time Force Setup ──────────────────────┐
│                                                         │
│ [STEP Files]                                            │
│   File 1: [Browse...] _____________________.stp  [x]   │
│   File 2: [Browse...] _____________________.stp  [x]   │
│   [+ Add STEP File]                                     │
│                                                         │
│ [Contact Detection]                                     │
│   Tolerance: [1.0] mm                                   │
│   ☑ Detect +Z / -Z faces                               │
│   ☑ Detect +X / -X faces                               │
│   ☑ Detect +Y / -Y faces                               │
│   [Detect Contacts]  → Status: ___ pairs found         │
│                                                         │
│ [Force Definition]                                      │
│   Input: ● CSV File  ○ Manual Table                    │
│                                                         │
│   CSV: [Browse...] ________________.csv                │
│   Format: Time(s), Force(N)                            │
│                                                         │
│   Direction Mapping:                                    │
│   +Z faces → +Z force (as-is)                          │
│   -Z faces → -Z force (negated)                        │
│                                                         │
│ [Analysis Setup]                                        │
│   ● Auto (try to create Modal + Transient)            │
│   ○ Manual (add Forces to existing analysis)           │
│                                                         │
│   Target Analysis: [Dropdown: Analysis 1 ▼]           │
│                                                         │
│ [Execute]  [Cancel]                                    │
│                                                         │
│ [Log] ▼                                                │
│ ┌─────────────────────────────────────────────────┐   │
│ │ Imported STEP: model.stp (3 bodies)              │   │
│ │ Detected 12 contact faces                        │   │
│ │ Created NS: Contact_+Z (4 faces)                 │   │
│ │ Created NS: Contact_-Z (4 faces)                 │   │
│ │ Applied Force to Contact_+Z (500 time steps)     │   │
│ │ Complete!                                         │   │
│ └─────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────┘
```

---

## 구현 우선순위

### 🟢 Phase 1 (높은 확실성)
1. STEP 임포트 + 면 법선 분석
2. Named Selection 생성
3. CSV Force 적용 (기존 분석에)

### 🟡 Phase 2 (중간 확실성)
4. 접촉면 검출 (거리 + 법선 방향)
5. UI 통합

### 🔴 Phase 3 (낮은 확실성)
6. Modal + Transient 자동 생성 (**문서 확인 필요**)

---

## 검증 필요 사항

**실제 Mechanical에서 테스트 필요**:
1. `geo_body.Faces[i]` 문법 확인
2. `face.GetFaceCenter()` 존재 여부
3. `Model.Analyses.Add()` 정확한 메서드명
4. Modal Superposition 설정 방법

**추천**: Phase 1만 먼저 구현하여 API 동작 확인 후, Phase 2/3 진행

---

## 참고 자료

- [GeometryImport API](https://scripting.mechanical.docs.pyansys.com/version/stable/api/ansys/mechanical/stubs/v242/Ansys/ACT/Automation/Mechanical/GeometryImport.html)
- [GetGeoBody Method](https://storage.ansys.com/corp/ACT_Reference_Guide_doc_v180/Mechanical/Reference.methode.Ansys.ACT.Automation.Mechanical.Body.GetGeoBody.html)
- [Get Face Normal Example](https://ansyshelp.ansys.com/public/Views/Secured/corp/v251/en/act_script/act_script_examples_get_normal_of_a_face.html)
- [Analysis.AddForce](https://ansyshelp.ansys.com/public///Views/Secured/corp/v242/en/act_script/mech_apis_AnalysisObject.html)
- [Modal Superposition Transient](https://ansyshelp.ansys.com/public//Views/Secured/corp/v242/en/wb_sim/ds_trans_struct_analysis_type_linked_modal.html)
- [PyMechanical Modal Example](https://embedding.examples.mechanical.docs.pyansys.com/examples/01_basic/modal_acoustics_analysis.html)
