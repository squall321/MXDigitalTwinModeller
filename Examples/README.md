# MX Material Twin - Example Data

이 폴더에는 Material Twin 기능을 테스트하기 위한 예제 CSV 파일들이 포함되어 있습니다.

## 📁 파일 목록

### Phase 1A: Elastic Calibration (탄성 물성 보정)

#### 1. **Steel_ASTM_E8_Elastic.csv**
- **재료:** Structural Steel
- **Young's Modulus:** 200,000 MPa (200 GPa)
- **Poisson's Ratio:** 0.30
- **Density:** 7,850 kg/m³
- **시편:** ASTM E8 Standard (50mm × 12.5mm × 3mm)
- **변형률 범위:** 0 ~ 0.25% (탄성 영역)
- **데이터 포인트:** 100
- **노이즈:** ±15N (realistic experimental noise)

**용도:** 가장 일반적인 구조용 강철 보정 예제

---

#### 2. **Aluminum_6061_Elastic.csv**
- **재료:** Aluminum Alloy 6061-T6
- **Young's Modulus:** 70,000 MPa (70 GPa)
- **Poisson's Ratio:** 0.33
- **Density:** 2,700 kg/m³
- **시편:** ASTM E8 Standard
- **변형률 범위:** 0 ~ 0.3%
- **데이터 포인트:** 100
- **노이즈:** ±8N

**용도:** 알루미늄 합금 보정 (경량 재료)

---

#### 3. **ABS_Plastic_ASTM_D638.csv**
- **재료:** ABS Plastic
- **Young's Modulus:** 2,500 MPa (2.5 GPa)
- **Poisson's Ratio:** 0.35
- **Density:** 1,050 kg/m³
- **시편:** ASTM D638 Type I (50mm × 13mm × 3.2mm)
- **변형률 범위:** 0 ~ 0.5%
- **데이터 포인트:** 120
- **노이즈:** ±2N

**용도:** 플라스틱 재료 보정 (저강성 재료)

---

#### 4. **Copper_Elastic.csv**
- **재료:** Pure Copper
- **Young's Modulus:** 120,000 MPa (120 GPa)
- **Poisson's Ratio:** 0.34
- **Density:** 8,960 kg/m³
- **시편:** ASTM E8 Standard
- **변형률 범위:** 0 ~ 0.2%
- **데이터 포인트:** 80
- **노이즈:** ±10N

**용도:** 구리 재료 보정 (전기/전자 부품)

---

#### 5. **Steel_Reference_LowNoise.csv**
- **재료:** Structural Steel (Reference)
- **Young's Modulus:** 200,000 MPa
- **변형률 범위:** 0 ~ 0.2%
- **데이터 포인트:** 50
- **노이즈:** 0N (perfect data)

**용도:** 알고리즘 검증용 (이상적 데이터, 오차 < 1% 기대)

---

### Phase 1B: Plastic Calibration (소성 경화 보정) - Preview

#### 6. **Steel_Bilinear_Plastic.csv**
- **재료:** Structural Steel with Plasticity
- **Young's Modulus:** 200,000 MPa
- **Yield Stress:** 250 MPa
- **Tangent Modulus:** 5,000 MPa (plastic hardening slope)
- **변형률 범위:** 0 ~ 5% (탄성 + 소성)
- **데이터 포인트:** 200
- **노이즈:** ±20N

**용도:** Phase 1B (Bilinear Plastic Calibration) 테스트용
- TB,PLAS,BKIN 파라미터 추정
- LS-DYNA MAT003 (Plastic Kinematic) 생성

---

## 🚀 사용 방법

### Phase 1A 테스트 (현재 구현됨)

1. **ANSYS Mechanical 실행**
2. **Geometry 임포트** (또는 SpaceClaim에서 시편 생성)
3. **Material Twin 버튼 클릭**
4. **Step 1:** Detect Specimen
   - SpaceClaim에서 생성한 경우: YAML 자동 감지
   - 수동 입력: Gauge Length = 50mm, Width = 12.5mm, Thickness = 3mm
5. **Step 2:** Select CSV File
   - 예: `Steel_ASTM_E8_Elastic.csv` 선택
6. **Step 3:** Material Properties
   - Poisson's Ratio: 0.30 (default)
   - Density: 7850 kg/m³ (default)
7. **Step 4:** Calibrate Young's Modulus (E)
   - 결과 예상: E ≈ 200,000 MPa, R² > 0.99
8. **Step 5:** Create Material in Engineering Data
   - Material Name: "Structural_Steel"

---

## 📊 CSV 포맷

모든 파일은 다음 포맷을 따릅니다:

```csv
displacement_mm,force_N
0.000000,0.000000
0.001250,187.500000
0.002500,375.000000
...
```

**컬럼:**
- `displacement_mm`: 변위 [mm]
- `force_N`: 하중 [N]

**헤더 변형 지원:**
- displacement: `displacement`, `disp`, `extension`, `elongation`
- force: `force`, `load`

---

## 🔍 예상 보정 결과

| CSV File | Expected E (MPa) | Material Suggestion | Notes |
|----------|-----------------|---------------------|-------|
| Steel_ASTM_E8_Elastic.csv | 200,000 | Structural Steel | ±1% with noise |
| Aluminum_6061_Elastic.csv | 70,000 | Aluminum Alloy | ±1% with noise |
| ABS_Plastic_ASTM_D638.csv | 2,500 | ABS Plastic | ±2% with noise |
| Copper_Elastic.csv | 120,000 | Unknown Material | Copper not in database |
| Steel_Reference_LowNoise.csv | 200,000 | Structural Steel | <0.01% (perfect) |
| Steel_Bilinear_Plastic.csv | 200,000* | Structural Steel | *Elastic region only (Phase 1A) |

---

## 📝 노트

### Phase 1A (현재 구현)
- 탄성 영역만 사용 (0~0.2% strain)
- Linear regression으로 E 계산
- Plastic data는 무시됨

### Phase 1B (향후 구현)
- `Steel_Bilinear_Plastic.csv` 사용
- 소성 영역 포함 (0.2%~5% strain)
- σy, Et 파라미터 추정
- TB,PLAS,BKIN + LS-DYNA MAT003 생성

---

## 🧪 검증 체크리스트

Phase 1A 테스트 시:

- [ ] Steel_Reference_LowNoise.csv → E = 200,000 MPa (±1%)
- [ ] Steel_ASTM_E8_Elastic.csv → E = 199,000~201,000 MPa (노이즈 고려)
- [ ] Aluminum_6061_Elastic.csv → E = 69,000~71,000 MPa
- [ ] ABS_Plastic_ASTM_D638.csv → E = 2,400~2,600 MPa
- [ ] R² > 0.95 (all cases)
- [ ] Engineering Data에 재료 생성 성공
- [ ] Material assignment to geometry 가능

---

**생성일:** 2026-02-22
**MX Material Twin Version:** Phase 1A
