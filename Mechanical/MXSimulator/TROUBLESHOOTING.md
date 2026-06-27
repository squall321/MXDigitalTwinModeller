# Material Twin Troubleshooting Guide

## 문제 1: "Specimen Detection" 안됨

### 증상
- "Detect Specimen" 버튼 클릭 시 "Specimen not detected" 메시지

### 원인 및 해결

#### 원인 1: specimen.yaml 파일이 없음
**확인:**
```
SpaceClaim에서 시편을 생성하지 않았거나,
.scdoc 파일과 같은 폴더에 specimen.yaml이 없음
```

**해결:**
1. SpaceClaim 열기
2. MX Digital Twin → Tensile Specimen 메뉴에서 시편 생성
3. Document 저장 (Ctrl+S) → .scdoc 파일 생성
4. 같은 폴더에 `specimen.yaml` 파일 자동 생성 확인
5. Mechanical에서 Geometry → SpaceClaim Component 연결
6. Material Twin → Detect Specimen 재시도

#### 원인 2: Workbench Component가 아님 (STEP Import 사용)
**확인:**
```
Geometry가 STEP 파일에서 import된 경우
(SpaceClaim Component 연결 아님)
```

**해결 (방법 A - 권장):**
1. Workbench에서 SpaceClaim Component 추가
2. 위 "원인 1" 해결 방법 따르기

**해결 (방법 B - 수동 입력):**
1. Material Twin 버튼 클릭
2. "Specimen not detected" 메시지 확인 후 OK
3. 현재는 수동 입력 UI 미구현 → **임시방편:**
   - Workbench Parameters 직접 생성:
     - Parameters → Add Parameter
     - P1_GaugeLength = 50 mm
     - P2_GaugeWidth = 12.5 mm
     - P3_Thickness = 3.0 mm
     - P4_SpecimenType = "ASTM E8 Standard"
   - Detect Specimen 재시도

#### 원인 3: Geometry Source 경로를 찾을 수 없음
**확인:**
Mechanical Scripting Console에서 실행:
```python
geometry = ExtAPI.DataModel.Project.Model.Geometry
print("Geometry:", geometry)
if geometry:
    print("Source:", getattr(geometry, 'Source', 'N/A'))
    print("SourceFile:", getattr(geometry, 'SourceFile', 'N/A'))
```

**해결:**
- Geometry Component가 올바르게 연결되었는지 확인
- Workbench Project 저장 (Ctrl+S)
- Geometry 우클릭 → Update

---

## 문제 2: CSV 파일 선택/파싱 실패

### 증상
- CSV 파일 선택 후 "CSV parsing error" 메시지

### 원인 및 해결

#### 원인 1: CSV 포맷 오류
**요구 포맷:**
```csv
displacement_mm,force_N
0.000000,0.000000
0.001250,187.500000
0.002500,375.000000
...
```

**확인사항:**
- 헤더 행 필수 (첫 번째 줄)
- 2개 컬럼 (displacement, force)
- 최소 10개 데이터 포인트

**해결:**
- Examples 폴더의 예제 CSV 파일 사용:
  - `Steel_ASTM_E8_Elastic.csv`
  - `Aluminum_6061_Elastic.csv`
  - etc.

#### 원인 2: 파일 인코딩 문제
**해결:**
- CSV 파일을 UTF-8 또는 ASCII로 저장
- 특수문자 사용 금지 (숫자만)

#### 원인 3: 데이터 포인트 부족
**확인:**
```
"Insufficient data points: X (min 10 required)" 메시지
```

**해결:**
- 최소 10개 이상의 데이터 포인트 필요
- 더 많은 데이터 수집 또는 보간

---

## 문제 3: Calibration 실패

### 증상
- "Calibrate Young's Modulus" 버튼 클릭 시 에러

### 원인 및 해결

#### 원인 1: venv가 설치되지 않음
**확인:**
```
"Virtual environment not found!" 메시지
경로: Mechanical/MXSimulator/calibration_env/Scripts/python.exe
```

**해결:**
```cmd
cd D:\MXDigitalTwinModeller\Mechanical\MXSimulator
setup_venv.bat
```

설치 확인:
```cmd
calibration_env\Scripts\activate
python -c "import scipy; print('OK')"
deactivate
```

#### 원인 2: Specimen 정보 없음
**확인:**
```
"Please detect specimen first." 메시지
```

**해결:**
- Step 1: Detect Specimen 먼저 실행
- 위 "문제 1" 참조

#### 원인 3: CSV 데이터 없음
**확인:**
```
"Please select CSV data first." 메시지
```

**해결:**
- Step 2: Select CSV File 먼저 실행
- 위 "문제 2" 참조

#### 원인 4: 탄성 영역 데이터 부족
**확인:**
```
"Insufficient data points in elastic region (0~0.20%): X points"
```

**해결:**
- 변형률 0~0.2% 범위에 최소 10개 포인트 필요
- 더 세밀한 초기 데이터 수집
- max_elastic_strain 조정 (코드 수정 필요)

#### 원인 5: 비현실적인 E 값
**확인:**
```
"Unrealistic Young's modulus: X MPa"
```

**해결:**
- 단위 확인:
  - Displacement: [mm]
  - Force: [N]
  - 결과 E: [MPa]
- Cross-section area 확인 (mm²)
- Gauge length 확인 (mm)

---

## 문제 4: Material 생성 실패

### 증상
- "Create Material in Engineering Data" 버튼 클릭 시 에러

### 원인 및 해결

#### 원인 1: Calibration 미실행
**해결:**
- Step 4: Calibrate Young's Modulus 먼저 실행

#### 원인 2: Material 이름 중복
**확인:**
```
"Material 'XXX' already exists. Overwrite?" 메시지
```

**해결:**
- Yes: 기존 Material 덮어쓰기
- No: Material 이름 변경 후 재시도

#### 원인 3: Engineering Data 접근 불가
**해결:**
- Mechanical Model 열려 있는지 확인
- Engineering Data 탭 접근 가능한지 확인

---

## 디버그 모드

### Diagnostic Script 실행
```
Mechanical → Scripting → Open Script
  → D:\MXDigitalTwinModeller\Mechanical\MXSimulator\diagnose_material_twin.py
  → Run
```

**출력 확인:**
- [1] Script Location
- [2] Calibration Module (exists?)
- [3] Virtual Environment (python.exe?)
- [4] Module Import (success/fail)
- [5] Geometry Detection (Source path?)
- [6] Workbench Parameters (list)
- [7] CSV Parser Test (parse example CSV)

### Log 확인
IronPython Console 출력:
- YAML Detection Error
- Calibration subprocess stdout/stderr

---

## 예제 워크플로우 (정상 동작)

### 시나리오: Steel 시편 보정

**1. SpaceClaim (Phase 0):**
```
- MX Digital Twin → Tensile Specimen
- ASTM E8 Standard 선택
- Gauge Length: 50mm, Width: 12.5mm, Thickness: 3mm
- Create → 시편 생성
- Ctrl+S → Document 저장 (예: specimen.scdoc)
- specimen.yaml 자동 생성 확인 (같은 폴더)
```

**2. Workbench:**
```
- SpaceClaim Component 추가
- 위에서 만든 specimen.scdoc 열기
- Mechanical Component 추가
- Geometry → SpaceClaim Component 연결
- Update All
```

**3. Mechanical (Phase 1A):**
```
- Material Twin 버튼 클릭
- Step 1: Detect Specimen
  → "Detected from SpaceClaim YAML" ✓
  → Gauge Length: 50mm, Width: 12.5mm, Thickness: 3mm ✓

- Step 2: Select CSV File
  → Examples/Steel_ASTM_E8_Elastic.csv
  → "100 data points loaded" ✓

- Step 3: Material Properties
  → Poisson's Ratio: 0.30 (default)
  → Density: 7850 kg/m³ (default)

- Step 4: Calibrate
  → Young's Modulus: 200,065 MPa ✓
  → R²: 0.999992 ✓
  → Suggested: Structural_Steel

- Step 5: Create Material
  → Material Name: "Structural_Steel"
  → Create ✓
  → Engineering Data에 Material 생성 확인
```

---

## 자주 묻는 질문

**Q: venv를 매번 설치해야 하나요?**
A: 아니요. setup_venv.bat는 최초 1회만 실행하면 됩니다.

**Q: Workbench 없이 Standalone Mechanical에서도 되나요?**
A: 부분적으로 가능합니다:
   - STEP Import로 Geometry 가져오기
   - Workbench Parameters 직접 생성 (P1_GaugeLength 등)
   - 또는 수동 입력 UI (Phase 1A에서 구현 예정)

**Q: 여러 CSV 파일을 동시에 처리할 수 있나요?**
A: 현재는 1개씩만 가능합니다. Multi-rate calibration은 Phase 2A에서 구현 예정.

**Q: LS-DYNA MAT 카드는 언제 생성되나요?**
A: Phase 1B (Plastic) 이후부터 생성됩니다. 현재 Phase 1A는 탄성 물성만 다룹니다.

---

**문제가 계속되면:**
1. diagnose_material_twin.py 실행
2. 출력 결과 확인
3. 해당 섹션의 에러 메시지 복사
4. GitHub Issues 또는 개발자에게 문의
