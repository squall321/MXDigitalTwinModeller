# MX Material Twin - Implementation Status

## ✅ Phase 0 Complete: Infrastructure Setup

### What's Been Implemented

**통합 Calibration System** (Single MaterialCalibrator.exe)
- ✅ Unified runner.py with calibration_type routing
- ✅ PyInstaller-based standalone executable (99MB)
- ✅ JSON-based I/O (IronPython-compatible)
- ✅ Elastic calibration implemented (Phase 1A)
- ✅ Placeholders for plastic, visco, hyper calibrations

**Specimen Detection** (3가지 방법 지원)
1. ✅ Workbench Parameters (P1_GaugeLength, P2_GaugeWidth, P3_Thickness, P4_SpecimenType)
2. ✅ specimen.yaml file (SpaceClaim 자동 생성 - 우선 방식)
3. ✅ .specimen.json file (STEP export fallback)

**CSV Data Input**
- ✅ 2-column CSV parsing (displacement_mm, force_N)
- ✅ IronPython-compatible simple parser
- ✅ Minimum 10 data points validation

**Elastic Calibration (Phase 1A)**
- ✅ Young's Modulus (E) calculation via linear regression
- ✅ Material suggestion based on E and density
- ✅ Stress-strain curve generation
- ✅ R² goodness-of-fit reporting

**Engineering Data Integration**
- ✅ AddMaterial() to Engineering Data
- ✅ Set E, ν, ρ properties
- ✅ Material name customization

---

## 📦 Deployment

### Files Deployed

**Executable (통합 모듈):**
```
C:\Users\Sonic\AppData\Roaming\Ansys\v252\ACT\extensions\MXSimulator\
└── calibration\
    └── MaterialCalibrator.exe  (99MB, 통합 executable)
```

**Python Modules (Fallback):**
```
└── calibration\
    ├── runner.py                      (통합 runner)
    ├── elastic_calibrator.py          (Phase 1A)
    ├── run_elastic_calibration.py     (Legacy, Phase 1A)
    └── utils\
        ├── csv_parser.py
        └── yaml_parser.py             (YAML 파싱 - 수정됨)
```

**Build Tools:**
```
└── calibration\
    ├── build_calibrator.bat           (PyInstaller 빌드 스크립트)
    └── setup_venv.bat                 (venv 생성)
```

### Deployment Command

```bash
cd d:\MXDigitalTwinModeller\Mechanical
bash deploy_mxsimulator.sh
```

---

## 🧪 Testing

### Standalone Test Suite

```bash
cd d:\MXDigitalTwinModeller\Mechanical\MXSimulator
python test_material_twin.py
```

**Test Results:**
```
[PASS] YAML Parser         - specimen.yaml 파싱 정상
[PASS] CSV Parser          - tensile_data.csv 파싱 정상
[PASS] Elastic Calibration - MaterialCalibrator.exe 정상 동작

Total: 3/3 tests passed ✓
```

### Test Files

**d:\MXDigitalTwinModeller\test_specimen.yaml:**
```yaml
SpecimenType: "ASTM E8 Standard"
GaugeLength_mm: 50.0
GaugeWidth_mm: 12.5
Thickness_mm: 3.0
```

**d:\MXDigitalTwinModeller\test_tensile_data.csv:**
```csv
displacement_mm,force_N
0.000,0.0
0.005,187.5
...
0.100,3750.0
```

### Expected Calibration Result

```json
{
  "success": true,
  "calibration_type": "elastic",
  "result": {
    "E_modulus": 50000.0,
    "poisson_ratio": 0.3,
    "density": 7850.0,
    "r_squared": 1.0,
    "elastic_limit_stress": 100.0,
    "num_points_used": 21,
    "suggested_material": "Aluminum Alloy (estimated)"
  }
}
```

---

## 🔧 Usage in ANSYS Mechanical

### Method 1: Workbench Parameters (우선 방법)

**In SpaceClaim Component:**
1. Create ASTM E8 specimen geometry
2. Define Workbench Parameters:
   - `P1_GaugeLength` = 50.0 mm
   - `P2_GaugeWidth` = 12.5 mm
   - `P3_Thickness` = 3.0 mm
   - `P4_SpecimenType` = "ASTM E8"
3. Create Named Selections:
   - `Specimen_LeftEnd`
   - `Specimen_RightEnd`
4. Update geometry

**In Mechanical Component (connected to SpaceClaim):**
1. Open Material Twin dialog
2. Click "Detect Specimen" → Auto-detects from Parameters ✓
3. Select CSV file (displacement_mm, force_N)
4. Set Poisson's ratio (ν) and density (ρ)
5. Click "Calibrate Young's Modulus (E)"
6. Review results (E, R², material suggestion)
7. Click "Create Material in Engineering Data"
8. Material is now available in Engineering Data

### Method 2: YAML File (SpaceClaim 자동 생성)

**In SpaceClaim:**
1. Create specimen using TensileTest module
2. Module auto-generates `specimen.yaml` in same directory as .scdoc
3. Save geometry

**In Mechanical:**
1. Import geometry from SpaceClaim
2. Material Twin → Detect Specimen
3. Auto-detects from specimen.yaml ✓
4. (Same as Method 1 from step 3)

### Method 3: JSON File (STEP Export Fallback)

**In SpaceClaim:**
1. Export to STEP
2. Optionally create `.specimen.json` in project directory

**In Mechanical:**
1. Import STEP file
2. Material Twin → Detect Specimen
3. Detects from .specimen.json if present
4. (Same as Method 1 from step 3)

---

## 🏗️ Architecture

### Unified Calibration Pattern

**Input JSON Format:**
```json
{
  "calibration_type": "elastic",  // "plastic", "visco", "hyper"
  "displacement": [...],
  "force": [...],
  "gauge_length": 50.0,
  "cross_section_area": 37.5,
  "poisson_ratio": 0.3,
  "density": 7850.0,
  "max_elastic_strain": 0.002
}
```

**Execution Pattern (Same as PostProcess):**
```python
# Priority 1: Standalone exe (권장)
MaterialCalibrator.exe input.json

# Fallback: venv python
python runner.py input.json
```

**Output JSON Format:**
```json
{
  "success": true,
  "calibration_type": "elastic",
  "result": {
    "E_modulus": 50000.0,
    "poisson_ratio": 0.3,
    "density": 7850.0,
    "r_squared": 1.0,
    "elastic_limit_stress": 100.0,
    "num_points_used": 21,
    "suggested_material": "Aluminum Alloy"
  }
}
```

### Type Routing (Unified Module)

```python
# runner.py
def main():
    calib_type = input_data.get('calibration_type', 'elastic')

    if calib_type == 'elastic':
        result = run_elastic_calibration(input_data)  # Phase 1A ✓
    elif calib_type == 'plastic':
        result = run_plastic_calibration(input_data)  # Phase 1B TODO
    elif calib_type == 'visco':
        result = run_visco_calibration(input_data)    # Phase 3A TODO
    elif calib_type == 'hyper':
        result = run_hyper_calibration(input_data)    # Phase 3B TODO
```

---

## 🐛 Known Issues (Fixed)

### ~~Issue 1: YAML Parser Returning Zeros~~
**Status:** ✅ FIXED (2024-02-22 23:15)

**Problem:** `get_specimen_info_from_yaml()` was looking for lowercase keys (`gauge_length_mm`) but YAML file had PascalCase keys (`GaugeLength_mm`)

**Solution:** Updated parser to support both naming conventions:
```python
gauge_length = yaml_data.get('GaugeLength_mm', yaml_data.get('gauge_length_mm', 0))
```

### ~~Issue 2: Separate Executables Per Phase~~
**Status:** ✅ FIXED (2024-02-22 23:00)

**Problem:** Initially created separate ElasticCalibrator.exe for each calibration type

**Solution:** Created unified `MaterialCalibrator.exe` with type routing via `calibration_type` parameter

---

## 📋 Next Steps (Future Phases)

### Phase 1B: Bilinear Plastic Calibration (σy, Et)
- [ ] Implement `run_plastic_calibration()` in runner.py
- [ ] Create `calibration/plastic_calibrator.py`
- [ ] ANSYS TB,PLAS,BKIN output
- [ ] LS-DYNA MAT003 output

### Phase 3A: Prony Series (DMA → Viscoelasticity)
- [ ] Implement `run_visco_calibration()` in runner.py
- [ ] Create `calibration/visco_calibrator.py`
- [ ] DMA CSV input (Freq_Hz, E'_MPa, E''_MPa)
- [ ] ANSYS TB,PRONY output
- [ ] LS-DYNA MAT077 Prony output

### Phase 3B: Hyperelastic + Viscoelastic
- [ ] Implement `run_hyper_calibration()` in runner.py
- [ ] Large deformation tensile data input
- [ ] Mooney-Rivlin / Ogden model selection
- [ ] ANSYS TB,HYPER output
- [ ] LS-DYNA MAT077 full card output

### Phase 6: COR Prediction & Optimization ⭐
- [ ] Drop test simulation setup
- [ ] β-factor grid search
- [ ] COR error < 5% target validation

---

## 🔍 Debugging

### Check Deployment

```bash
# Check if MaterialCalibrator.exe exists
ls -lh "C:/Users/Sonic/AppData/Roaming/Ansys/v252/ACT/extensions/MXSimulator/calibration/MaterialCalibrator.exe"
# Expected: 99MB file

# Check if Python modules are deployed
ls "C:/Users/Sonic/AppData/Roaming/Ansys/v252/ACT/extensions/MXSimulator/calibration/"
# Expected: runner.py, elastic_calibrator.py, utils/
```

### Manual Calibration Test

```bash
# Create input JSON
cat > test_input.json << EOF
{
  "calibration_type": "elastic",
  "displacement": [0.0, 0.01, 0.02, 0.03, 0.04, 0.05],
  "force": [0.0, 750.0, 1500.0, 2250.0, 3000.0, 3750.0],
  "gauge_length": 50.0,
  "cross_section_area": 37.5,
  "poisson_ratio": 0.3,
  "density": 7850.0,
  "max_elastic_strain": 0.002
}
EOF

# Run calibrator
cd "C:/Users/Sonic/AppData/Roaming/Ansys/v252/ACT/extensions/MXSimulator/calibration"
./MaterialCalibrator.exe test_input.json

# Check result
cat test_input_result.json
```

### Rebuild Executable

```bash
cd d:\MXDigitalTwinModeller\Mechanical\MXSimulator\calibration
cmd //c build_calibrator.bat

# Copy to deployment location
cp dist/MaterialCalibrator.exe ./MaterialCalibrator.exe

# Deploy
cd ..\..\Mechanical
bash deploy_mxsimulator.sh
```

---

## 📚 References

- **Plan:** `C:\Users\Sonic\.claude\plans\steady-launching-frost.md`
- **Deployment Script:** `d:\MXDigitalTwinModeller\Mechanical\deploy_mxsimulator.sh`
- **Build Script:** `d:\MXDigitalTwinModeller\Mechanical\MXSimulator\calibration\build_calibrator.bat`
- **Test Suite:** `d:\MXDigitalTwinModeller\Mechanical\MXSimulator\test_material_twin.py`

---

**Last Updated:** 2024-02-22 23:30
**Status:** Phase 0 Complete ✅ | Phase 1A Complete ✅
**Next Milestone:** Phase 1B (Plastic Calibration)
