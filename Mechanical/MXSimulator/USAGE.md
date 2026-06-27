# MXSimulator ACT Extension 사용법

## 워크플로우 개요

```
Keyword 입력 → Contact Face 검출 → Named Selection 생성 → CSV Force 적용 → Modal Superposition Analysis
```

---

## 전제 조건

- ANSYS Mechanical이 열려 있고, **Geometry 셀에 형상이 로드**되어 있어야 함
- STEP import는 Workbench의 Geometry 셀에서 upstream 처리 (Extension 내부에서 불가)
- Mesh는 Extension 외부(Mechanical 표준 UI)에서 독립적으로 수행

---

## Step 1 — Target Body Selection & Contact Face Detection

**위치**: "Target Body Selection & Contact Face Detection" 그룹

| 필드 | 설명 |
|------|------|
| Keywords | 쉼표 구분, 대소문자 무시 (예: `Cap, Lid, ASTM`) |
| Tolerance | 접촉 판정 허용 간격 (mm, 기본 0.1) |

**버튼**: `Find Target Bodies & Detect Contact Faces`

- 키워드가 비어있으면 모델 전체 바디를 대상으로 함
- 3조건 AND 접촉 판정:
  1. 법선 반대 방향 (`dot < -0.9`)
  2. face_A 중심 → face_B 평면 수직거리 < tolerance
  3. face_B 중심 → face_A 평면 수직거리 < tolerance
- 결과: `"Found N bodies, M contact faces (+Z:k, -Z:k, ...)"`
- 0 결과 시 tolerance를 늘려보거나 keyword 확인

---

## Step 2 — Create Named Selections

**위치**: "Named Selections (Auto-Created)" 그룹

**버튼**: `Create Named Selections from Detected Faces`

- Step 1 완료 후 같은 dialog 세션에서 클릭해야 함 (dialog 닫으면 face 데이터 리셋)
- 방향별 NS 생성: `Contact_+Z`, `Contact_-Z`, `Contact_+X`, 등
- 로그에서 `[NS] Create Named Selections clicked. face_data_list count: N` 확인
  - N=0 이면 Step 1을 먼저 실행
  - N>0 이면 NS 생성 진행

---

## Step 3 — Apply Forces (CSV)

**위치**: "Time Force Application (CSV)" 그룹

CSV 형식:
```
Time(s),Force(N)
0.000000,0.0000
0.000100,31.3953
...
```

예제 파일 (MXSimulator 폴더):
- `force_sine_100hz.csv` — 100Hz 정현파 ±500N (정상상태 진동)
- `force_damped_sine.csv` — 감쇠 사인파 (충격 후 진동)
- `force_impulse.csv` — 해닝 펄스 2000N (순간 충격, 모드 가진용)

**버튼**: `Apply Forces to Named Selections`

- 실행 전 analysis가 있어야 함 (Step 4 먼저 또는 기존 analysis 사용)
- 방향별 축/부호 자동 결정: `+Z` → Z축 양수, `-Z` → Z축 음수

---

## Step 4 — Modal Superposition Analysis

**위치**: "Modal Superposition Analysis Setup" 그룹

| 설정 | 기본값 |
|------|--------|
| Number of Modes | 20 |
| Max Frequency | 1000 Hz |
| End Time | 0.02 s |
| Time Step | 0.0001 s |

**버튼**: `Create Modal Superposition Analysis`

- Modal Analysis + Transient Structural 두 개 생성
- Solution Method (Mode Superposition) 설정 시도, 실패 시 수동으로:
  `Analysis Settings → Solution Method → Mode Superposition`
- 이후 순서: Modal 먼저 Solve → Transient Solve

---

## 배포

```
소스: d:\MXDigitalTwinModeller\Mechanical\MXSimulator\main.py
설치: C:\Users\{user}\AppData\Roaming\Ansys\v252\ACT\extensions\MXSimulator\main.py
```

변경 후 배포:
```bash
cp "/d/MXDigitalTwinModeller/Mechanical/MXSimulator/main.py" \
   "/c/Users/Sonic/AppData/Roaming/Ansys/v252/ACT/extensions/MXSimulator/main.py"
```

Mechanical 재시작 또는 `Tools → Manage Extensions → Reload` 필요

---

## 트러블슈팅

| 증상 | 원인 | 해결 |
|------|------|------|
| Contact faces = 0 | tolerance 너무 작거나 바디가 실제로 떨어져 있음 | tolerance 0.5~1mm로 증가 |
| NS 생성 안 됨 | dialog 닫았다 재열어서 face_data_list 리셋 | 같은 dialog에서 Find → Create NS 연속 실행 |
| force 적용 안 됨 | analysis가 없음 | Analysis 먼저 생성 |
| Modal settings 경고 | parameterized property | 수동으로 설정 |
