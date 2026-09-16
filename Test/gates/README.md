# Test/gates

사람이 ANSYS GUI 안에서 돌리는 **런타임 GATE 스크립트**의 추적 사본. 원본은 `.claude/skills/ansys-api-catalog/verification/`
(gitignore 대상) 에 있고, 여기 사본이 저장소에 남는 기준본이다 — 수정하면 두 곳을 같이 맞출 것.

| 스크립트 | 실행 위치 | 판정 줄 | 대상 |
|---|---|---|---|
| `verify_energy_api.py` | Mechanical → File → Scripting → Open Script → Run (solve 된 Modal/Harmonic/Transient 모델) | `ENERGY_GATE: ...` + solve 폴더 `energy_gate_result.json` | Vibration Energy 전제 API (EnergyContribution / Result.Total / AddFigure / SetNumber) — [[lat.md/mechanical/postprocess.md]] |

도중에 끊기면 이름이 `GATE_` 로 시작하는 결과 객체를 수동으로 지운다 (스크립트 헤더 참고).
