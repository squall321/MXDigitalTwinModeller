# Specimen Detection 진단 가이드

## 문제: Specimen 인식이 안됨 (CSV는 정상)

### 원인 분석

SpaceClaim에서 시편 생성 시 `specimen.yaml` 파일을 자동 생성하는 코드는 이미 구현되어 있습니다.
하지만 **Document가 저장되어 있어야만** YAML을 생성합니다.

**코드 위치:**
- `Services/TensileTest/SpecimenMetadataService.cs` Line 41-47:
  ```csharp
  string documentPath = document.Path;
  if (string.IsNullOrEmpty(documentPath))
  {
      // 저장되지 않은 문서 - 사용자에게 저장 요청
      System.Diagnostics.Debug.WriteLine(
          "Document must be saved before creating metadata. Skipping metadata export.");
      return;  // ← YAML 생성 건너뜀!
  }
  ```

### 해결 방법

#### Option 1: SpaceClaim에서 Document 저장 후 시편 생성 (권장)

1. **SpaceClaim 실행**
2. **File → Save As...**
   - 저장 위치: `d:\TestProjects\tensile_test.scdoc`
3. **인장시편 버튼 클릭**
   - ASTM E8 Standard 선택
   - 생성 버튼 클릭
4. **확인**: `d:\TestProjects\specimen.yaml` 파일 생성되었는지 확인
   ```bash
   ls d:/TestProjects/specimen.yaml
   ```

5. **Mechanical 실행**
   - File → Import → `d:\TestProjects\tensile_test.scdoc`
   - Material Twin → Detect Specimen
   - **예상 결과**: "Detected from SpaceClaim YAML" ✓

#### Option 2: Workbench 통합 (자동 연결)

1. **Workbench 실행**
2. **SpaceClaim Component 추가**
   - 우클릭 → Open with SpaceClaim
3. **시편 생성**
   - 인장시편 버튼 → ASTM E8 Standard → 생성
   - **중요**: SpaceClaim 창에서 Ctrl+S (저장!)
4. **SpaceClaim 닫기** → Update
5. **Mechanical Component 연결**
   - SpaceClaim Component → 우클릭 → Transfer Data to New → Static Structural
6. **Mechanical에서 확인**
   - Material Twin → Detect Specimen
   - **예상 결과**: "Detected from SpaceClaim YAML" ✓

#### Option 3: 수동 Workbench Parameters (Fallback)

YAML 생성이 안되면 수동으로 Parameters 생성:

**Workbench에서:**
1. **SpaceClaim Component** → Parameters 탭
2. **우클릭 → New Input Parameter**
   - Name: `P1_GaugeLength`
   - Type: `Length`
   - Value: `50 mm`
3. **반복**:
   - `P2_GaugeWidth` = `12.5 mm`
   - `P3_Thickness` = `3.0 mm`
   - `P4_SpecimenType` = `"ASTM E8"` (String)

**Mechanical에서:**
- Material Twin → Detect Specimen
- **예상 결과**: "Detected from Workbench Parameters" ✓

---

## 진단 체크리스트

### 1. SpaceClaim Document 저장 확인

```bash
# SpaceClaim에서 생성한 .scdoc 파일이 있는지 확인
ls -la d:/TestProjects/*.scdoc
```

**예상 결과:**
```
-rw-r--r-- 1 Sonic 197121 12345 Feb 22 23:00 tensile_test.scdoc
```

### 2. specimen.yaml 생성 확인

```bash
# .scdoc과 같은 폴더에 specimen.yaml이 있는지 확인
ls -la d:/TestProjects/specimen.yaml
cat d:/TestProjects/specimen.yaml
```

**예상 결과:**
```yaml
# MX Material Twin - Specimen Metadata
# Auto-generated from SpaceClaim: 2024-02-22 23:00:00

specimen_type: ASTM_E8_Standard
gauge_length_mm: 50.0
gauge_width_mm: 12.5
thickness_mm: 3.0
grip_length_mm: 57.0
grip_width_mm: 20.0
total_length_mm: 200.0
fillet_radius_mm: 12.5

# Named Selections for boundary conditions
named_selections:
- Specimen_LeftEnd
- Specimen_RightEnd
- Specimen_GaugeSection

# Creation information
created_date: 2024-02-22T23:00:00.000Z
spaceclaim_version: V252
document_path: d:\TestProjects\tensile_test.scdoc
```

### 3. Mechanical에서 Geometry 소스 확인

**Mechanical Python Console에서:**
```python
geometry = ExtAPI.DataModel.Project.Model.Geometry
print("Geometry Source:", geometry.Source if hasattr(geometry, 'Source') else 'None')
print("Geometry SourceFile:", geometry.SourceFile if hasattr(geometry, 'SourceFile') else 'None')

import os
source_file = geometry.Source if hasattr(geometry, 'Source') else geometry.SourceFile
if source_file and os.path.exists(source_file):
    source_dir = os.path.dirname(source_file)
    yaml_path = os.path.join(source_dir, 'specimen.yaml')
    print("Expected YAML path:", yaml_path)
    print("YAML exists:", os.path.exists(yaml_path))
```

**예상 출력:**
```
Geometry Source: d:\TestProjects\tensile_test.scdoc
Expected YAML path: d:\TestProjects\specimen.yaml
YAML exists: True
```

### 4. YAML Parser 테스트

```bash
cd d:/MXDigitalTwinModeller/Mechanical/MXSimulator
python -c "
import sys
sys.path.insert(0, 'calibration')
from utils.yaml_parser import parse_yaml, get_specimen_info_from_yaml

yaml_data = parse_yaml('d:/TestProjects/specimen.yaml')
print('YAML data:', yaml_data)

specimen_info = get_specimen_info_from_yaml(yaml_data)
print('Specimen info:', specimen_info)
"
```

**예상 출력:**
```
YAML data: {'specimen_type': 'ASTM_E8_Standard', 'gauge_length_mm': 50.0, ...}
Specimen info: {'GaugeLength': 50.0, 'GaugeWidth': 12.5, 'Thickness': 3.0, ...}
```

---

## 빠른 테스트 (지금 바로 확인)

### Test 1: SpaceClaim에서 시편 생성 + 저장

```bash
# 1. SpaceClaim 실행
# 2. File → New
# 3. File → Save As → d:/test_specimen.scdoc
# 4. 인장시편 버튼 → ASTM E8 Standard → 생성
# 5. Ctrl+S (저장)
# 6. 확인:
ls -la d:/specimen.yaml
cat d:/specimen.yaml
```

### Test 2: Mechanical에서 인식 확인

```bash
# 1. ANSYS Mechanical 실행
# 2. File → Import Geometry → d:/test_specimen.scdoc
# 3. Material Twin 버튼 클릭
# 4. "Detect Specimen" 버튼 클릭
# 5. 예상 결과: "Status: Detected from SpaceClaim YAML" (초록색)
```

---

## 문제 해결: YAML 키 이름 불일치

**SpaceClaim에서 생성하는 YAML:**
```yaml
gauge_length_mm: 50.0    # lowercase_mm
```

**yaml_parser.py가 기대하는 키:**
```python
# 두 가지 형식 모두 지원하도록 이미 수정됨 (2024-02-22 23:15)
gauge_length = yaml_data.get('GaugeLength_mm', yaml_data.get('gauge_length_mm', 0))
```

✓ **이미 수정 완료** - lowercase, PascalCase 모두 지원

---

## 자동 진단 스크립트

```bash
cd d:/MXDigitalTwinModeller
python diagnose_specimen.py
```

**diagnose_specimen.py를 생성하겠습니다...**

---

## 요약

**문제 원인:**
1. ✅ YAML 생성 코드 구현됨 (`SpecimenMetadataService.SaveMetadata`)
2. ❌ **Document가 저장되지 않으면 YAML 생성 건너뜀**
3. ✅ YAML 파싱 코드 정상 작동 (`yaml_parser.py`)

**해결책:**
1. **SpaceClaim에서 Document 저장 (Ctrl+S) 후 시편 생성**
2. 또는 **Workbench에서 SpaceClaim Component 사용** (자동 저장)
3. 또는 **수동으로 Workbench Parameters 생성** (Fallback)

**다음 단계:**
1. SpaceClaim에서 시편 생성 → 저장 확인
2. `specimen.yaml` 파일 존재 확인
3. Mechanical에서 Detection 테스트
