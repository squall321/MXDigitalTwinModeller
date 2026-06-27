# Phone Designer 구현 계획

> ⚠️ **2026-06-24 정정: 이 문서의 build123d 외부 생성기 전제는 폐기됨.** 5-agent 로드맵 워크플로우가 코드를 읽고 확인: (1) `TestModelGenerator.cs` 가 이미 네이티브 SC from-scratch 생성을 함(build123d 불요), (2) build123d 는 82% 검증 mod 도구 + MCP 서버를 버리고 STEP 왕복(프로젝트 #1 취약점)을 강제함. **→ 권위 문서는 [FROM_SCRATCH_ROADMAP.md](FROM_SCRATCH_ROADMAP.md) (네이티브 SC, P0-P7).** 단 이 문서의 **Stage 사양 S00-S12 + 패턴 카탈로그는 그대로 재사용**(커널만 build123d→네이티브 SC 로 교체).

> **별도 sub-project**: ~~`tools/phone-designer/`~~ → 네이티브 SC: `Services/ReverseEngineer/Generation/`  
> **이 계획은 기존 `Commands/SmartPhone/` 폐기 + 0 부터 재설계**

---

## 1. 배경 및 목적

### 1.1 현재 상태의 문제

`Commands/SmartPhone/` 외 미커밋 상태의 Smart Phone 탭은 **architecture 와 형상 디자인 양쪽에서 폐기 수준**이다:

- **형상 품질**: Bounding rectangle + 단순 prism Boolean subtract 의 조합. 측면 곡률 0, chamfer 0, step-down pocket 0. 결과는 "박스에 사각 구멍" 수준 ([Services/SmartPhone/FrontFrameGenerator.cs](Services/SmartPhone/FrontFrameGenerator.cs) 참조).
- **Architecture**: SpaceClaim Add-In 의 4계층 패턴 (Command → Service → Dialog) 에 묶여 있어 iteration 이 30–60초 (재빌드 + SpaceClaim restart). LLM 친화적이지 못함.
- **Hack UI**: 개별 feature 다이얼로그들이 매번 `FrameLength = 152.3, FrameWidth = 71.8` 같은 하드코딩된 값으로 전체 generate 를 다시 호출. partial application 불가능.

### 1.2 새 접근법

**build123d 기반 Python sub-project + 별도 UI + LLM 통합**:

1. **Headless Python CAD** (build123d) 으로 형상 생성 → SpaceClaim startup 없이 0.1–1초 iteration
2. **Progressive refinement** stage 구조 → "박스에서 시작해서 점진적으로 폰 형태로 깎아냄"
3. **별도 web UI** (가벼운 HTML + Three.js) → 3D preview + 파라미터 조절 + LLM 채팅
4. **LLM Tool Use** (Claude API) → 자연어 설계 변경 → 즉시 재생성
5. **STEP export** → 디자인 확정 후 SpaceClaim/Mechanical 의 기존 CAE 파이프라인으로 연결

### 1.3 Non-goals (이 계획 범위 밖)

- **현재 SpaceClaim Add-In 의 다른 기능 변경 없음** — 시편/메쉬/Mechanical 측 모두 유지
- **Front Metal 외 다른 폰 부품** — 첫 단계에서는 외곽 프레임 (front metal frame) 만 구현. 백 글래스, 디스플레이, 내부 부품은 Phase 후반 또는 추후
- **다중 폰 모델 동시 지원** — 우선 iPhone 12 reference 한 모델만. Galaxy/Pixel 등은 라이브러리 일반화 후
- **자동 CAE 메쉬 생성** — STEP 출력까지만. ANSYS 메쉬는 기존 [[mesh]] 파이프라인으로
- **공식 OEM 데이터 사용** — 비공개. iFixit + GrabCAD reverse-engineered + 케이스 메이커 DXF 사용

---

## 2. 아키텍처

### 2.1 컴포넌트 다이어그램

```
┌─────────────────────────────────────────────────┐
│  Frontend (가벼운 HTML + Three.js + vanilla JS) │
│  - 3D preview (glTF)                            │
│  - LLM 채팅 패널                                │
│  - Parameter sliders                            │
│  - Stage 토글 체크박스 (Stage 0..N)             │
└────────────────┬────────────────────────────────┘
                 │ HTTP (REST) + WebSocket
                 ▼
┌─────────────────────────────────────────────────┐
│  Backend (FastAPI, Python 3.11+)                │
│  - /generate           파라미터 → 형상 생성     │
│  - /chat               LLM 대화 + Tool Use      │
│  - /preview.gltf       최신 형상 glTF 응답      │
│  - /export/step        STEP 다운로드            │
└────────────────┬────────────────────────────────┘
                 │
        ┌────────┴────────┐
        ▼                 ▼
┌──────────────┐   ┌─────────────────────────┐
│ build123d    │   │ Claude API (anthropic)  │
│ Generator    │   │ - Tool Use (function    │
│ - Stages     │   │   calling)              │
│ - Patterns   │   │ - Conversation context  │
└──────────────┘   └─────────────────────────┘
```

### 2.2 디렉토리 구조 (계획)

```
tools/phone-designer/
├── pyproject.toml              # 의존성 (build123d, fastapi, anthropic, ...)
├── README.md
├── .env.example                # ANTHROPIC_API_KEY 등
├── src/
│   ├── generator/
│   │   ├── __init__.py
│   │   ├── parameters.py       # PhoneParameters (dataclass)
│   │   ├── stages/             # Progressive refinement stages
│   │   │   ├── __init__.py
│   │   │   ├── base.py         # IStage interface
│   │   │   ├── s00_bbox.py     # Stage 0: Bounding box
│   │   │   ├── s01_corner_r.py
│   │   │   ├── s02_edge_chamfer.py
│   │   │   ├── s03_side_profile.py
│   │   │   ├── s04_display_pocket.py
│   │   │   ├── s05_camera_bump.py
│   │   │   ├── s06_camera_lens_holes.py
│   │   │   ├── s07_port_cutout.py
│   │   │   ├── s08_speaker_grille.py
│   │   │   ├── s09_side_buttons.py
│   │   │   ├── s10_antenna_lines.py
│   │   │   ├── s11_mic_sensor_holes.py
│   │   │   └── s12_final_fillet.py
│   │   ├── patterns/           # 재사용 패턴 라이브러리
│   │   │   ├── __init__.py
│   │   │   ├── base/
│   │   │   │   ├── rounded_slab.py
│   │   │   │   ├── stepped_pocket.py
│   │   │   │   └── plateau.py
│   │   │   ├── openings/
│   │   │   │   ├── usbc.py
│   │   │   │   ├── lightning.py
│   │   │   │   ├── speaker_grille.py
│   │   │   │   └── mic_hole.py
│   │   │   ├── camera/
│   │   │   │   ├── lens_hole.py
│   │   │   │   └── camera_island.py
│   │   │   └── buttons/
│   │   │       ├── recess.py
│   │   │       └── side_switch.py
│   │   └── pipeline.py         # Stage 실행 orchestrator
│   ├── server/
│   │   ├── __init__.py
│   │   ├── main.py             # FastAPI app
│   │   ├── routes.py
│   │   ├── llm_tools.py        # Claude Tool 정의
│   │   └── session.py          # 대화 컨텍스트 + 형상 상태
│   ├── ui/                     # Frontend 정적 파일
│   │   ├── index.html
│   │   ├── app.js              # Three.js viewer + 채팅
│   │   └── style.css
│   └── export/
│       ├── step.py             # STEP export wrapper
│       └── gltf.py             # glTF export wrapper
├── presets/
│   ├── iphone12.yaml           # iPhone 12 reference dimensions
│   ├── iphone15_pro.yaml       # 추후
│   └── README.md               # preset 형식 설명
├── tests/
│   ├── test_stages.py          # 각 stage 단위 테스트
│   ├── test_patterns.py        # 패턴 단위 테스트
│   └── test_pipeline.py        # E2E
└── reference/                  # gitignore — 외부 CAD reference
    └── iphone12_teardown.gltf  # Sketchfab 다운로드 (개인 사용)
```

### 2.3 데이터 흐름

1. 사용자가 UI 에서 *"카메라 부분 0.5mm 더 튀어나오게"* 입력
2. Frontend → `POST /chat { message, session_id }`
3. Backend → Claude API 호출 (Tool definitions 동봉)
4. Claude → `set_camera_bump_height(height_mm=2.0)` tool call 응답
5. Backend → `PhoneParameters` 업데이트 + generator 재실행
6. Backend → glTF 응답 + LLM 의 자연어 응답 같이 반환
7. Frontend → Three.js viewer 갱신 + 채팅에 응답 표시

---

## 3. Progressive Refinement Stages

각 stage 는 **input Body → output Body** 의 순수 함수. 모두 [[src/generator/stages/base.py#IStage]] interface 구현.

### Stage 사양

| 번호 | 이름 | 목적 | 핵심 build123d 호출 |
|---|---|---|---|
| **S00** | Bounding Box | 외형 직사각 prism 생성 | `Box(L, W, T)` |
| **S01** | Corner Rounding | 모서리 R (Z축 방향 4개 edge) | `fillet(edges by Axis.Z)` |
| **S02** | Edge Chamfer | 윗면/아랫면 chamfer | `chamfer(top/bottom edges)` |
| **S03** | Side Profile | 측면 곡률 (선택) | `loft([section A, section B])` |
| **S04** | Display Pocket | 앞면 step-down (관통 X) | `Sketch on top face + extrude SUBTRACT` |
| **S05** | Camera Bump | 뒷면 plateau 돌출 | `Sketch on bottom face + extrude ADD` |
| **S06** | Camera Lens Holes | 렌즈 1~4개 원형 hole | `Hole` 또는 `Circle + extrude SUBTRACT` |
| **S07** | Port Cutout | USB-C / Lightning 측면 슬롯 | `SlotOverall + extrude SUBTRACT` |
| **S08** | Speaker Grille | 측면 hole array | `GridLocations + Circle + SUBTRACT` |
| **S09** | Side Buttons | 음량/전원 버튼 recess | `RectangleRounded on side + SUBTRACT` |
| **S10** | Antenna Lines | 금속 frame 절연 슬릿 | thin `Polyline + extrude SUBTRACT` |
| **S11** | Mic / Sensor Holes | 마이크 / 근접센서 핀홀 | `Hole` array |
| **S12** | Final Fillet | 모든 미마감 edge 일괄 fillet | `fillet(remaining sharp edges)` |

### Stage 구조 인터페이스 (계획)

```python
# src/generator/stages/base.py
from abc import ABC, abstractmethod
from build123d import Part
from ..parameters import PhoneParameters

class IStage(ABC):
    @property
    @abstractmethod
    def name(self) -> str: ...

    @property
    @abstractmethod
    def order(self) -> int: ...

    @abstractmethod
    def is_enabled(self, params: PhoneParameters) -> bool: ...

    @abstractmethod
    def apply(self, body: Part, params: PhoneParameters) -> Part: ...
```

### Pipeline orchestrator (계획)

```python
# src/generator/pipeline.py
def generate(params: PhoneParameters, stop_at_stage: int | None = None) -> Part:
    stages = sorted(STAGE_REGISTRY, key=lambda s: s.order)
    body = stages[0].apply(None, params)  # S00 returns initial bbox
    for stage in stages[1:]:
        if stop_at_stage is not None and stage.order > stop_at_stage:
            break
        if not stage.is_enabled(params):
            continue
        body = stage.apply(body, params)
    return body
```

---

## 4. Pattern 라이브러리

Stage 가 호출하는 **재사용 가능한 building block**. 한 패턴 = Python 함수 1개.

### 4.1 패턴 함수 시그니처 표준

```python
def <pattern_name>(
    body: Part,           # 입력 body
    *,                    # 이하 모두 keyword-only
    <semantic params>,    # 의미 있는 파라미터 (mm 단위)
    position: tuple = (0, 0, 0),
) -> Part:
    """
    한 줄 설명.

    Parameters
    ----------
    <param>: <type>
        <설명>

    Returns
    -------
    Part
        패턴 적용 후의 body
    """
    ...
```

### 4.2 초기 패턴 카탈로그 (Phase B–D 에서 점진 구현)

| 카테고리 | 패턴 | 매개변수 | 사용 stage |
|---|---|---|---|
| **base** | `rounded_slab` | length, width, height, corner_r | S00–S01 |
| | `stepped_pocket` | face, sketch_shape, depth, offset | S04 |
| | `plateau` | face, sketch_shape, height | S05 |
| **openings** | `usbc_opening` | position, depth | S07 |
| | `lightning_opening` | position, depth | S07 |
| | `speaker_grille` | position, hole_dia, count, spacing | S08 |
| | `mic_hole` | position, diameter | S11 |
| **camera** | `lens_hole` | position, diameter, depth | S06 |
| | `camera_island` | position, size, height, lens_layout | S05–S06 통합 |
| **buttons** | `side_button_recess` | side, position_y, length, depth | S09 |
| | `mute_switch_slot` | position_y | S09 |
| **antenna** | `antenna_line` | path, width, depth | S10 |

### 4.3 새 패턴 추가 절차

1. `patterns/<category>/<name>.py` 파일 생성
2. 함수 한 개 작성 (signature 표준 따름)
3. `tests/test_patterns.py` 에 단위 테스트 추가
4. (선택) `llm_tools.py` 에 Claude tool 로 노출

→ **추가 작업 약 30분 / 패턴**. LLM 도 자동 생성 가능 (Phase F 참조).

---

## 5. LLM 통합 설계

### 5.1 Tool 정의 패턴

`src/server/llm_tools.py` 에 모든 Claude tool 등록. 두 종류:

**(a) 파라미터 변경 tool** — 단순 값 set
```python
{
    "name": "set_dimensions",
    "description": "Set the phone outer dimensions in mm.",
    "input_schema": {
        "type": "object",
        "properties": {
            "length_mm": {"type": "number", "minimum": 100, "maximum": 200},
            "width_mm": {"type": "number", "minimum": 50, "maximum": 100},
            "thickness_mm": {"type": "number", "minimum": 5, "maximum": 12},
        },
        "required": []  # 일부만 변경 가능
    }
}
```

**(b) 패턴 적용 tool** — 새 feature 추가
```python
{
    "name": "add_camera_lens",
    "description": "Add a circular camera lens hole at given position.",
    "input_schema": {
        "type": "object",
        "properties": {
            "position_x_mm": {"type": "number"},
            "position_y_mm": {"type": "number"},
            "diameter_mm": {"type": "number", "minimum": 1, "maximum": 20}
        },
        "required": ["position_x_mm", "position_y_mm", "diameter_mm"]
    }
}
```

### 5.2 안전 규칙

- LLM 은 **정의된 tool 만 호출 가능** — `exec()` / 임의 코드 생성 불가
- 모든 파라미터에 **bound 검증** (min/max) — schema 레벨 + Python validator 이중
- **변경 transaction 형** — 변경 적용 실패 시 이전 상태로 자동 rollback
- 매 호출마다 **before/after preview** 비교 가능

### 5.3 대화 컨텍스트 관리

- Session 단위로 `PhoneParameters` 보존
- 대화 history (마지막 N=10 턴) 를 Claude API 에 보냄
- "방금 한 거 되돌려" 같은 요청을 위해 **parameter snapshot stack** 유지

---

## 6. Reference 데이터 (iPhone 12 기준)

### 6.1 Reference 소스

| 항목 | 출처 | 정확도 |
|---|---|---|
| 외형 (L × W × T, 코너 R) | Apple spec sheet | ±0.05mm |
| 디스플레이 영역 | Apple developer (bezel 가이드) | ±0.1mm |
| 카메라 모듈 위치/크기 | [iPhone 12 Teardown - Sketchfab](https://sketchfab.com/3d-models/iphone-12-teardown-708eaa5d195544918e5f70b69eedcdfa) (CC BY 4.0) | ±0.5mm |
| 버튼 / 슬롯 위치 | 케이스 메이커 DXF (공개 시) | ±0.2mm |
| 안테나 라인 위치 | iFixit teardown 사진 | ±1mm |

### 6.2 Preset 형식 (계획)

```yaml
# presets/iphone12.yaml
model: "iPhone 12"
manufacturer: "Apple"
year: 2020
reference_sources:
  - "Apple spec sheet (apple.com/iphone-12/specs)"
  - "Sketchfab teardown by Peter_D (CC BY 4.0)"

dimensions:
  length_mm: 146.7
  width_mm: 71.5
  thickness_mm: 7.4
  outer_corner_radius_mm: 10.0

stages:
  s01_corner_rounding:
    enabled: true
    radius_mm: 10.0
  s02_edge_chamfer:
    enabled: true
    chamfer_mm: 0.5
  s04_display_pocket:
    enabled: true
    bezel_top_mm: 5.0
    bezel_side_mm: 2.5
    corner_r_mm: 8.0
    depth_mm: 0.4
  s05_camera_bump:
    enabled: true
    position: [16, 56]  # [x_mm, y_mm] from center
    size: [33, 33]
    corner_r_mm: 8
    height_mm: 1.5
  s06_camera_lens_holes:
    enabled: true
    lenses:
      - position: [-6, 6]
        diameter_mm: 9.0
      - position: [6, -6]
        diameter_mm: 9.0
  # ... 나머지
```

---

## 7. Phase 별 계획

각 Phase 끝에 동작하는 산출물 1개씩 나오도록 구성.

### Phase A — 환경 셋업 + 기준 측정 (1일)

**작업**:
- [ ] `tools/phone-designer/` 디렉토리 생성
- [ ] `pyproject.toml` + 의존성 (build123d, fastapi, anthropic, uvicorn, pyyaml)
- [ ] `python -m venv` + install 확인
- [ ] iPhone 12 Teardown glTF 다운로드 → `reference/iphone12_teardown.gltf` (gitignore)
- [ ] Reference 측정 → `presets/iphone12.yaml` 1차 작성 (S00–S02 항목만)

**산출물**: `iphone12.yaml` (S00–S02 만 채워진 상태)

**검증**: `python -c "import build123d; print(build123d.__version__)"` 동작

---

### Phase B — Stage 0–2 구현 + STEP/glTF export (2일)

**작업**:
- [ ] `parameters.py` — PhoneParameters dataclass
- [ ] `stages/base.py` — IStage interface
- [ ] `stages/s00_bbox.py` — Bounding Box
- [ ] `stages/s01_corner_r.py` — Corner Rounding (Z-axis edges)
- [ ] `stages/s02_edge_chamfer.py` — Top/bottom edge chamfer
- [ ] `pipeline.py` — orchestrator
- [ ] `export/step.py`, `export/gltf.py` — 변환 wrapper
- [ ] CLI 스크립트 — `python -m generator iphone12.yaml --stage 2 --out phone.step`

**산출물**: CLI 로 *"iPhone 12 의 모서리 둥근 chamfered slab"* STEP/glTF 생성

**검증**:
- [ ] 생성된 STEP 을 SpaceClaim 에서 열어 외형 확인
- [ ] Reference iphone12 teardown glTF 와 외형 size/proportion 일치 확인
- [ ] Stage 0/1/2 각각 따로 stop → 시각적으로 점진적 refinement 확인

---

### Phase C — Stage 4–7 (디스플레이 + 카메라 + 포트) (1주)

**작업**:
- [ ] `patterns/base/stepped_pocket.py`
- [ ] `patterns/base/plateau.py`
- [ ] `patterns/openings/usbc.py` / `lightning.py`
- [ ] `patterns/camera/lens_hole.py`
- [ ] `patterns/camera/camera_island.py`
- [ ] `stages/s04_display_pocket.py`
- [ ] `stages/s05_camera_bump.py`
- [ ] `stages/s06_camera_lens_holes.py`
- [ ] `stages/s07_port_cutout.py`
- [ ] preset iphone12.yaml 의 S04–S07 항목 채움
- [ ] 단위 테스트 (`tests/test_patterns.py`, `tests/test_stages.py`)

**산출물**: 디스플레이 step-down + 카메라 bump + USB-C 슬롯이 적용된 iPhone 12 형상

**검증**:
- [ ] glTF 결과를 Three.js 또는 SpaceClaim 에서 시각 확인
- [ ] Teardown reference glTF 와 카메라 위치/크기 비교 (±1mm 이내)
- [ ] 각 stage 단위 테스트 통과

---

### Phase D — Stage 8–12 + 마감 (1주)

**작업**:
- [ ] `patterns/openings/speaker_grille.py`
- [ ] `patterns/openings/mic_hole.py`
- [ ] `patterns/buttons/recess.py`
- [ ] `patterns/buttons/side_switch.py`
- [ ] `patterns/antenna/antenna_line.py`
- [ ] `stages/s08–s12` 구현
- [ ] preset iphone12.yaml 전체 항목 채움
- [ ] Stage 토글 기능 (각 stage 끄기/켜기) CLI 테스트

**산출물**: 완성도 있는 iPhone 12 Front Metal 모델 (STEP + glTF)

**검증**:
- [ ] 전체 stage 활성 시 reference 와 동등 수준 외형
- [ ] 각 stage 토글했을 때 해당 feature 만 적용/제거되는지

---

### Phase E — FastAPI 백엔드 + Three.js 프론트엔드 (1주)

**작업**:
- [ ] `server/main.py` — FastAPI app + CORS
- [ ] `/generate` 엔드포인트 — params → glTF
- [ ] `/preview.gltf` — 최신 형상 응답
- [ ] `/export/step` — STEP 다운로드
- [ ] `ui/index.html` + `app.js` (Three.js viewer + parameter sliders)
- [ ] Stage 토글 체크박스 UI
- [ ] `python -m server` 실행 → `http://localhost:8000` 접속

**산출물**: 브라우저에서 슬라이더 움직이면 0.5초 내 형상 업데이트

**검증**:
- [ ] iphone12 preset 로드 → 슬라이더로 length 150 → 160 변경 → 즉시 반영
- [ ] STEP 다운로드 → SpaceClaim 에서 열림

---

### Phase F — LLM 통합 (Claude Tool Use) (3–5일)

**작업**:
- [ ] `server/llm_tools.py` — Tool definition (5–10개 시작)
- [ ] `server/session.py` — 대화 컨텍스트 + parameter snapshot stack
- [ ] `/chat` 엔드포인트 — Claude API 호출 + tool execution loop
- [ ] UI 에 채팅 패널 추가
- [ ] `.env` 에 `ANTHROPIC_API_KEY` 설정
- [ ] 안전 검증 (parameter bound, transaction rollback)

**산출물**: *"카메라 부분 더 튀어나오게"* → 1–3초 후 반영된 형상

**검증**:
- [ ] *"두께를 8mm 로 변경"* → S00 thickness 업데이트 + 재생성
- [ ] *"디스플레이 베젤 더 얇게"* → S04 bezel 파라미터 업데이트
- [ ] *"방금 한 거 되돌려"* → snapshot stack 에서 이전 상태 복원
- [ ] 부적절한 값 (예: 두께 100mm) → schema validation 거부

---

### Phase G — 확장성 검증 + 두 번째 폰 (3일)

**작업**:
- [ ] `presets/iphone15_pro.yaml` 또는 `galaxy_s24.yaml` 작성
- [ ] 동일 generator 가 새 preset 으로 다른 폰 형상 생성 확인
- [ ] 패턴 라이브러리에 부족한 패턴 추가 (예: titanium frame chamfer, dynamic island)

**산출물**: 2개 이상 폰 모델 동작

**검증**:
- [ ] 두 preset 모두 reference 와 외형 일치

---

### 총 예상 일정

| Phase | 기간 | 누적 |
|---|---|---|
| A. 환경 + 측정 | 1일 | 1일 |
| B. Stage 0–2 | 2일 | 3일 |
| C. Stage 4–7 | 1주 | 10일 |
| D. Stage 8–12 | 1주 | 17일 |
| E. UI + FastAPI | 1주 | 24일 |
| F. LLM 통합 | 3–5일 | 약 4주 |
| G. 두 번째 폰 | 3일 | 약 4.5주 |

**병행 작업 시 (ANSYS 새 기능 개발과 50/50)**: 약 9주

---

## 8. 마이그레이션 (기존 코드 정리)

### 8.1 폐기 대상

```
[삭제]
Commands/SmartPhone/                         # 10개 Command 파일
Models/SmartPhone/                           # 9개 Model 파일
Services/SmartPhone/                         # 4개 Service 파일
UI/Dialogs/AddFeaturesDialog.cs
UI/Dialogs/AntennaBreakDialog.cs
UI/Dialogs/BaseFrameDialog.cs
UI/Dialogs/BoltBossDialog.cs
UI/Dialogs/CutoutDialog.cs
UI/Dialogs/ExportPackageDialog.cs
UI/Dialogs/FinalFinishDialog.cs
UI/Dialogs/FrameDesignerDialog.cs
UI/Dialogs/InjectionPartDialog.cs
UI/Dialogs/ShearPanelDialog.cs

[원복]
AddIn.cs                                     # Smart Phone 등록 제거
MXDigitalTwinModeller.csproj                 # Smart Phone 파일 references 제거
Core/UI/IconHelper.cs                        # Smart Phone 아이콘 reference 제거
Resources/Icons/SmartPhone*.png              # 아이콘 (선택)

[삭제]
lat.md/spaceclaim/smart-phone.md             # lat.md 의 Smart Phone 도메인 파일
lat.md/lat.md                                # 인덱스에서 smart-phone 링크 제거
lat.md/spaceclaim-addin.md                   # Smart Phone 탭 언급 제거
```

### 8.2 보존 결정

- 현재 모두 untracked (커밋 안됨) → **단순 삭제 + main.cs 원복**
- Git history 에도 안 남으므로 archive 필요 없음
- **결정**: 마로 삭제. 별도 branch 보존 불필요.

### 8.3 마이그레이션 순서

1. **Phase A 시작 전에** SpaceClaim 측 Smart Phone 코드 + 다이얼로그 일괄 삭제
2. `AddIn.cs` 의 `using SmartPhone` + Smart Phone 탭 등록 부분 제거 → 다시 컴파일되는지 확인
3. `MXDigitalTwinModeller.csproj` 의 file reference 제거
4. lat.md/ 의 smart-phone 관련 항목 정리
5. 커밋 ("Remove unfinished SmartPhone code in favor of separate phone-designer project")

---

## 9. 검증 / 테스트 전략

### 9.1 단위 테스트 (각 Phase 에 포함)

- 각 stage: input body + params → expected output (volume, bounding box 검증)
- 각 pattern: 단순 input body 에 적용 → 결과의 face count, edge count 검증
- export: 생성된 STEP 이 valid (`occt-import-validate` 등으로 검증)

### 9.2 시각 검증

- 매 Phase 끝에 reference glTF 와 생성 glTF 를 같은 viewer 에 띄워 비교
- Three.js viewer 에 "reference / generated / diff" 토글
- Stage 별 progressive snapshot 저장 (디버깅용)

### 9.3 LLM 시나리오 테스트 (Phase F)

미리 정의된 자연어 명령 10개로 회귀 테스트:
1. *"두께를 1mm 줄여줘"*
2. *"카메라 모듈을 좀 더 작게"*
3. *"USB-C 를 Lightning 으로 바꿔줘"*
4. *"스피커 그릴 hole 을 12개로"*
5. *"전체 길이를 5mm 키워줘"*
6. *"디스플레이 베젤을 더 얇게 (1.5mm)"*
7. *"카메라 plateau 높이를 0.5mm 더"*
8. *"코너 R 을 8mm 로"*
9. *"방금 변경 되돌려"*
10. *"전체 리셋"*

각 명령이 → 적절한 tool call + 결과 검증.

---

## 10. 의존성 (`pyproject.toml` 초안)

```toml
[project]
name = "phone-designer"
version = "0.1.0"
requires-python = ">=3.11"

dependencies = [
    "build123d >=0.9.0,<1.0",     # CAD kernel
    "fastapi >=0.115",              # web framework
    "uvicorn[standard] >=0.30",     # ASGI server
    "anthropic >=0.40",             # Claude API SDK
    "pyyaml >=6.0",                 # preset 파일
    "pydantic >=2.0",               # 데이터 검증
]

[project.optional-dependencies]
dev = [
    "pytest >=8",
    "ruff",
    "mypy",
]
```

---

## 11. 열린 질문 (Phase 진행 중 결정)

| 항목 | 옵션 | 결정 시점 |
|---|---|---|
| **Frontend 프레임워크** | (a) vanilla HTML + Three.js, (b) React + shadcn | Phase E 시작 시 |
| **LLM 통합 수준** | (a) Tool Use 만, (b) Agent loop (자율 검증), (c) MCP server | Phase F 시작 시 |
| **CAE 연결 시점** | (a) 디자인 확정 후 manual, (b) phase H 로 자동화 | Phase G 후 |
| **다중 폰 reference 데이터** | 각 폰 preset YAML 직접 작성 vs LLM 자동 생성 | Phase G |
| **저장소 위치** | 현 repo 의 `tools/phone-designer/` vs 별도 repo | Phase A 시작 시 |

---

## 12. 다음 단계

**현재 작업**: 본 계획서 검토 + Phase A 시작 승인 대기

**Phase A 시작 전 결정 필요**:
1. 저장소 위치 (현 repo subfolder vs 별도 repo)
2. 기존 SmartPhone 코드 삭제 시점 (Phase A 전에 먼저 삭제할지)
3. iPhone 12 vs 다른 폰 (iPhone 15 Pro 같은 최신) 으로 reference 시작할지

승인되면 **Phase A: 환경 셋업 + 기준 측정** 부터 시작.

---

## 참고 자료

- [build123d official docs](https://build123d.readthedocs.io)
- [build123d GitHub (gumyr/build123d)](https://github.com/gumyr/build123d)
- [OpenCascade documentation](https://dev.opencascade.org/doc/refman/html/)
- [Anthropic Tool Use guide](https://docs.anthropic.com/en/docs/agents-and-tools/tool-use/overview)
- [Sketchfab: iPhone 12 Teardown (Peter_D, CC BY 4.0)](https://sketchfab.com/3d-models/iphone-12-teardown-708eaa5d195544918e5f70b69eedcdfa)
- [GrabCAD iPhone tag](https://grabcad.com/library/tag/iphone)
- 기존 프로젝트 IMPLEMENTATION_PLAN.md (DMA 시편 계획서 참조)
- 본 프로젝트 [[lat.md/spaceclaim/smart-phone.md]] (현재 코드 분석 — 폐기 예정)
