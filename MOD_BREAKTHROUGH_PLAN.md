# MOD_BREAKTHROUGH_PLAN — 수정 자동화 일반화 및 잔여 실패 돌파 계획

> 작성: Cycle 35+ 종합 (strategies / verification / infra / api-capabilities 4개 분석 + critique 검증 결과 통합)
> 원칙: **모든 신규 API·가설은 smoke test 통과 전까지 "미검증"으로 취급한다** (OffsetBodyFaces silent no-op 전례).

---

## 0. ✅ GATE 실험 결과 (2026-06-10 실측 — probe_gate_experiments.py / probe_blast_radius.py)

| Gate | 판정 | 실측 증거 |
|------|------|----------|
| **W1-1 ContainsPoint MaterialSign oracle** | **GO** | CP-1~5 전부 PASS: solid interior=T, far outside=F, hole bore=F, hole face toward-axis=F(air), away-axis=T(material). 단위 body (20×20×10 박스+5mm 관통홀)에서 기하학적 정답 100% 일치 |
| **Body.Copy + faceMap** | **GO** | COPY-1~3 PASS: `Copy()`/`Copy(out fm, out em)` 동작, faceMap 7/7 해석, hole face 매핑 성공 |
| **Copy 위 작업 (attach-first)** | **GO (조건부)** | COPY-4b: `DesignBody.Create(part, copy)` 후 OffsetFaces 동작 + **원본 격리 확인** (orig R 불변). 단 attach 없는 detached copy op = **SC 프로세스 crash ×3 재현** |
| **Volume 3단 게이트** | **GO + 실전 검증** | VOL-1 이 unit body 에서 **진짜 sign-inversion 을 라이브로 검출** (positive offset 이 hole 을 축소 — dv=+7.07e-8 vs analytic=-8.64e-8). 게이트가 설계 목적대로 동작 |
| **E-2 IsDeleted 메커니즘** | **부분 반증** | native body 성공 offset 후: DesignFace.IsDeleted=False, Shape.IsDeleted=False, **cached handle 로 연속 offset 도 성공** (E2-1/E2-2 PASS). "deleted state" 는 native-success 에서 발생 안함 — STEP import + 실패 상태 특이적 |
| **E-3 ReplaceFaceGeometry** | **GO** | `FaceGeometry(Surface, bool reversed)` ctor 발견 (reflection). `Cylinder.Create(frame, r)` static 존재. **hole R 1.6→2.1mm 직접 교체 성공** (E3-3 PASS) — OffsetFaces/Boolean 우회하는 3번째 프리미티브 검증 완료 |
| **E-1 Blast Radius** | **NO-GO (same-process recovery)** | linkrods 0.05mm offset 강제실패 ("Operation failed" 깨끗한 예외) 직후 **T8 `DesignBody.Create(pristine copy)` 가 HANG** (300s timeout). 이전 재현 포함 4회 독립 확인: 실패 후 프로세스는 회수 불능 — **evict-on-exception 만 유효**, same-process Copy-Probe-Commit 복구는 기각 |

### 신규 발견 kernel landmines (전부 process-kill 급)
1. **on-surface point 에 `ContainsPoint`** → SC 프로세스 crash (probe ×1 재현; eps 오프셋 필수, eps=0.2×R 검증됨)
2. **detached Body (DesignBody 미부착) 에 OffsetFaces** → SC 프로세스 crash (×3 재현). **모든 copy 작업은 `DesignBody.Create` 선행 필수**
3. **과대 offset** (1mm wall 에 0.45mm 단면 shrink) → 예외가 아니라 프로세스 crash. 0.05mm 급은 깨끗한 예외
4. **kernel 실패 직후의 `DesignBody.Create`** → hang. 실패 후엔 어떤 doc-mutation 도 금지

### ✅ Phase 1 실행 결과 (2026-06-11 atomic 60-cell 실측)

| 지표 | Phase 1 전 | Phase 1 후 |
|------|-----------|-----------|
| **Real verified** | 26~39 (fake 혼재) | **42/45 = 93.3%** |
| **Fake OK** | 14~20 | **0** |
| Real FAIL | 5~16 | **3** |

구현된 항목: W1-2 ContainsPoint oracle (ChangeFilletRadius concavity 입력 교체) + W1-3 SIGN_INVERTED gate + W1-4 flipSign fresh-import retry + W2-6 SurfaceSwap (Hole 1순위, ReplaceFaceGeometry) + W2-4 axis tier ladder (SurfaceSwap·Boolean 양쪽 cached-axis fallback).

검증된 회복: nist_ctc_01 Fillet 5→6 정확, boxy Fillet 8→9.6 정확 (SIGN_INVERTED→retry), Ventilator Hole 51.86→62.23 정확 (cached-axis Boolean). 잔여 3: nist_ftc_07 Fillet (tangent merge → W2-5), linkrods Wall 1mm + 624ZZ Wall (kernel rejection → W2-3 ScaleNormalized).

### ✅ Phase 2 실행 결과 (2026-06-11 atomic 실측)

구현: W2-3 ScaleNormalized proactive (Wall, min(|δ|,T)<2mm 예측 발동) + WallSurfaceSwap (scaled-class Strategy 0, ReplaceFaceGeometry 평면 교체) + ChangeFilletRadius forceScaled + orchestrator kernel-fail retry (5번째 spec 필드).

| 결과 | 상세 |
|------|------|
| **최종 42/45 = 93.3% verified, Fake 0, regression 0** | 신규 scaled+swap 경로로 라우팅된 기존 통과 wall 5개 (SampleModel1·nist_stc_06·as1-oc·as1-md·Ventilator) 전부 유지 |
| 잔여 3 = **진짜 kernel boundary** | 각각 OffsetFaces(전 scale+subdivision)·Boolean·SurfaceSwap·RoundEdges·pre-heal 전부 거부: ① linkrods Wall (곡면 전요 rod, swap re-trim crash) ② 624ZZ Wall (bearing race, swap "Operation failed") ③ nist_ftc_07 Fillet (tangent merge, 1000×도 거부) |
| 판명 사실 | linkrods/624ZZ 류 거부는 **tolerance 가 아니라 구조적** (1000× 동일 실패) — ScaleNormalized 가설의 적용 한계 확인. W2-5 (delete+re-round) 는 DeleteFaces API 부재로 보류 |

### ✅ Phase 3 W3-1 실행 결과 (2026-06-11, matrix 3 runs × 108 cells)

18-primitive × 6-model matrix (T1 smoke slice) 구축·실행 — **15개 미검증 primitive 의 사상 첫 실측**.

| Run | VERIFIED | 개선 동인 |
|------|----------|----------|
| 1 (baseline) | 33/89 = 37% | — |
| 2 | 38 | driver: top-face anchor (bbox top-center mid-air 제거) + mirror 평면 + SIGN retry 이식 → Fillet 4/4 |
| 3 | **41 (46%)** | **axis-aware 리팩토링**: AddHole(+axisDir) / Move·RemoveHole fill 이 hole.Axis 방향 + DepthMm=0→bore 전장 — "Distance cannot be zero" 클래스 전멸, RemoveHole 2→5 V |

Change 3종 (hardened): 3 runs 연속 15/15 V — Phase 1-2 체계의 재현성 입증.

**최종 V51 = 57.3% (run 7)** — 진화 V33→38→41→44→48→**51**.

| Run | 핵심 수정 | Δ |
|------|----------|----|
| 1 | baseline (15 미검증 primitive 첫 실측) | 33 |
| 2 | driver anchor + SIGN retry 이식 | +5 |
| 3 | hole 계열 axis-aware (AddHole+axisDir, fill 체인) | +3 |
| 4 | boss 계열 axis-aware (Height 추정 + cap 탐색) + RotateHole oracle | +3 |
| 5/6 | Add-surface 배치 (face anchor + 법선 axis + embed + featMm 상한) | +4 |
| 7 | **안전 배치점 finder** (ContainsPoint straddle 검증으로 면 선택 + inset 제거) | +3 |

**Run 7 breakthrough — 안전 배치점 finder:** 면을 "최대 면적" 이 아니라 "ContainsPoint 로 한쪽 solid·한쪽 air 가 확인되는 첫 면" 으로 선택, 0.2mm 절대 probe. 이전 inset(`minDim*0.15`)이 대형 part 에서 anchor 를 면 밖으로 밀어내던 버그 제거 → **AddBoss 1→4** (nist/samplemodel2/boxy STEP 전부, 법선이 run5 와 반대 = 이전 outward 오판이 근본 원인이었음).

**결함無 primitive 7종**: ChangeHole/Wall/Fillet (Phase1-2, 7 runs 연속) + AddRib.

**잔여 gap (전부 placement/extraction):**
1. **Mirror bounds F4** — primitive 의 bounds 사전체크가 hole 위치·STEP axis 품질에 민감
2. **Boss Move/Rotate 재인식 F4** — op 성공하나 extractor 가 재추가 boss 미인식 (boss 추출 criteria)
3. **11752 fill "General Failure" F3** — 곡면 flange Unite kernel 한계 (확정)
4. AddBoss F2 (as1-oc assembly void + AddHole 등 ~6) — 다중-body assembly 배치
5. AddChamfer 'top' +Z 필터 (2), ChangeBossHeight cap 탐색 (2)

**구조적 결론:** 단일-feature 치수 변경 (Change*) 93%+ 견고. Add* 배치는 ContainsPoint straddle 검증으로 STEP 단일-body 까지 해결 — 잔여는 assembly(다중 body) + extraction(boss 재인식) + kernel(곡면 fill) 의 3개 클래스로 명확히 분리됨. 일반화 아키텍처는 단일-body 까지 완성.

### ✅ Phase 3 W3-1 후속 (2026-06-14, matrix run 8→11) — **V67 = 77% (attempted 87중)**

진화 **V59→63→67**. 3종 generalization breakthrough:

| Run | 핵심 수정 | V |
|------|----------|----|
| 8 (재기준) | run 7 이후 placement/oracle hardening 누적 | 59 |
| 10 | **AddHolePattern drillAxis**: pattern hole 을 +Z 가 아닌 **면 법선**으로 천공 (C# 14번째 param `drillAxis` → AddHole 전파). run 9 가 구버전 드라이버+구 DLL 불일치로 무효였음을 action-문자열 대조로 발견·교정 | 63 |
| 11 | **(a) AddSlit/AddPocket/AddRib 면-법선 일반화** (`PrismBasis` 헬퍼 — normalAxis 기본 +Z 면 legacy 수학적 동일 → 무회귀; nist 비-Z 면 cutter 빗나감 클래스 전멸) **(b) AddHolePattern oracle 완화** (≥2 hole 이 예측 좌표 + count 증가 = 패턴 동작 증명) **(c) MoveHole count-delta oracle** (old-slot population 감소 검사로 multi-hole 모델 false-IC 제거) | **67** |

**무회귀 핵심:** `PrismBasis(n, orient)` 가 n=+Z 일 때 기존 `Cross(zAxis, long)` 코드와 동일한 basis 를 산출 (longAxis×shortAxis = n 증명) → upright 면 5V 유지하며 tilted 면만 추가 정복. AddHole/AddBoss 의 +Z 제거(run 3-7)와 동일 원칙을 cutter 계열로 확장.

per-primitive (run 11): **AddSlit 5V, AddPocket 5V, AddRib 5V, AddHolePattern 5V** (전부 11752 제외 만점), MoveHole 2V(sm2/nist vacate 검증)/2IC/2F.

**잔여 cells = 3 구조적 클래스 (확정):**
1. **11752 곡면 flange ~12 cells** — Add*/Move*/Remove*/Rotate* 전반의 fill/cut "General Failure". planar-face·bbox-through-extent 가정이 곡면 sheet 에 부적용. **kernel 구조 한계** (T3 weekly sweep 영역, 단일-body 평면계 아키텍처 범위 밖)
2. **MoveHole 잔여 2 cells** (SampleModel1 IC `.scdoc` 추출 quirk, as1-oc F non-manifold assembly) — heuristic/assembly
3. **Mirror 비대칭 2 + samplemodel2 대형 boss 3** (ChangeBossDiameter 540→649, RotateBoss large-arc) — heuristic 한계

### ✅ MoveHole fill-margin fix (2026-06-14, focused 6-model 검증) — **V67→68**

**근본원인:** MoveHole.fill 이 정확히 bore 반경(`rM`)의 filler cylinder 를 Unite → 동축·동반경 표면이 **coincident (zero-thickness) cylindrical face** 를 남기고 kernel 이 이를 보존, extractor 가 old hole 을 계속 인식 ("pop N->N"). 수정: `rFillM = rM + max(rM·2%, 1e-5)` — 여분 annulus 는 주변 solid 내부라 Unite 부작용 0, bore wall 을 union 내부로 흡수. **boxy IC→V** ("vacated 2->1") 검증. SampleModel1 은 `.scdoc` 추출 quirk 로 잔존(별도 클래스).

**운영 교훈:** **빌드 직후 SC cold-launch 가 >240s 변동** — per-cell force-kill 이 cold cache 를 반복 무효화하면 모든 cell 이 타임아웃(빈 결과). smoke 격리 테스트(add-in import 없이 89s 통과)로 "코드 회귀 아님 = 환경 cold-start" 판별. **focused 재실행은 PerCellTimeoutSec≥300 + warm 선행** 필요.

---

## 8. Phase 4 — 구조적 한계 돌파 (복잡 실모델 = 폰 프론트 메탈 전제조건)

남은 3개 미검증 클래스는 polish 가 아니라 **복잡 실모델의 지배적 특성**(곡면·어셈블리·대변형)이다. 폰 프론트 메탈 진입의 전제조건이므로 전부 돌파 대상.

### ✅ Keystone gate 결과 (2026-06-14, gate_localfill_probe.py on 11752)

`Body.Shape.IntersectCurve(axisLine)` **곡면에서 robust 확인** — `List[IntPoint[SurfaceEvaluation, CurveEvaluation]]`, 각 IntPoint 에 `.Point` / `.EvaluationA`(hit face) / `.EvaluationB`(curve param). 11752 H1 의 bore 진입/이탈을 t=48.4/59.4mm 로 정확 반환.

**결정적 발견 (2단계 심층):**
1. 11752 H1 추출 `PositionMm.y=0` 인데 IntersectCurve 는 axis 교차를 y∈[-48,-59] 로 반환 (앵커 48mm off).
2. **더 중요 — 그 2 교차점 사이가 solid** (midpoint ContainsPoint=TRUE). 진짜 bore 라면 void(FALSE) 여야 함. 즉 **추출 axis 가 실제 bore 를 빗나가 solid 벽을 관통** → `FeatureExtractor` 가 곡면부 hole 을 근본 오추출. fill 메커니즘만 고쳐선 불충분.

→ **진짜 keystone = live-cylinder relocation**: 추출 hole 근방에서 live body 의 실제 `Cylinder` surface face 를 재탐색해 표면 geometry 에서 참 axis/radius/axial-extent 를 읽고, 그것을 모든 hole 연산(fill/move/remove/rotate)의 입력으로 사용. (driver `find_live_cylinder_near` 검증 패턴을 연산 입력으로 승격.) IntersectCurve 는 face-extent 측정의 보조 도구. fill-Unite 는 `DesignBody.Create` 래퍼 필수 (raw `Body.Copy()` ops = landmine).

### ✅ 근원적 검증 토대 (2026-06-15, gate_kernel_truth.py — 사용자 지시 "근원적 검증 먼저")

**문제:** 모든 기존 oracle 이 FeatureExtractor 재추출에 의존 — 곡면부에서 추출이 깨지므로(11752 H1 앵커 48mm off) oracle 도 거짓 상속.
**해결:** live face surface 를 **커널에서 직접** 읽은 canonical fingerprint (`kernel_truth.py`):
- Cylinder = foot-of-perpendicular 앵커 + `Cylinder.Radius`(런타임 존재 확인) + face axial-extent
- Plane = 법선(sign-canonical) + signed offset + area; + Body.Volume + bbox, 전부 양자화
- `diff(fp0,fp1)` = face-sig multiset delta + dvol — **추출기 무관 L1 oracle primitive**

**mutation test 통과** (nist ChangeHoleDiameter 25→30): radii delta 가 정확히 `12.5mm −1 / 15.0mm +1`, 그 외 0 → **stable+sensitive+specific = KERNEL_TRUTH_VALIDATED**. 플랜 규칙 "oracle 은 mutation test 통과 전 신뢰 금지" 충족. 이후 모든 작업의 판정 기준.

### ✅ W4-1 부분 검증 + W4-1b 분기 (2026-06-15, gate_w41.py on 11752)

C# `TryRelocateHoleCylinder` 구현 — live `Cylinder` 면에서 참 axis/radius/extent 읽기. gate 결과 **`relocated=true, r=8.573mm`** → 곡면 11752 의 참 bore 정위 **성공**(추출 앵커 48mm off 우회, keystone 가설 확정). 단:
1. relocation 후에도 fill Unite 가 **"General Failure"** — 위치 아닌 **dirty 곡면 STEP 의 Boolean 강건성** 문제.
2. 그 실패가 **body 를 poison** — post-failure scale-trick 재시도가 "object is deleted" (blast-radius landmine 재확인). **in-process 복구 불가.**
3. relocation 단독은 planar multi-hole(boxy)을 **V→IC 회귀** + 11752 은 어차피 Boolean 실패 → **순이득 0**.

→ **`RELOCATE_FILL=false` 로 gate off (V68 보존)**. 활성화는 **W4-1b** (아래)와 동시에만.

### ✅✅ 결정적 발견 (2026-06-15, gate_w41.py pin-probe) — 11752 hole 실패 = 추출 오분류

11752 의 hole-연산 실패(RemoveHole/MoveHole/RotateHole "General Failure")의 진짜 원인을 **4중 가설 체계적 제거**로 규명:
1. ~~위치~~ (relocation 이 참 cylinder r=8.573mm 정위 — `relocated=true`)
2. ~~tolerance~~ (1000× scale-trick 으로도 General Failure)
3. ~~cap tangency~~ (돌출 50% 로도 General Failure)
4. ~~poison~~ (forceScale fresh body 로도)

→ **진짜 원인: FeatureExtractor 가 solid PIN 을 hole 로 오분류.** kernel-truth pin-probe (relocated cylinder 축 중심 `ContainsPoint`) = **`axis_centre_solid=true`** 확정. RemoveHole 이 이미-solid 에 filler Unite → coincident → "General Failure" + **body poison**. **어떤 fill 기법으로도 못 고치는 garbage-in — 오직 커널 직접 검증이 드러냄** ("근원적 검증 먼저"의 정당성).

**✅ kernel-truth PIN 가드** (RemoveHole/MoveHole): fill 전 relocated cylinder 축 중심 `ContainsPoint`=solid → pin 판정, **Unite 미시도**(poison 방지) + 정직 거부. focused 6모델 검증: 진짜 hole 5/5 VERIFIED(오거부 0), 11752 만 정직 FAILED("solid pin, not a hole"). poison-유발 General Failure → 의미적으로 옳은 clean 실패로 전환.

**남은 진짜 표적 = 분류 정확성(W4-2c):** 11752 의 H1 을 boss/pin 으로 재분류하면 hole-op 거부가 **boss-op 정상처리**로 바뀜. kernel-truth material-side(`ContainsPoint` 축중심 solid/void) oracle 이 hole↔boss 판별의 ground truth — FeatureExtractor 의 CylinderRole 분류를 이걸로 교정.

| ID | 작업 | 근본원인 (실측) | 검증 |
|----|------|----------------|------|
| **W4-1b** | **forceScale 재import fill** (구현·빌드 완료, RemoveHole) — 단 11752 은 pin 이라 fill 무의미; forceScale 자체(center-scale 1000×)는 동작 확인. 진짜 hole 곡면부 검증은 corpus 확장 후 | (위 4중 제거로 11752 은 fill 문제 아님이 판명) | 곡면 진짜-hole 모델에서 Remove/Move VERIFIED (corpus 확장 필요) |
| **W4-2c** | **kernel-truth hole↔boss 재분류** — `ContainsPoint` 축중심으로 CylinderRole 교정, 오분류 pin→boss 전환 | FeatureExtractor pin→hole 오분류 (11752 H1 실측) | 11752 H1 이 boss 로 분류 → ChangeBossDiameter 등 정상 | — RemoveHole/MoveHole 에 forceScale 파라미터: import 직후 body 1000× 선(先)scale → relocation+fill → scale-down. harness 가 General-Failure cell 을 forceScale 로 재실행(Fillet 패턴 기존재). poison 회피(fresh body) + tolerance 회복 | Boolean General Failure 가 body poison → in-process 복구 불가, proactive scale 만 유효 | 11752 Remove/Move/RotateHole VERIFIED (kernel-truth) + 6모델 무회귀 |
|----|------|----------------|------|
| **W4-1** | **live-cylinder relocation** (정정된 keystone) — 추출 hole 근방 live `Cylinder` face 재탐색 → 참 axis/radius/extent 를 연산 입력으로. fill/move/remove/rotate 의 bbox-투영을 이 참 extent + IntersectCurve 측정으로 교체. DesignBody.Create 래핑 필수 | 추출 axis 가 곡면 bore 빗나감 (solid 벽 관통, anchor 48mm off) → fill 이 엉뚱한 위치 → "General Failure" (11752 Move/Remove/Rotate Hole 4셀) | 11752 Move/Remove/Rotate Hole VERIFIED + 기존 6모델 무회귀 (focused, warm 300s) |
| **W4-2** | **local-feature 스케일링** — featMm 를 part-global bbox 가 아닌 **배치 면 local extent** + inward solid-depth 프로브(IntersectCurve 로 두께 측정)로 산정, engage 보장 | featMm 가 116mm part 에서 1.53mm 붕괴 → cut 안 닿음 (11752 Add* volOk=False, dV≈0) | 11752 AddBoss/AddHole/Pocket/Slit/Rib VERIFIED |
| **W4-3** | **곡면 boss own-face axis-extent** — cap 을 평면-수직 탐색 대신 boss 소속 face 들의 axis 투영 span 으로 측정 | 평면 cap 가정이 곡면 boss top 에 부적용 (11752 ChangeBossHeight "cap not found") | ChangeBossHeight VERIFIED on 11752 |
| **W4-4** | **body-targeted assembly 연산** — multi-body part 에서 대상 feature 소속 body 선택, 신규 hole 위치가 그 body 내부 fully-inside(ContainsPoint) 확인 후에만 drill | assembly body 경계 hole → "non-manifold" (as1-oc MoveHole) | as1-oc Move/Add 클래스 VERIFIED |
| **W4-5** | **arbitrary-plane mirror + 대변형 Boolean** — mirror bounds 사전체크를 ContainsPoint 검증으로 대체(이미 driver 일부 적용), 대형 boss 치수변경을 OffsetFaces 대신 신규-cylinder Subtract/Unite 로 | bounds 사전체크 민감(Mirror 비대칭 2) / OffsetFaces 대변형 폭발(samplemodel2 540→649) | Mirror/대형boss VERIFIED |

### ✅✅✅ W4-2c 완료 + corpus 확장 실측 (2026-06-15, 9-model V100/IC12/F17/NA37 = 77.5% attempted)

`CylinderRoleClassifier.ProbeSolidCore` — hole↔boss 의 `IsReversed` 를 kernel-truth ContainsPoint(축중심 solid/void)로 교정. clean import 동일(무회귀), 11752 flipped 만 교정.
- **11752:** H1(pin) → **boss B1 재분류** (n_holes 0, n_bosses 1). poison-General-Failure hole-op → 정직 N_A + boss-op 획득(ChangeBossDiameter/MoveBoss/AddHolePattern V).
- **무회귀:** 원본 6모델 ChangeHoleDiameter 5/5 V, RemoveHole 5/5 V.
- **신규 모델:** face_recog_sample 14V/1IC, as1_pe_203(assembly) 13V (Change/Move/Remove/RotateHole+Mirror+Wall 전부 V), 624ZZ_bearing(곡면) 8V/3IC/5F.
- **결정적 교차검증:** 624ZZ 곡면 bore 의 Move/Remove/RotateHole = **V** → hole 머신 곡면 정상작동. **11752 실패 = 곡면 kernel 한계 아닌 분류 오류** 독립 입증.
- **잔여 = W4-2 곡면 placement** (624ZZ/11752 Add* FAILED: featMm 붕괴 + 곡면 안전배치) + 곡면 대형 ChangeBoss/Wall.

### ✅ W4-2 곡면 placement 완료 (2026-06-16, 9-model V105/IC10/F14 = 81.4% attempted)

드라이버(mod_matrix_test.py) 곡면 배치 수정 — **리빌드 불필요**:
1. **featMm 붕괴 해소** — `minDim/8`(부품 최소치수/8; 베어링 5mm→0.63mm = cut 미engage/미인식) → `minDim/3` + 인식 floor 1.2mm (단 `topFaceMm/6` 으로 상한해 소형 면 overflow 방지).
2. **annular 면 다점 샘플링** — `safe_placement` 가 bbox 중심(ring 의 중앙 bore void)만 보던 것을 8방향×2반경 in-plane 오프셋 샘플로 확장 → ring 재질 위 anchor 확보, 큰 ring 면 채택.

**결과:** 624ZZ(곡면 bearing) AddBoss/AddHole/AddPocket/AddRib/AddSlit **전부 0V→VERIFIED**. Add* primitive 9모델 중 8V(11752 제외 만점). 8 working 모델 무회귀(SampleModel1 등 Add* 정상). 세션 진화 V59→67→100→**105**.

**잔여 = 11752 단독** (얇은 곡면 flange, side-face(0,-1,0) 배치에서 AddBoss/Hole/Rib 미engage). 단일 pathological 모델 — featMm/placement 가 아닌 flange-특이 through-extent. 별도.

### ✅ W4-3 부분 (2026-06-16, ChangeBossHeight 방향 수정)

OffsetFaces offset 부호를 cap 의 **outward normal·axis dot 투영**으로 결정 (`offset = dHm·dot(outward,axis)`): cap 이 +axis 면 grow, −axis 면도 grow, 옆면(⊥)이면 ≈0 **안전 no-op**. ContainsPoint straddle 로 outward 판정. **face_recog ChangeBossHeight IC→VERIFIED** (80→96, 이전 64 부호반전 해소), 무회귀(잘못된 방향 대신 no-op). V105→106.
**잔여 (정직한 재분석):** FindBossCapFace 는 이미 dot>0.95(축∥normal) 우선 — cap-선택 문제 아니었음. relocation 기반 index-free 측정으로 교체(robustness 개선, 유지)했으나 samplemodel2 는 여전히 "measured 50" = **OffsetFaces 가 그 cap 에서 no-op**(kernel). 624ZZ 곡면 OffsetFaces "Operation failed", 11752 pin-boss. → **OffsetFaces 커널 한계 영역** (non-goals: OffsetFaces 깊은 진단 루프 기각). ChangeBossHeight 의 일반화 가능 부분(방향)은 face_recog 로 완료, 잔여는 OffsetFaces 대체전략(신규-cylinder Subtract/Unite for height) 필요 — W4-5 와 통합.

### ⚠️ MirrorFeature 비대칭 — 조사 후 baseline 유지 (2026-06-17)

twin-finder(`axis_hits_solid`) 강화 3회 시도 — (1) 연속 solid run≥1.5d, (2) offset sweep 확장, (3) 수직 disk 검사 — **전부 net-zero 이하**: SampleModel1 F→V 되면 samplemodel2 V→F, disk 검사는 SampleModel1 마저 F. offset-sweep + ContainsPoint heuristic 이 어떤 twin 을 "first"로 고르냐에 fragile, 실패를 옮길 뿐. **원본 복원**. 비대칭 part 의 mirror twin 은 본질적으로 hard — 진짜 해법은 part 의 실제 대칭면 탐지(symmetry detection)이지 offset 휴리스틱이 아님. 별도 연구 항목.

### ✅ W4-5 boss OffsetFaces-대체 Boolean + Fix1 MoveHole oracle (2026-06-18, V105→**V107**)

**아키텍처 (poison 회피의 정석):** OffsetFaces 실패는 SC 프로세스를 poison → in-process 복구 불가. 따라서 **OffsetFaces-first + 실패 시 하니스가 fresh body 재실행 + useBoolean** (ChangeFilletRadius forceScale 와 동일 패턴, run_mod_matrix.ps1).
- `TryChangeBossHeightBoolean` / `TryChangeBossDiameterBoolean` — live boss cylinder relocate → 자유 CAP(축 너머 air, ContainsPoint) → Unite(grow)/Subtract(shrink, height) 또는 동축 grow(diameter). gate(gate_w45.py) 검증: 624ZZ boss 4.6→7.6 성장.
- height target 정확도: dHm 을 caller 의 `EstimateCylFaceExtentMm`(624ZZ est 0.2, 실제 4.6 → over-grow 9.4) 대신 **relocated extent**(tHi−tLo)로 계산 → 624ZZ ChangeBossHeight 정확히 5.0.
- **Boolean 은 primary 가 아님**: 11752 thin/curved boss 에서 Unite General-Failure → OffsetFaces-first 가 11752 보존(useBoolean 미사용).

**결과:** **624ZZ(곡면 bearing) ChangeBossDiameter F→V + ChangeBossHeight F→V** (둘 다 OffsetFaces "Operation failed"/no-op 하던 곳). 11752 ChangeBossDiameter 보존(OffsetFaces). 곡면 bearing 이 BossD/BossH/MoveHole/AddHole 전부 VERIFIED.

**Fix1 — MoveHole oracle = kernel-truth:** re-extract `g2` count(extractor phantom, nist 4×D25 에서 인접 bore 오검출) → **live body `find_live_cylinder_near` + `ContainsPoint(old)` solid**(old bore 충전 확인)으로 교체. vacate = `oldSolid OR (no live bore at old)`. nist/624ZZ V, SampleModel1/sm2/boxy 는 old 미충전으로 IC(정직).

**잔여 (genuine):** samplemodel2 ChangeBossDiameter(732mm huge boss, Boolean General-Failure/oracle), samplemodel2 ChangeBossHeight(degenerate 양끝-solid), 11752 ChangeBossHeight(pin-boss), MoveHole old-미충전 3(.scdoc fill), as1-oc(assembly W4-4), 624ZZ Wall(kernel poison), 11752 Add*(flange), Mirror 비대칭(symmetry detection).

---

## 9. Phase 5 — 잔여 genuine-limit 돌파 (2026-06-18 계획, 폰 프론트 메탈 목표)

### ✅✅ 대표-corpus 측정 (2026-06-18, 12모델 V143/IC10/F20/NA43 = **82.7% verified**) — cold-review #1 반증

cold-review 가 "84%는 비대표(단순 test part) corpus 위 과장"이라 의심 → **곡면-rich 실제 part 3종 추가**(Ventilator 293면/0.90곡면, RC_Buggy 220면/0.74곡면, F623ZZ 베어링)로 검증.
**결과: 9모델 ~84% → 12모델 82.7% (−1.5p 만 하락).** Ventilator 13V/3F(76%), RC_Buggy 12V/4F(71%), F623ZZ 12V/0F(92%). **일반화 가능 돌파(kernel-truth·분류·곡면 placement·boss Boolean)가 곡면-rich 실제 part 에 실제로 robust** — 측정 입증. "단일-모델 취약" 우려 부분 반증.
**새 실패 클래스 (곡면 복잡 모델이 드러냄):** (a) **AddChamfer 곡면 미engage**(Ventilator/RC_Buggy, dV≈0) NEW, (b) **MoveHole "old 미충전" 5모델 재발**(rOld=live cylinder = 실제 fill 실패, 최다 잔여), (c) ChangeFilletRadius 방향오류(RC_Buggy), (d) ChangeBoss* embedded/degenerate(Ventilator), (e) MoveBoss/RotateBoss off-support.
→ Phase 5 우선순위 갱신: **MoveHole old-fill(5셀, 최다) + AddChamfer 곡면(2셀) 이 W5-2 보다 높은 ROI.**

### ✅ MoveHole OR-oracle (2026-06-18, V143→**V144**) + AddChamfer 곡면 진단

**MoveHole oracle 의 진짜 난이도 규명 (3 oracle 실측):** 단일 신호 전부 불결 — (1) `ContainsPoint(old)` = off-bore 추출 anchor 에서 거짓-IC(.scdoc/복잡부), (2) count-conservation = fill 의 filler-wall cylinder 가 count 오염(nist 4→6, 단 old 는 실제 충전 solid=True). **해법 = OR 결합** `vacated = oldSolid OR (cyl count 보존)`: 각 part 에서 깨끗한 신호 선택, 실패는 rNew=None 게이트로 거짓양성 불가. **boxy IC→V(count 3→3), nist V 유지(oldSolid)** = 무회귀 +1. 잔여 IC(SampleModel1/sm2/Ventilator/RC_Buggy)는 두 신호 다 불결(cyl 1→9 등 복잡부 face-split artifact) — genuine oracle 난이도.

**AddChamfer 곡면 진단:** `RoundEdges(FixedRadiusRound)` "all" edge — Ventilator/RC_Buggy 는 candidates>0 이나 **NURBS-인접 edge 에서 RoundEdges no-op**(status OK, faces+0). 624ZZ(단순곡면) 작동. → **RoundEdges-on-NURBS 한계**. tractable(adjacent-face-type 으로 chamferable edge 필터링)이나 별도 사이클.

### ✅✅ W4-4 멀티바디 어셈블리 — 본질 능력 구축 (2026-06-18, 사용자 지정 "본질적 문제")

**keystone 검증 (gate_w44_extract.py on as1-oc):** `RealModelPipeline.ExtractAllBodies=true` → **5 body 전체(nut/rod/bolt/l-bracket/plate)에서 22홀+6보스 surface** (현재 top-graph=nut 2홀만 → **feature 가시성 10×**). `FeatureGraph.Shells[i] ↔ FindAllDesignBodies(doc)[i]` 병렬 순서 = body-targeting 토대.

**body-targeted modify 검증 (gate_w44_modify.py, 비-first body=plate):**
- ✅ **AddHole 작동** — plate(body[4])에 4mm 관통홀 dV=−251mm³(=π·2²·20 정확). 비-active body 도 완전 modify 가능.
- plate H1 의 CylinderFaceIndex(1)=Cylinder r=5mm 정확 (face-index 정합성 OK, 멀티바디 추출 신뢰).
- ⚠️ **ChangeHoleDiameter no-op** (success=true, measured 10 미변경) — face 는 찾으나 ReplaceFaceGeometry/OffsetFaces 가 **비-active body 에서 무적용**. 멀티바디 문제 아닌 SC-API active-body 의존 (별도 진단).

**구현:** `RealModelPipelineResult.AllBodies` 노출(Shells 와 병렬) → 호출자가 (Shells[i], AllBodies[i]) 페어로 body-targeted 연산.

### 🔬 W4-4 근본 경계 규명 (2026-06-18, 심층) — 비-active body 의 SC 연산 비대칭

심층 진단으로 **fundamental SC 동작**을 확정: 어셈블리의 **비-active body** 에서
- ✅ **Subtract 작동** (AddHole plate dV=−251mm³ 확인; enlarge-via-Subtract 도 원리상 가능)
- ❌ **ReplaceFaceGeometry / OffsetFaces / Unite 전부 no-op** (성공 반환하나 무변경). ChangeHoleDiameter 의 SurfaceSwap→Boolean(fill=Unite) 둘 다 무효 = measured 미변경.

원인: SC 가 in-place(ReplaceFaceGeometry)·additive(Unite)·offset 연산에 **body 활성화(edit context)** 요구 — `InteractionContext.ActivePart` 는 property 이나 `Activate()` 메서드 부재 → **headless API 로 활성화 불가**. Subtract 만 비-active body 에서 동작.

**의미:** **단일-body(폰 메탈 프레임)는 active 라 전 연산 작동** — 목표 핵심엔 무영향. 멀티-body 어셈블리의 **비-first body 기존-feature 수정**만 이 경계에 막힘. W4-4 능력 = 전체-body 추출 + targeting + **subtractive 연산(AddHole/enlarge)**. 비-active in-place/additive 는 genuine SC 한계(활성화 우회: body 를 단일-part 로 Copy→수정→복귀, Copy landmine 위험).

**적용 robustness:** `SwapChangedRadius` — ChangeHoleDiameter 의 SurfaceSwap 후 radius 실변경을 kernel-truth 검증, no-op 이면 cascade 진행(active body 무회귀, 비-active 는 Boolean 도 막혀 honest fail).


남은 실패는 전부 "새 전략 필요" 클래스. 폰 프론트 메탈(얇은 곡면 벽 + 멀티바디 어셈블리)을 직접 겨냥해 **value×tractability** 로 우선순위화. 검증된 방법론: **각 항목 gate 실험 → kernel-truth oracle → 구현 → 무회귀**.

| ID | 표적 | 근본 (실측) | 접근 (gate-first) | kernel-truth 검증 | 우선 |
|----|------|------------|------------------|------------------|------|
| **W5-1** | **IntersectCurve local through-extent** — AddHole/Boss/Slit/Rib 의 cutter extent | bbox-투영이 얇은 곡면 flange 에서 air 로 overshoot + cutter 위치 빗나감 (11752 Add* "volOk=False"/dV≈0) | `Body.IntersectCurve`(배치점+법선 line)로 **실제 진입/이탈** → cutter 를 local solid 깊이로 sizing+위치. W4-1 검증된 도구 재사용 | dV sign + face-count delta; cutter 가 실제 재질 제거 | **1 (최고)** — 얇은 곡면 벽 = 폰 메탈 핵심, 도구 검증완료 |
| **W5-2** | **multi-body assembly targeting (W4-4)** | AddHole at new pos 가 body 경계 횡단 → "non-manifold" (as1-oc MoveHole) | 대상 feature 소속 body 식별(face-set), 신규 pos 가 그 body 내부 fully-inside(ContainsPoint) 확인 후에만 drill. body 별 Subtract | 소속 body volume↑/↓; non-manifold 회피 | **2** — 폰=멀티바디, 별개 capability |

> **✅✅ W5-2 벽두께 가드 — full matrix 무회귀 확정 (2026-06-22, 216/216, snapshot `matrix_summary_W52_wallguard_20260622.md`):** `ModificationService.AddHole` 에 thin-wall TANGENCY 가드(`MIN_WALL_M=0.5mm`, axis-수직 in-plane bbox 극단에서 center 클램프, 불가시 skip→IC). **핵심: `clampToWall` 파라미터 opt-in** — MoveHole 의 drill 만 true(이동 타깃은 근사라 클램프 허용), Mirror/직접 AddHole 은 false(정확 위치 보존). 이유: 첫 무차별-적용 빌드가 **samplemodel2 Mirror V→F 회귀**(MirrorFeature(hole)→AddHole 호출의 twin 위치가 클램프됨, ModificationService.cs:2436) → opt-in 으로 격리, Mirror 복구 확인. **최종: VERIFIED 138→141 (79.8%→82.0%), 진짜 회귀 0.** 개선 5: as1-oc MoveHole(FAILED→V, W5-2 벽-가드), samplemodel2/624ZZ/Ventilator MoveBoss(IC→V, 전 사이클 오라클), Ventilator AddChamfer(F→IC). "회귀" 1건(RC_Buggy AddChamfer IC→F)은 **dV=3.4e-21 부동소수점 노이즈**(Ventilator AddChamfer F→IC 와 짝, net0, 곡면 no-op genuine limit, 벽-가드 무관). 워크플로우 진단(tangency NOT body-boundary)이 실측 입증됨. **함정 2개 실측 학습:** (1) 공유함수 가드는 모든 호출자 영향→opt-in 필수, (2) 작업 중 **D: 디스크 100%-full** 로 JSON 못써 false-FAILED 유발(MSI 392M+로그 정리로 해소). 잔여: AddHoleOnBody pure-subtractive overload(폰-어셈블리 forward-looking, 현 red 0회복)는 미구현 — 별도.
>
> **⚠️ W5-2 가설 정정 (2026-06-22, 3-agent 진단 워크플로우):** 위 "body 경계 횡단" 가설은 **as1-oc 에 대해 틀림**. 코드+geometry 검증: as1-oc MoveHole 셀은 `ExtractAllBodies=False` 로 **단일 'nut' body(20×15×3)** 만 추출, cutter 가 두 번째 body 를 건드린 적 없음. 진짜 원인 = **얇은 벽 tangency** — nut 20mm 폭, D10(r5) bore 를 +5mm 옮기면(`shift=min(20,5,20)=5`) bore 가 x=20 벽에 **정확히 접해 zero-thickness sliver** → non-manifold(ModificationService.cs:1680, fill 성공 후 subtract 실패). 내 bbox-rim 클램프가 안 통한 이유도 이것(벽두께 ≠ rim). **추가 발견:** MoveHole-on-assembly 는 W4-4 한계로 근본 불가 — Unite-fill 이 non-active body 에서 NO-OP → double-hole+Success=true 사일런트 손상. **재정의된 W5-2:** (1) AddHole 에 **same-body 벽두께 가드**(MIN_WALL 클램프 or skip→IC) = as1-oc 실제 fix (+1셀, multi-body 불필요), (2) `AddHoleOnBody` pure-subtractive overload = 진짜 어셈블리 capability(현 red 0회복, 폰-어셈블리 forward-looking). MoveHole-on-assembly 는 active-body 가드로 IC 처리. **gate: probe_w52_wallguard.py 로 tangency+클램프+커널수용 MIN_WALL 실측 후 C# 구현.**
| **W5-3** | **symmetry-plane Mirror** | offset-heuristic twin 이 비대칭 part 에서 off-body | 실제 대칭면 탐지(planar face-pair 또는 bbox-center plane 에서 body 대칭 잔차=0 확인) → 그 면으로 mirror | twin이 solid 위(ContainsPoint) + 원본 보존 | **3** — Mirror 3셀, fragile heuristic 대체 |
| **W5-4** | **.scdoc MoveHole old-fill 진단** | `ContainsPoint(old)`=False (old bore 중심 미충전) — fill 미달 or old anchor off-bore | 진단: C# fill 이 .scdoc 에서 old bore 를 안 채우는지 vs old anchor 가 bore 밖인지. relocate-old-then-fill | oldSolid True 전환 | **4** — 3셀, 빠른 진단 |
| — | samplemodel2 732mm boss | degenerate embedded cylinder(양끝 solid) | Boolean grow 가 embedded 에 무의미 | — | **닫음** (genuine) |
| — | 624ZZ Wall | OffsetFaces poison cascade (Phase2 확정) | — | — | **닫음** (kernel) |

### ⚠️ W5-1 gate 결과 (2026-06-18, gate_w51.py) — keystone 도구 검증, 11752 Boolean-병리로 DEFER

`Body.IntersectCurve`(배치점 법선 line)가 **11752 flange 두께를 정확 측정** (entry 0, exit 11.017mm; bbox-투영이면 part Y-span 으로 overshoot). cutter_mid_solid=true(재질 안 정확 배치). **BUT Subtract 후 부피 2587→2725 (+138 = cutter 부피만큼 증가)** = 재질 안의 cutter 를 Subtract 해도 ADD 됨 → **11752 의 Boolean-Subtract 가 반전 병리**. 이게 matrix 11752 AddHole "volOk=False"(dV<0 아님)의 진짜 원인 — **sizing 문제 아닌 dirty STEP Boolean 병리**.
→ **W5-1 keystone 도구(IntersectCurve local-extent)는 sound, 단 유일 표적 11752 가 Boolean 레벨 병리라 현 corpus 검증 불가. DEFER**: clean thin-curved 모델을 corpus 에 추가(W5-2 와 함께)한 뒤 W5-1 sizing 구현·검증. 11752 은 classification(pin)·Boolean(반전) 다중 병리 = 영구 genuine limit.

### 🧹 기술부채 정리 (cold-review #6, 2026-06-18 인벤토리)

ModificationService.cs = **4420줄 단일 파일**, 19 public + 19 helper, 34 재시도-플래그 분기, 38 Boolean op 지점. 정량화된 중복:
- **axis→frame boilerplate 10곳** (`ArbitraryPerpendicular`→`Cross`→`Frame.Create`→`Plane.Create`) + **CircleProfile cutter 6곳** — 동일 패턴.
- relocation(`TryRelocateHoleCylinder`) 10곳 재사용 = 좋은 패턴(유지).

**정리안 (다음 사이클, rebuild-검증 동반):**
1. `MakeCoaxialCylinder(axisDir, base, radius, length)` 헬퍼 → 6 cutter 사이트 통합 (~60줄→6 호출).
2. `UniteFillerCylinder` 를 `BooleanCoaxialCylinder(..., bool subtract)` 로 일반화 → DesignBody.Create+Unite/Subtract+Delete 중복 6곳 통합.
3. partial class 분할: `.Holes.cs/.Bosses.cs/.Walls.cs/.Fillets.cs/.AddPrimitives.cs/.Helpers.cs`.
4. 재시도 플래그 통일: `forceScale`(Wall/Fillet) + `useBoolean`(Boss) → 단일 "strategy escalation" 파라미터 (harness retry 일원화).
⚠️ 작동 코드 refactor 는 rebuild+matrix 무회귀 검증 필수 — 측정 corpus 확정 후 실행.

### 🔬 Oracle 엄밀성 audit (cold-review #4, 2026-06-18) — hardening scope 확정

driver verify() 별 의존 분류:
- **kernel-truth only (엄밀, mutation-test 급):** ChangeHole/Fillet/Wall/BossHeight, MoveHole.
- **FeatureExtractor only (취약 — 재추출 phantom 위험):** **ChangeBossDiameter / MirrorFeature / AddHolePattern**. 이 3종 VERIFIED 는 증거력 약함. samplemodel2 ChangeBossDiameter "no boss at D=732" 가 정확히 이 추출기-의존 oracle 탓.
- 혼합(KT+FE): AddBoss/Hole, MoveBoss, RemoveHole, RotateBoss, AddSlit.

**hardening (다음 사이클):** FE-only 3종을 kernel-truth 로 전환 — ChangeBossDiameter→`cyl_radii(diff)` 반경버킷 이동, MirrorFeature→`find_live_cylinder_near(twin)`+원본보존, AddHolePattern→live cylinder count delta. 이러면 모든 VERIFIED 가 추출기-무관 증거력 획득.

### ✅✅ oracle-rigor hardening 완료 (2026-06-18) — 전 oracle kernel-truth 화

FE-only 3종을 `find_live_cylinder_near`/`count_live_cylinders`(live body) 로 전환:
- **ChangeBossDiameter** → live cyl of D≈new at boss axis. 작동 4셀(11752/face_recog/624ZZ/RC_Buggy) V(추출기-무관 증거), **samplemodel2 는 kernel-truth 가 old R=305 잔존 노출 → genuine failure 확정**(false-negative 가설 기각).
- **MirrorFeature** → live twin bore + 원본보존. nist/as1_pe/F623ZZ V, SampleModel1/boxy/624ZZ F(비대칭). baseline 동일.
- **AddHolePattern** → live bores + cyl count delta. SampleModel1/nist/boxy/as1_pe/Ventilator V, 624ZZ/F623ZZ IC. baseline 동일.

**결과: verdict 무변경(무회귀)이나 전 VERIFIED 가 추출기-무관 증거 획득.** 냉정한 리뷰 #4(검증 신뢰성)의 핵심은 숫자 아닌 **trustworthiness** — 이제 12모델 matrix 의 모든 VERIFIED 가 kernel-truth 증거 위에 섰다. crown jewel 이 검증 전반의 기반이 됨.

### ✅✅✅ 권위있는 trustworthy matrix (2026-06-20, 12모델 V145/IC10/F18/NA43 = **83.8%**, 216/216 누락 0)

cold-review 3대 약점 전부 측정 완수한 **결정적·신뢰가능** 측정:
- **#1 corpus 대표성**: 곡면-rich 12모델(Ventilator/RC_Buggy/F623ZZ 포함)에서 **83.8%** — "84% 비대표 과장" 반증.
- **#2 하니스 결정성**: run_mod_matrix.ps1 에 **warm-up pre-pass**(_warm.py 로 OS 캐시·SC 스택 선기동) → **216/216, MISSING 0** (이전 cold-launch ±3 변동 제거).
- **#4 oracle 신뢰성**: 전 oracle kernel-truth → 모든 VERIFIED 가 추출기-무관 증거.

**= 검증 시스템이 rigorous(kernel-truth) + deterministic(warm-up) + 대표 corpus 위에서 trustworthy 83.8% 를 산출.** 숫자가 처음으로 완전히 방어 가능.

### ✅ oracle-rigor 마무리 + AddChamfer 진단 (2026-06-20)

**MoveBoss/RotateBoss/RotateHole oracle 도 kernel-truth 전환** (FE re-extract 루프 제거 → `find_live_cylinder_near` + count/ContainsPoint OR-vacate). 결과: 깨끗한 boss(face_recog/11752/RotateHole 다수) V, **degenerate feature(베어링 race·samplemodel2 embedded boss)는 정직한 IC/F** — extractor 가 false-V 주던 걸 노출. = **전 18 primitive oracle 이 kernel-truth**(cold-review #4 완결). trustworthy > inflated 원칙대로 일부 degenerate V→IC(정직).

**AddChamfer NURBS 필터**: `IsChamferableEdge`(양쪽 면 Plane/Cylinder/Cone, NURBS-인접 skip, catch→keep 무회귀). 작동 5셀 V 유지하나 **Ventilator/RC_Buggy 여전히 F** — 필터로도 안 풀림 = spline-heavy 부품의 RoundEdges 가 analytic edge 에서도 no-op, **더 깊은 RoundEdges-on-그 geometry 한계**(별도). 무회귀라 유지.

### ✅✅✅ V146 권위있는 trustworthy matrix — kernel-truth 전환 실측 확정 (2026-06-21, 216/216 누락 0)

**전체:** VERIFIED=138 / IC=17 / FAILED=18 / N_A=43 → **verified-rate 79.8%** (138/173 verifiable). baseline V145(83.8%) 대비 **V −7 은 회귀가 아니라 false-V 정직화** (snapshot: `matrix_summary_V146_kerneltruth_20260621.md`).

**실행 메모:** 첫 run 이 624ZZ(9번째 모델) AddHole 셀에서 셀당시간 누적증가(74→169s)로 stall(148/216 보존). `run_mod_matrix.ps1 -Resume` 스위치 신설(완료 셀 skip+JSON 보존, PerCellTimeout 240→300s)로 fresh-proc 재시작 → 누수 리셋(셀 35~70s 안정) → 잔여 68셀 완주. **resume 가 stall 복구 표준 절차**.

**3-primitive kernel-truth 전환 per-cell 순효과 (회귀 0건):**
- V→V (진짜성공 유지): 11752/face_recog MoveBoss, 624ZZ/Ventilator RotateBoss, SampleModel1/samplemodel2/nist/boxy/as1_pe/624ZZ RotateHole.
- F→F (진짜실패 유지): RC_Buggy MoveBoss, samplemodel2/11752/RC_Buggy RotateBoss.
- **V→IC (false-V 정직화) 7셀**: samplemodel2/624ZZ/Ventilator MoveBoss, face_recog RotateBoss, as1-oc/Ventilator/RC_Buggy RotateHole — 전부 **"old not vacated"(cyl count↑)** = 원 feature 미삭제로 모델 망가졌는데 extractor 가 V 주던 것.
- **깨끗한 feature V→F 회귀 = 0건.** baseline 의 `boxy RotateHole 8.28mm off=V`·`face_recog RotateBoss 10.38mm off=V` 같은 느슨한 V 를 정확히 교정. → **전환 keep 확정.**

### 🔬 35-bad-cell 심층 root-cause 분석 (2026-06-21, 13-agent 적대검증 워크플로우)

35개 bad 셀 → **9개 root-cause 클러스터**. 그중 A(11)+C(7)+D(5)=23셀(66%)이 대부분. **결정적 발견:**

**⚠️ 클러스터 A(최대, 11셀)는 우리 코드 버그가 아니라 _오라클 비대칭 버그_** — `RotateHole`=`return MoveHole(...)`(ModificationService.cs:2326), `RotateBoss`=`return MoveBoss(...)`(:2285) = **바이트 동일 커널 op** 인데 verdict 가 갈림. 원인: 3개 verify 의 `vacated` 신호 불일치 — MoveHole(537)은 `rO is None` 누락, MoveBoss(569)는 `oldSolid` 누락, Rotate(633)만 3신호 완전체. ⇒ 같은 op·같은 count(SampleModel1 4→5, Ventilator 0→14)인데 Move=IC vs Rotate=V. **즉 위 V146 "old not vacated 7셀 정직화" 중 Move측 IC 4셀은 사실 _잘못된 IC_ 였음 — 우리가 자신을 과소평가.** 적대검증이 원추정 +5→**+4 교정**(Ventilator/RC_Buggy MoveHole 은 Rotate쌍도 IC라 안 풀림), volume-gate 제안은 multi-wall through-cut 에 unsound 라 **기각**.

**ROI 순위 (적대검증된 corrected 수치):**
| # | 작업 | 난이도 | 회복 | →rate |
|---|---|---|---|---|
| 1 | **클러스터 A: Move 오라클을 Rotate 3신호로 통일** (오라클-only, 무위험) | S | +4 | 82.1% |
| 2 | G+I: as1-oc MoveHole shift 클램프 + 624ZZ AddHolePattern OD-chord 배치 | M | +2 | 83.2% |
| 3 | B: MirrorFeature twin 반경-클리어런스 validator (boxy V, 624ZZ/SM1 정직 N_A) | M | +1 | 84.8% |
| 4 | 정직성 클린업: AddBoss/Hole volume-gate(Boolean no-op silent-success 버그), 11752 BossHeight 측정 fix, RC_Buggy fillet 라이브-torus baseline | L | +0.5 | 85.3% |

**정직한 천장 = ~85%, 90%+ 는 물리적으로 불가능.** 나머지 ~17 bad = **진짜 커널/feature-degeneracy 한계**: 클러스터 C(off-support boss·11752 dirty-STEP Boolean no-op), D(NURBS RoundEdges no-op·11752 bent-tube), E+F+H(624ZZ 22-cyl race OffsetFaces reject, flush height=0 boss, torus minor-radius 불변). 기존 retry 가 이미 소진 — 쫓으면 false-positive 오라클 위험만 ↑, V 이득 ~0. **A+G+I+B 까지만 추진, 나머지 document-only.**

### ⚠️ 클러스터 A 실제 구현 결과 — 예측(+4)보다 작은 +2 (2026-06-21, 정직한 후기)

분석의 +4 예측을 실제 구현·실측한 결과 **확실한 이득은 +2뿐**. 솔직한 결말:
- ✅ **MoveBoss 3신호 통일**(`oldSolid=contains_mm` 추가) → **624ZZ/Ventilator VERIFIED (+2 확정)**. 유지.
- ✅ **MoveHole 3신호 통일**(`rO is None` 추가) + `old_bore_filled` axis-multi-sample 헬퍼 → robust 화, **무회귀**(nist/boxy/as1_pe/624ZZ/F623ZZ 전부 V 유지). 유지.
- ❌ **samplemodel2/SampleModel1 MoveHole 은 여전히 IC** — 분석/적대검증/코드에이전트 셋 다 "false-IC, 회복 가능" 주장했으나 **실측은 다름**. verify 내부 디버그로 측정한 진값: `rO=True ddO=0.00`(old axis 위에 d/2 cylinder face 가 정확히 0거리로 잔존) + `contains_mm(old)=False`. probe 가 한 번은 [T×5]solid 한 번은 sc=False 로 **모순** → **ContainsPoint 가 cylinder 경계점에서 비결정적**임을 노출. 어떤 vacate 신호로도 단정 불가 = **진짜 모호 케이스 → 정직한 IC 유지**(V 강제 = false-positive). 포기.
- ❌ **클러스터 G 클램프**(as1-oc shift clamp): 실측 결과 as1-oc non-manifold **못 고침**(원인은 shift 크기 아님) + 다른 셀 무변화 → **no-op 으로 되돌림**. as1-oc MoveHole = genuine AddHole-subtract non-manifold 한계.

**교훈:** 분석/적대검증의 "회복 가능" 추정도 실측 앞에서 틀릴 수 있음. **ContainsPoint 단일점은 oracle ground-truth 부적합**(경계 불안정). trustworthy > inflated 원칙대로 모호 경계셀은 IC 유지. 순효과: **138→140 V (79.8%→80.9%)**, 회귀 0. 클러스터 B/I 는 동일 위험(오라클 무리)이 예상되므로 **재평가 후 신중 추진** 또는 document-only.

### 순서·전략 (Phase 5, 갱신)
1. ~~W5-1~~ → **W5-2 (assembly) 먼저** — 비병리 표적(as1-oc) 존재. body-targeting + corpus 에 multi-body STEP 추가. 그 corpus 가 W5-1 의 clean thin-curved 표적도 제공.
2. W5-1 sizing — clean 표적 확보 후.
2. W5-2 assembly — corpus 에 multi-body STEP 추가(as1-oc 외) + body-targeting. 폰 어셈블리 토대.
3. W5-3 symmetry-detection, W5-4 .scdoc fill 진단.

### (구) Phase 4 순서·전략
1. **W4-1 (keystone) 먼저** — IntersectCurve 헬퍼 `MeasureBoreSegment(body, P, axis)` → (entry, exit) 를 ModificationService 에 추가하고 fill 4곳 교체. 단일 변경으로 곡면 hole 클래스 4셀 + 미래 복잡모델 일반 해결.
2. W4-2/3 는 11752 Add*/BossHeight (곡면 단일-body) 마무리.
3. W4-4 는 assembly (별도 corpus 확장 필요 — as1-oc 외 multi-body STEP 추가).
4. W4-5 는 heuristic 잔여.

### Corpus 확장 (T2 nightly 표적)
현 6모델은 곡면 1(11752)·어셈블리 1(as1-oc)뿐 — 통계적으로 빈약. **추가 staging 대상**: 곡면/revolved 3-5종(폰 메탈 유사 sheet·flange), multi-body assembly 2-3종. `Test/RealCAD/` 의 occt/pythonocc/stepcode 디렉토리에서 후보 스캔 → applicability manifest pre-pass(W3-1) 로 N_A 자동 제외.

### Gate 반영 — Phase 2 수정
- **W2-1 Copy-Probe-Commit**: 원안 기각. 수정안 = **Probe-First**: mod 전에 copy 를 attach 하고 copy 에 먼저 적용 → 성공 시 원본에 적용. 단 copy 에서의 실패도 프로세스를 오염시키므로 (process-global), 이 패턴의 가치는 "원본 보존" 뿐이고 프로세스는 어차피 evict — **per-cell 격리 유지가 정답**
- **W2-2 Session-reuse**: 조건부 GO — 성공 cell 연쇄는 안전 (E2 증거). 정책 = 첫 예외 발생 시 즉시 evict. 단 successful-mod 후 Document.Open NRE 사례 (1차 corpus) 있으므로 K-cap + 매 cell 후 Document.Open canary 필요
- **W2-6 SetFaceGeometry 전략**: GO 확정 — Hole/Wall 의 1순위 전략 후보로 승격 (sign 무관, offset 무관, topology 보존)
- **P1 ContainsPoint oracle**: GO 확정 — ChangeFilletRadius/ChangeHoleDiameter sign 입력 교체 즉시 착수 가능

---

## 1. 현황 요약

**검증된 성공률: 39/45 attempted = 86.7% REAL verified** (before/after 치수 일치 기준, atomic corpus 60 cells, per-cell SC 격리, ~35분)

### 잔여 실패 6 cells

| # | Cell | 증상 | Root Cause |
|---|------|------|-----------|
| 1 | nist_ctc_01 / Fillet | 5.0→6.0 요청, 측정값 4.0 = before − dR (**정확한 부호 반전**), 상태는 OK | concavity sign이 `face.IsReversed` 단독 유도 (CylinderRoleClassifier.cs:70) — STEP translator가 임의로 설정하는 Parasolid sense flag라 재질 방향을 보장하지 않음. ChangeFilletRadius는 자체 측정값(MeasuredAfterMm)으로 Success를 gate하지 않음 (ModificationService.cs:766-793) |
| 2 | boxy / Fillet | 8.0→9.6 요청, 측정 6.4 = before − dR, JSON status "OK" | 동일 (sign inversion). probe+corrective 수정은 폐기됨 — 첫 offset 성공 후 캐시된 Face 핸들이 "object is deleted" (stale reference, ModificationService.cs:692-695) |
| 3 | nist_ftc_07 / Fillet | tangent-neighbor merge로 offset 실패 | OffsetFaces 변형 방식의 본질적 한계 — tangent 흡수가 일어나는 chain은 deform이 아니라 topology 재생성이 필요 |
| 4 | linkrods / Wall (T=1mm) | symmetric step 1/2 실패 → A-only/B-only/Boolean 전부 "The object is deleted" | **kernel poisoning cascade**: native-scale OffsetFaces 실패가 body를 오염시켜 후속 전략 전멸. scale-trick은 Boolean 경로 안에서만, 실패 후에만 발동 — OffsetFaces에는 너무 늦음 |
| 5 | 624ZZ / Wall (T=5mm, δ=0.5mm) | 동일 cascade | 동일 — scale-trick이 reactive(에러 문자열 매칭)라서 poisoning 이벤트 자체가 트리거인 구조적 모순 |
| 6 | Ventilator / Hole (52mm) | Boolean: "live face geometry is not a Cylinder" → OffsetFaces가 body 오염 | STEP import가 hole 벽을 bare `Surface`로 노출 (FaceClassifier.cs:227-261) — Boolean 재구성이 live Cylinder face를 요구하지만 실제 필요한 것은 축(axis) 하나뿐 |

※ linkrods 0.25mm Hole은 현재 scale-trick으로 통과하지만 sub-mm 클래스 (phone front-metal의 0.2-0.5mm rib/fillet) 전체가 동일 위험군.

### 구조적 결함 (cell 단위 실패 너머)

| 결함 | 증거 |
|------|------|
| **검증 3계층 전부 confirmation-biased** — "기대값에 가장 가까운 후보" 선택 | wall global plane-pair scan (centroid 계산은 dead code, overlap 검사 없음, :327-368), hole nearest-R fallback에 suspect flag 미설정 (:592-597), Python any_feature_near (mod_atomic_test.py:133-146). counterbore/치수 패밀리에서 silent no-op이 "verified"로 통과 가능 |
| **18개 primitive 중 15개는 측정 증거 zero** | MeasuredAfterMm 할당은 4곳뿐 (:297, :590, :596, :790). MoveHole/MoveBoss는 TargetCenter조차 미설정 — Success = "예외 안 났음" |
| **판정 권한 3중 분산** | C# OK ↔ Python FAIL_VERIFY ↔ offline FAKE-OK가 한 cell에서 충돌 (boxy). 기대값 factor(×1.2/×0.9)와 tolerance가 3곳에 중복 정의 |
| **harness wall-clock 75%가 SC 재기동 오버헤드** | 실제 kernel 작업 ~208s vs 총 ~1900s. 실제 poisoning cell은 60개 중 7-8개뿐인데 60회 전부 process 격리 |
| **검증 예외는 fail-open** | catch { /* keep OK */ } ×3곳, INCONCLUSIVE가 1급 결과가 아님 — 86.7%는 상한값 |

---

## 2. 일반화 아키텍처 — Strategy Executor

per-feature 하드코딩 체인 (Wall/Hole/Fillet 3개가 각자 진화, Boss는 hardening zero)을 **하나의 generic executor + 선언적 strategy registry**로 대체한다. 단, big-bang 재작성이 아니라 **strangler pattern** — hardening이 전무한 Boss 경로를 pilot으로 시작하고, verifier 결합이 가장 깊은 Wall을 마지막에 이관한다.

### 2.1 Strategy Registry (capability metadata)

```csharp
interface IModStrategy {
    StrategyCaps Caps { get; }
    bool TryApply(ModContext ctx, out string err);
}
// caps는 corpus로 의미가 증명된 것만 — 과설계 금지 (critique 합의)
class StrategyCaps {
    Direction Direction;            // Grow | Shrink | Both  (Boolean hole=Grow-only, Boolean wall=Shrink-only — 현재 주석으로만 존재하는 제약을 데이터화)
    bool PoisonsBodyOnFailure;      // checkpoint 정책 결정
    CrashRisk CrashRisk;            // RoundEdges bulk = High (samplemodel2 SC crash)
    double MinFeatureSizeMm;        // ScaleNormalized decorator 발동 기준
}
```

- Verifier는 cap이 아니라 **strategy entry와 함께 delegate pair로 전달** (wall Boolean의 의도적 검증 다운그레이드 같은 strategy×verifier 상호작용은 비트로 환원 불가 — critique 지적).
- Decorator: `Subdivided(s)`, `ScaleNormalized(s)` — scale factor는 단일 `ModContext.ScaleFactor` 필드로 모든 tolerance/캐시 좌표 소비자가 공유 (bodyScaleFactor 파라미터 threading과 에러 문자열/'삭제' substring 게이트 전부 제거).
- **에러 문자열 포맷은 byte 단위로 보존** — per-strategy 연결 메시지가 지금까지 모든 root cause 추적의 원천이었고 verify_atomic_results.py가 파싱함.

### 2.2 Geometric Pre-flight Oracle (시도 전 위험 분류)

mutation 없는 read-only 질의만으로 시도 전에 경로를 결정:

1. **MaterialSign(face)** — `Body.ContainsPoint` 기반 재질 방향 oracle (§3-P1). face 중점 P에서 곡률 중심 방향 ±eps 두 점을 probe. **양쪽 모두 inside/outside면 degenerate-thin-wall로 flag하고 캐시된 classifier 값으로 fallback — 절대 추측하지 않음.** eps는 상수가 아니라 local feature size 비례 (0.2×R 등).
2. **Feature size gate** — `s = min(|delta|, targetDim) < threshold` 이면 ScaleNormalized 경로 (threshold는 corpus sweep으로 결정, §5 Phase 2).
3. **GetCollision instrumentation** — Boolean 전략 전에 tool↔target collision 판정 (`None/Touch/Intersect/Contained/ContainedTouch`)을 **우선 로깅으로만** 배치. corpus 데이터로 "Imprinting failed = tangency" 가설이 확인된 후에만 자동 보정(geometry-scaled perturbation, through-hole은 Intersect 필수 / blind-hole은 Contained 허용)으로 승격.
4. **Transform-dirty flag** — body에 extraction 이후 Transform/topology 변경 heal이 가해졌는지 추적. 캐시 좌표 fallback (Ventilator axis tier)의 안전 조건.

### 2.3 In-process Checkpoint/Restore — Copy-Probe-Commit (조건부)

`Modeler.Body.Copy()` / `Copy(out faceMap, out edgeMap)` (XML:11781-11787, BendingFixtureService.cs:321에서 이미 사용 중) + `DesignBody.Create(Part, name, Body)` (XML:7294).

**트랜잭션 패턴 (3단계):**
```
Probe(copy)  : 위험한 op를 detached copy에서 실행, faceMap으로 target 변환, 기존 verifier로 측정
Apply(live)  : 증명된 op만 live body에 1회 적용
Swap-in      : live 적용도 실패하면 DesignBody.Create(part, name, provenCopy)로 교체, 오염 body는 삭제하지 않고 abandon (deleted-face body의 Delete 자체가 throw 가능)
```

**⚠️ 전제가 미검증** — "Document.Open NRE after any mod failure"는 body가 아니라 **process-global 상태 오염**을 시사. 따라서 이 아키텍처 전체가 **Blast-Radius Probe 실험 (§5 Phase 1 E-1) 통과를 gate로 한다.** copy-per-attempt 정책 필수: pristine B0는 절대 건드리지 않고 attempt i마다 Bi = B0.Copy() — "성공 후에만 재캡처" 정책은 한 단계 아래에서 같은 one-shot 문제를 재생산함 (critique 지적).

Tier 1 대안으로 `Application.Undo(n)`도 같은 실험에서 검증하되, **n은 상수가 아니라 `UndoSteps.Count`의 before/after 차분으로 계산** (ChangeX 1회가 WriteBlock 여러 개 실행 — TryHealBody, progressive multi-step 등).

### 2.4 Unified GeometricProof Verification Contract

**모든** primitive가 의무적으로 채우는 증거 객체. **C#이 유일한 판정 권한** — Python은 transport, offline 스크립트는 집계기로 격하.

```csharp
class GeometricProof {
    Verdict Verdict;                 // VERIFIED | FAILED | INCONCLUSIVE  (3값, fail-open 금지)
    ReasonCode Reason;               // SILENT_NOOP | SIGN_INVERTED | WRONG_VALUE | PROBE_FAILED | KERNEL_REJECTED
    double ExpectedMm, ToleranceUsedMm;          // 기대값/tolerance를 record에 내장 → offline expected_for() 삭제
    double BeforeValueMm, AfterValueMm;          // 동일한 anchored probe로 양쪽 측정
    double VolumeBeforeM3, VolumeAfterM3, AnalyticDeltaM3;  // 보존 법칙 검사
    int FaceCountBefore, FaceCountAfter;
    object CanonicalAnchor;          // axis line / plane pair / fillet support-spine
}
```

- **Anchored predictive probe**: "기대값 근처 탐색"이 아니라 **캡처한 identity 위치에서 측정**. Hole = `Body.IntersectCurve`(축 통과 수직 chord → 2R 직독) + 축방향 extent overlap (counterbore false-positive 제거). Wall = pre-mod centroid에서 normal 방향 ray의 entry/exit pair (Boolean 전략 포함 face identity 불요 — 현행 Boolean 면제 폐지) + `ContainsPoint`(midpoint) 재질 확인.
- **Probe self-test**: mod **전에** 같은 probe로 before 값을 재현 못 하면 INCONCLUSIVE-PROBE — 측정 실패와 수정 실패를 처음으로 분리.
- **Negative control**: before 값이 같은 anchor에서 사라졌는지 확인.
- **Volume 3단 게이트** (전 primitive 공통, 비용 ~0): ① sign(dV) = sign(analytic) — **이것 하나로 모든 방향 반전 버그 검출**; ② |dV| > eps — silent no-op 검출; ③ analytic 대비 ~3배 이내 sanity band (hard fail 아님). sub-mm cell은 `ComputeVolume(precision)` 사용, sheet body는 skip.
- 보고 지표 2개: **proven-success rate** (VERIFIED/attempted)와 **unproven rate** (INCONCLUSIVE/attempted).

---

## 3. Breakthrough 제안 순위 (impact × feasibility)

### 채택 — 우선순위순

| 순위 | 제안 | Impact | Feasibility | 비고 |
|------|------|--------|-------------|------|
| P1 | **ContainsPoint MaterialSign oracle** (fillet sign) | 실패 2 cell 직접 해결 + 모든 sign heuristic 대체 가능 | 높음 (read-only, documented) — 단 codebase 사용 전례 0건이라 spike 선행 | 기존 sign 매핑(:662)은 유지하고 **입력만** 교체. 새 부호 규약 도입 금지 (RT2 실험과 모순) |
| P2 | **Sign-inversion closed-loop retry** (orchestrator 레벨) | P1 실패 시에도 nist_ctc_01/boxy 결정적 해결 | 매우 높음 — 측정값 6.4는 이미 존재, gate만 추가 | fresh import라 deleted-state 무관. signOverride 파라미터 + solo spec 5번째 필드 |
| P3 | **Volume-delta proof** | 18개 primitive 전체에 방향/no-op 검출 | 매우 높음 (documented property, 비용 0) | 정밀도 한계: sub-mm dV ~1e-12 m³ → sign 검사 위주 |
| P4 | **판정 권한 C# 단일화 + INCONCLUSIVE 1급화** | FAKE-OK 클래스 구조적 차단 | 매우 높음 | offline verifier는 도메인 로직 0의 카운터로 |
| P5 | **Copy-Probe-Commit checkpoint** | poisoning cascade 해소 + harness 3배 단축 + probe 전략 부활 | **조건부** — blast-radius 실험 gate | critique: process-global 오염이 남으면 효과 반감 |
| P6 | **Proactive ScaleNormalized decorator** | linkrods/624ZZ Wall 해결 + sub-mm 클래스 전체 | 높음 — 단 3대 불변식 필수 (아래) | native-scale checkpoint 선캡처, bbox 사후 assert, 단일 scale 필드 |
| P7 | **Boolean hole axis tier ladder** (live Cyl → live Cone → cached×scale → tessellation fit) | Ventilator 해결 + free-form hole 일반화 | 높음 | transform-dirty flag 연동, Subtract 후 PieceCount==1 assert, verifier band는 **tool 축** 기준 (순환 논증 차단) |
| P8 | **Fillet rebuild: DeleteFaces(GrowSurrounding) + RoundEdges(Tracker)** | nist_ftc_07 + sign 문제 원천 제거 (방향 무관) | 중간 — undocumented API 2개, 의미론 미검증 | copy probe에서 전체 시퀀스 검증 후 commit. corner patch 포함 전체 chain 삭제, edge 1개씩, 대형 dR은 2-3 단계 분할 (crash 회피는 가설), BlendFaces fallback |
| P9 | **SetFaceGeometry/ReplaceFaceGeometry 표면 교체** (Hole·Wall 전용) | boolean/offset 자체를 우회하는 parametric edit primitive | 중간 — smoke test 필수 (Unsupported 이웃의 no-op 전례) | seam half-face 전부 한 dictionary로. **Fillet 적용은 기각** (§6) |
| P10 | **FaceResolver delegate** (sub-step 간 signature 기반 face 재해석) | multi-pass 기법 전면 해금 | 높음 — 단 mechanism B 증명 실험 선행 (`Topology.IsDeleted` 1회 확인) | 기대 중간값 R_k + axis/origin 근접성으로 패턴 모델 오인 방지. dict variant는 dictionary 자체를 재구축 |
| P11 | **Session-reuse-until-failure harness** + lifecycle 수정 | 35분 → ~10분, nightly matrix 토대 | 높음 (lifecycle 수정은 즉시; session reuse는 driver를 work-queue 루프로 개조 필요) | "예외 발생 시 즉시 evict + K=15 cell cap" 정책. dirty doc close의 save-prompt 위험 실험 포함 |
| P12 | **IPC 단순화** — atomic-rename result-file-is-marker + run-scoped queue file + runs.jsonl ledger | flake 추적, 이력, provenance (DLL build hash 기록) | 매우 높음 | per-cell env-var는 session reuse와 모순 → **queue file**로 (critique 정정) |
| P13 | **Strategy registry strangler** (Boss pilot → Hole/Fillet → Wall 최후) | 신규 primitive hardening 비용 상수화 | 높음 — blast radius 관리가 관건 | cell-level parity (동일 전략 선택 + 동일 측정값) 통과 후 단계 이관 |
| P14 | **Subtract tolerance ladder** (default → 1e-7 → 1e-6, cap = feature/50) | imprint-failure 일부 | 중간 | copy probe 위에서만, volume+dimension 검사 통과 시 채택 |

### 기각 — 사유 명시 (critique dead-end 증거 기반)

§6 비목표 참조. 핵심: OffsetErrorInfo 진단 루프 (silent no-op이 errorInfo **있는 상태에서** 기록됨), SetFaceGeometry fillet (기하학적 불능 — tangency 파괴), Undo 상수 깊이 가정 (WriteBlock 다중 실행), Subtract Tracker (overload 부재), SaveBodies→STEP (Parasolid 전용 enum).

---

## 4. Phase 1 — 이번 주 (highest-confidence)

> 목표: **fillet sign 실패 2 cell 해결 (→ 41/45 = 91.1%)** + 검증 신뢰성 기반 구축 + Phase 2 gate 실험 완료. 아키텍처 변경 없음 — 기존 체인에 외과적 삽입.

### 작업 항목

| ID | 작업 | 파일 |
|----|------|------|
| W1-1 | **ContainsPoint spike** (1일): TestModelGenerator 단위 body (cube + hole + convex round + concave fillet)에서 6개 probe 구성 전부 기하학적 정답 일치 assert. boundary tolerance 거동 기록. 실패 시 P1 폐기, P2만으로 진행 | Test/RE_SelfTest/ 신규 spike 스크립트 |
| W1-2 | spike 통과 시 **MaterialSign oracle**을 ChangeFilletRadius에 적용: `Concavity`를 mod 시점에 live face에서 유도, `if (faceFeat.IsReversed) sign *= -1` 라인 삭제. mirror probe 필수, 모호 시 캐시 classifier 값 fallback + flag | ModificationService.cs (:662-663), 신규 GeometryOracle.cs |
| W1-3 | **Sign-inversion gate**: ChangeFilletRadius·ChangeHoleDiameter에 ChangeWallThickness(:303-315)와 동일한 측정 게이트 + ReasonCode (\|measured−(before−δ)\|<tol → SIGN_INVERTED) | ModificationService.cs |
| W1-4 | **Closed-loop retry**: orchestrator가 SIGN_INVERTED를 읽으면 signOverride로 해당 cell 1회 재실행 (fresh SC = deleted-state 무관) | run_mod_corpus_atomic.ps1, mod_atomic_test.py, ModificationService.cs (optional signOverride 인자) |
| W1-5 | **GeometricProof 필드 + Volume 3단 게이트**를 ModificationResult에 추가, 우선 기존 3개 primitive + Boss 2개에 적용. Verdict/ReasonCode/ExpectedMm/ToleranceUsedMm를 cell JSON에 내장 | ModificationService.cs, mod_atomic_test.py |
| W1-6 | **offline verifier 격하**: expected_for()·rescue path 삭제, JSON 내장 verdict의 집계기로 전환 | verify_atomic_results.py |
| W1-7 | **Lifecycle 수정**: ① taskkill /PID /T /F 우선 → 이름 기반 sweep → WaitForExit(10s); ② poll에 $p.HasExited 추가 (crash cell 175s 절약); ③ DLL exclusive-open unlock gate (실패 시 loud abort); ④ SpaceClaim.exe 한정 WER DontShowUI; ⑤ JSON 미산출 cell에 CRASH/TIMEOUT 합성 row | run_mod_corpus_atomic.ps1 |
| W1-8 | **IPC**: write_done 삭제, temp→File.Move atomic rename이 marker 겸 heartbeat. run-scoped 결과 dir. runs.jsonl ledger (run_id, DLL hash, per-cell 타이밍, exit 분류) | mod_atomic_test.py, run_mod_corpus_atomic.ps1 |
| **E-1** | **Blast-Radius Probe 실험 (Phase 2 gate, 30분~반나절)**: 강제 OffsetFaces 실패 후 — ① restored copy에 op 가능? ② Document.Create? ③ 같은 파일 Document.Open? ④ 다른 파일 Open? ⑤ 오염 doc close 후? ⑥ dirty doc close → 다음 모델 open (save-prompt 위험)? ⑦ 단일 세션 N개 연속 성공 cell (열화 baseline)? ⑧ **copy에서 offset 실패 → 같은 doc의 pristine 원본에 즉시 corrective offset** (fillet 부활 시나리오)? + Undo(ΔUndoSteps) 복원 여부 | 신규 probe 스크립트, linkrods Wall cell 사용 |
| **E-2** | **Mechanism B 증명** (15분): fillet face에 성공 offset 1회 후 캐시 핸들의 `Topology.IsDeleted` 확인 — true이면서 body에 R0+δ/N face가 존재하면 stale-reference 확정 | probe 스크립트 |
| **E-3** | **SetFaceGeometry smoke test** (반나절): 단순 hole enlarge 1 cell — axis-band 측정 + volume delta 양쪽 통과 확인. 실패 거동 (poisoning 여부)도 기록 | probe 스크립트 |

### 검증 방법
- Acceptance gate: **nist_ctc_01·boxy fillet → verified-correct**, 나머지 18개 fillet cell 회귀 0. atomic corpus 전체 재실행으로 cell-level parity 확인.
- W1-5: 의도적으로 잘못된 mod (wrong face offset, half delta)를 가한 **mutation test**로 volume 게이트가 실제로 FAIL을 내는지 확인.
- E-1~E-3 결과는 runs.jsonl에 기록하고 Phase 2 go/no-go 결정에 직결.

### 예상 효과
- 성공률 86.7% → **91.1%** (41/45). sign-inversion 클래스 (silent wrong geometry — 실패보다 나쁨) 영구 차단.
- crash cell당 ~175s 절약, zombie DLL lock으로 인한 run 전멸/재부팅 위험 제거.
- 86.7%가 "상한값"에서 "증명값"으로: proven/unproven 2지표 분리 보고.

---

## 5. Phase 2 — 검증 후 (E-1~E-3 gate 통과 항목)

> 목표: **잔여 4 cell (linkrods·624ZZ Wall, Ventilator Hole, nist_ftc_07 Fillet) → 44~45/45 (97.8~100%)** + harness 35분 → ~10분.

### 작업 항목

| ID | 작업 | Gate | 검증 |
|----|------|------|------|
| W2-1 | **Copy-Probe-Commit 트랜잭션** 도입 (copy-per-attempt, swap-in 복구, abandon-not-delete) | E-1 ①⑧ 통과 | linkrods Wall에서 "native 실패 → 복원 → Boolean 성공"이 **단일 프로세스 안에서** 재현될 때만 harness 정책 변경 |
| W2-2 | **Session-reuse-until-failure**: driver를 in-process work-queue 루프로 개조 (queue file 소비), 예외 즉시 evict + K=15 cap, result-file heartbeat로 hang 감지·resume. crash 이력 모델은 per-cell 격리 유지 | E-1 ⑥⑦ 통과 | 깨끗한 단일 run에서 per-cell start/end 타임스탬프로 타이밍·실패 census 동일 데이터셋 재측정 (기존 수치는 2개 run 혼합이었음) |
| W2-3 | **ScaleNormalized decorator** — 3대 불변식: ① scale 전 native-scale Body.Copy checkpoint (복구는 "scaled body 폐기 + native backup 복귀", Transform(0.001) 신뢰 금지); ② 복원 후 bbox assert 불일치 시 cell hard-fail (1000x 잔류 geometry의 silent corruption 차단); ③ 단일 ModContext.ScaleFactor를 모든 tolerance (:567 1e-4 floor, :362 1e-7)·캐시 좌표 소비자가 사용 | W2-1 | sub-2mm cell (linkrods Hole/Wall/Fillet, 624ZZ Wall)을 threshold {0.5, 1.0, 2.0}mm로 sweep하여 경계 결정 |
| W2-4 | **Hole axis tier ladder**: live Cylinder → live Cone (Frame.DirZ) → cached Origin/Normal×scale (transform-dirty flag clean일 때만) → GetTessellation least-squares fit. Subtract 전 axis sanity check (live face bbox의 축 수직 spread ≈ cached R), 후 PieceCount==1 + tool-axis 기준 band verifier | 없음 (독립) | Ventilator cell 단독 검증 — W2-3과 **분리된 cell**에서 검증 (효과 혼동 방지) |
| W2-5 | **Fillet rebuild 전략**: copy probe에서 DeleteFaces(전체 chain + corner patch, GrowSurrounding) → support face 생존자 간 신규 edge 탐색 → RoundEdges 1-edge-per-call + Tracker + 반환 dict로 face identity → 대형 dR은 2-3 분할. CapAcross/BlendFaces fallback | E-3 패턴 + nist_ctc_01에서 GrowSurrounding 의미론 probe 1회 | nist_ftc_07 + fillet 전 cell 회귀. registry에 CrashRisk=High 유지 (단일 edge crash 회피는 가설임) |
| W2-6 | **SetFaceGeometry 전략** (Hole·Wall만): seam half-face 일괄 dictionary, ReplaceFaceOptions.Tolerance 명시. 기존 체인은 fallback으로 존치 | E-3 통과 | corpus Hole/Wall 열에서 기존 전략과 cell-level parity 이상일 때만 primary 승격 |
| W2-7 | **FaceResolver delegate**: TryOffsetFacesProgressive 양 variant가 sub-step마다 fresh Face[]/Dictionary 재구축. RoundEdges 경로는 Tracker 채택 | E-2 통과 | fillet N≥2 subdivision이 동작하는지 + 패턴 모델 (유사 R 다수)에서 오인 0 확인 |
| W2-8 | **Anchored predictive probe** (IntersectCurve)로 wall/hole 측정 교체 + Boolean 면제 폐지 + negative control | 없음 | 기존 corpus에서 측정값 동일성 + counterbore 모델 (624ZZ) mutation test |
| W2-9 | **Registry strangler pilot**: Boss 2개 경로를 generic executor로 — 현재 hardening zero라 순수 이득. MeasuredAfterMm 최초 부여 | W2-1 | Boss cell 신설 + 기존 60 cell 회귀 0 |

### 예상 효과
- 성공률 91.1% → **97.8% (44/45)**, fillet rebuild 성공 시 **100% (45/45)**.
- harness: 발사 횟수 60 → ~8-12 (1 + 실패 수), wall-clock **~35분 → 9-12분** (3배). 단 hard-crash cell은 process 격리 영구 유지라 "60→12"는 상한 추정.
- probe 전략 부활: 향후 모든 파괴적 op (defeaturing, simplify, void-cut)가 트랜잭션 안전성 획득.

---

## 6. Phase 3 — 연구 필요 (장기)

| ID | 작업 | 검증 | 예상 효과 |
|----|------|------|----------|
| W3-1 | **18-primitive matrix 확장**: extract-only applicability manifest pre-pass (1세션 ~2분, DLL build hash에 pin) → 360 중 ~200 applicable cell만 생성. L1 in-process oracle을 15개 미보유 primitive로 확장 (Add* = 기대 위치/크기 신규 feature 스캔, Move/Rotate = centroid/axis delta, Remove = 부재 + face-count delta, Mirror/Pattern = instance count + 대칭 잔차). **신규 oracle은 mutation test 통과 전 신뢰 금지** (fake-OK 재생산 방지) | golden manifest는 양자화 feature signature key (type, size 0.01mm, centroid 0.1mm, axis) — face index 금지. 최초 run은 사람이 검수 후 pin | T1 smoke (현 3 primitive, per-commit ~10분) / T2 nightly 전체 (~200 cell, 25-40분) / T3 weekly kernel-limit sweep |
| W3-2 | **STEP round-trip spot-check**: `part.Export(PartExportFormat.Step, ...)` (ExportStepCommand.cs:58의 검증된 경로) → fresh import → re-extract, 야간 ~10 cell 순환. Student edition export 제약 1회 확인 선행 | 치수 일치 | kernel-truth 검증 계층 |
| W3-3 | **Canonical geometric identity**: cylinder = foot-of-perpendicular 정규화 axis line + axial extent band; wall = 부호 정규화 plane pair + **예측된** post-mod offset 매칭; fillet = support-spine (비-fillet 이웃과의 경계 edge); body-level dimension fingerprint. Moniker는 topology-preserving 전략의 fast path로만 | 다단계 edit script (ApplyOperations)에서 step 간 identity 생존율 측정 | multi-edit 스크립팅 (phone CAD 본 목표)의 전제 조건 |
| W3-4 | **GetPName/GetFaceID characterization probe** (15분): op/save/reload 3경계에서 어떤 식별자가 생존하는지 분류. STEP import body의 PName이 비어 있으면 전면 폐기, 정수 ID는 fast-path cache로만 | 3-dump 비교 | 성공 시 in-process↔offline 검증의 identity 공간 통일 |
| W3-5 | **GetCollision 자동 보정 승격** (instrumentation 데이터가 tangency 가설을 확인한 경우): tool 길이 5-10% 연장, radius perturbation max(1e-6m, 0.1%R) — geometry-scaled | Boolean cell 전수 로깅 결과 분석 | imprint-failure 잔여분 |
| W3-6 | **Subtract tolerance ladder** (P14) — copy probe 위, cap = feature/50, 1e-6 초과 금지 | volume+dimension 이중 통과 | dirty STEP 대응 보강 |
| W3-7 | **Registry 전면 이관** (Hole → Fillet → Wall 최후) | 단계마다 cell-level parity | 신규 feature type (slot, pocket, rib) 추가 비용 = registry entry 1개 |

---

## 7. 비목표 (Non-Goals) — 추진하지 않기로 한 것과 그 이유

| 비목표 | 기각 사유 (dead-end 증거) |
|--------|--------------------------|
| **SetFaceGeometry로 fillet 반경 변경** (동일 axis + 신규 R) | 기하학적 불능: 동일 축의 신규 반경 cylinder는 support face와의 tangency를 상실 — R 감소 시 support 평면(축에서 R_old 거리의 접평면)은 더 작은 동심 cylinder와 **교차 자체가 불가능**, R 증가 시 non-tangent knife edge 생성. fillet은 W2-5 (delete+re-round) 경로로만 |
| **OffsetBodyFaces + OffsetErrorInfo 진단 루프, FixInexactEdges/FindAndFixGaps targeted repair** | reflection 결과 OffsetBodyFaces의 **모든** overload가 out OffsetErrorInfo를 가짐 → 기록된 silent no-op은 errorInfo가 있는 상태에서 발생한 것. FixInexactEdges는 whole-body 인자라 ProblemEntities 타겟팅이 불가능하고 이미 "no effect"로 기록된 dead end. 수리는 type-correct한 InspectModel→RepairBody(issues) 경로만 고려 |
| **SaveAsSnapshot + Application.Undo 기반 harness 재설계** | "WriteBlock 1회 = undo 1 step" 가정이 거짓 (ChangeX 1회가 다중 WriteBlock 실행). Document.Open NRE는 process-global 오염 시사 — snapshot 재오픈도 동일 NRE 가능성. Body.Copy가 성공하면 대부분 redundant. E-1 실험의 부속 항목으로만 확인 |
| **Subtract에 Tracker 전달** | Body.Subtract에는 Tracker overload가 존재하지 않음 (XML 11574/11599/11626 — ICollection 계열뿐). Tracker는 RoundEdges/Fuse/Split/Imprint 한정 |
| **Unsupported.BodyMethods.SaveBodies → STEP round-trip** | BodySaveFormat enum 멤버는 Text/Binary (Parasolid x_t/x_b)뿐 — STEP 불가. round-trip은 검증된 part.Export(PartExportFormat.Step) 사용 |
| **SQLite 결과 ledger** | SC 내장 IronPython 2.7에 sqlite3 부재, native dependency 세금. 단일 writer JSONL로 충분 (lock-free, git-diffable) |
| **per-cell env-var spec 채널** | env var는 프로세스 기동 시 1회 주입 — session reuse (W2-2)와 구조적으로 모순. run-scoped 파라미터만 env, cell spec은 queue file |
| **live body에서의 연속 offset probe+corrective** | 검증된 dead end: 첫 offset (성공이든 실패든) 후 캐시 Face가 "object is deleted". probe는 반드시 detached copy에서 (W2-1) |
| **"positive offset = 재질 반대 방향" 신규 부호 규약** | codebase 자체 실험과 모순 (wall :64 vs RT2 hole :416-421이 서로 반대). 기존 corpus-proven 매핑 유지, oracle은 **입력값**만 교체 |
| **SC 병렬 세션** | ANSYS Student license 동시성·/RunScript 다중 인스턴스 거동 미검증. 2-instance smoke test 전까지 아키텍처에 반영 금지 |
| **GetFaceID/GetModelerId를 영속 identity로 사용** | Parasolid 세션 entity tag — save/reload 비생존, boolean 중 re-tag. "ID 소멸 = topology 변경" 추론은 Boolean cell 전부에서 false positive. fast-path cache로만 |
| **머신 전역 WER 억제** | 테스트 머신의 모든 앱에 영향. SpaceClaim.exe per-executable 한정 + dialog-title 감시 defense-in-depth |
| **V252\bld_N 다중 폴더 + manifest pointer 배포** | SC는 AddIns 트리를 스캔해 발견되는 모든 add-in을 로드 — 이중 로딩 (중복 command 등록) 위험 + 하드코딩 ADDIN_DLL 경로 파손. 외부 빌드 dir + directory junction (mklink /J) flip으로 대체 |
| **무조건적 1000x scaling / boolean tolerance 1e-5 이상 완화 / Accuracy.LinearResolution 변경** | 무조건 scaling은 samplemodel2(1624mm)를 1.6km로 — 다른 실패 클래스 생성. 10um tolerance는 0.25mm hole을 weld 가능 + tolerant edge가 후속 op 오염. LinearResolution은 read-only 확인됨 |
| **registry 도입 시점의 LLM introspection 연동** | speculative scope creep — 86.7% 기반 회귀 게이트가 우선. registry 안정화 후 별도 plan |

---

## 부록: 핵심 파일

- `D:\MXDigitalTwinModeller\Services\ReverseEngineer\ModificationService.cs` — 전 전략·검증의 본체
- `D:\MXDigitalTwinModeller\Services\ReverseEngineer\FaceClassifier.cs`, `CylinderRoleClassifier.cs` — concavity 입력 교체 대상
- `D:\MXDigitalTwinModeller\Test\RE_SelfTest\mod_atomic_test.py` / `run_mod_corpus_atomic.ps1` / `verify_atomic_results.py` — harness 3계층
- `D:\MXDigitalTwinModeller\Test\RealCAD\atomic_corpus_summary.md` + `atomic_results\*.json` — 회귀 게이트 데이터
- `D:\MXDigitalTwinModeller\.claude\skills\ansys-api-catalog\raw\SpaceClaim.Api.V252.xml` — API surface (Body.Copy:11781, DesignBody.Create:7294, ContainsPoint:12240, IntersectCurve:12249, RoundEdges:11437-11461)
