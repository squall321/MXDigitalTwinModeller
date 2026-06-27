# W4 Long-Tail Design Note — Triage of Remaining Matrix Failures

> Read-only analysis. Inputs: `Test/RealCAD/matrix_summary.md` (9 models × 18 primitives, ~81% verified),
> the per-cell JSONs in `Test/RealCAD/matrix_results/`, `Services/ReverseEngineer/ModificationService.cs`,
> `Test/RE_SelfTest/mod_matrix_test.py`, `Test/RE_SelfTest/kernel_truth.py`, and `MOD_BREAKTHROUGH_PLAN.md §8`.
> Goal: separate tractable fixes from genuine kernel/heuristic limits and give a concrete, gated path for the tractable ones.

## TL;DR ranking (impact × tractability)

| Rank | Class | Cells | Verdict | Why |
|------|-------|-------|---------|-----|
| **1** | **ChangeBossDiameter via coaxial Boolean** | sm2, 624ZZ (2 F) | **TRACTABLE** — high value | W4-5 coaxial pattern already exists for ChangeBossHeight; diameter is the same shape op. Replaces the failing `OffsetFaces`. |
| **2** | **MoveHole / RotateBoss / RotateHole oracle → kernel-truth** | SampleModel1+boxy MoveHole IC, possibly RotateBoss sm2/11752 | **TRACTABLE** — oracle-only, no kernel risk | The IC/F is partly a *verification* failure (extractor phantom), not a kernel failure. `kernel_truth.diff` is the fix. |
| **3** | **RotateBoss large-arc re-add** | sm2, 11752 (2 F) | **PARTLY TRACTABLE** | The op (MoveBoss) verifiably works for small +x shift; large rotated displacement either lands off the host face or extractor drops the boss. Mix of oracle + placement. |
| — | **624ZZ ChangeWallThickness** | 1 F | **KERNEL LIMIT** — do not chase | Documented `OffsetFaces`/swap/Boolean cascade poison on curved bearing race. Already concluded in Phase 2. |
| — | **as1-oc MoveHole "non-manifold"** | 1 F | **ASSEMBLY LIMIT (W4-4)** | Needs body-targeted assembly drilling; out of single-body scope. |
| — | **11752 Add\* on thin curved flange** | 3 F + 2 IC | **KERNEL/PLACEMENT LIMIT** | Single pathological flange; through-extent assumption invalid. Low ROI. |

---

## 1. ChangeBossDiameter — `OffsetFaces failed: Operation failed.` (RANK 1, TRACTABLE)

**Data.** Two FAILED cells:
- `02_samplemodel2_ChangeBossDiameter.json`: `"BossD 610.000->732.000 B1"`, `"msg": "OffsetFaces failed: Operation failed."`, `after: null`.
- `09_624ZZ_bearing_ChangeBossDiameter.json`: `"BossD 12.800->15.360 B1"`, `status OK` but `"oracle": "no boss at D=15.360"`, `after ≈ 12.80` (silent no-op — the offset ran but produced nothing the oracle could find).

**Root cause (code).** `ChangeBossDiameter` (ModificationService.cs:1708-1770) has exactly one strategy:

```csharp
designBody.Shape.OffsetFaces(new[] { face }, offset);   // :1756
```

There is no Boolean fallback and no `useBoolean` parameter — unlike `ChangeBossHeight`, which already routes to `TryChangeBossHeightBoolean` (:1366-1441) when the harness retries with `useBoolean=true`. samplemodel2 is the big-Δ case (R 305→366mm, dR=61mm) where `OffsetFaces` blows up exactly as documented for the 540→649 boss in `§8`; 624ZZ is the curved-bearing case where `OffsetFaces` is a silent no-op (the same "Operation failed"/no-op behaviour seen on 624ZZ ChangeBossHeight and Wall).

**TRACTABLE.** The W4-5 coaxial-Boolean pattern generalizes cleanly to diameter — arguably *more* cleanly than to height, because a diameter change is a pure concentric radial shell, no cap/end detection needed:

- **Grow (dR>0):** Unite a coaxial cylinder of the new radius spanning the boss's full axial extent (the annulus `[currentR, newR]` is added; the inner overlap with existing material is a harmless Unite no-op).
- **Shrink (dR<0):** Subtract a coaxial **tube** — i.e. Subtract the region `[newR, currentR]`. Cannot Subtract a solid cylinder of newR (that would gut the boss). Implement as: build cyl(currentR) and cyl(newR), or simpler, Subtract a thin annular sweep. Practical approach: Subtract cyl(currentR) then Unite cyl(newR) in one write-block (net = resize), OR for shrink, Subtract the outer shell by first Uniting newR then trimming — start with the grow case only (both failing cells are grows: 610→732 and 12.8→15.36).

**Concrete proposal.**
1. Add `TryChangeBossDiameterBoolean(designBody, baseM, axisU, currentRm, newRm)` modeled directly on `TryChangeBossHeightBoolean`:
   - Relocate the live boss cylinder via `TryRelocateHoleCylinder` to get the true `foot/dir/R/tLo/tHi` (extraction-independent; this is what already makes the height-Boolean robust on curved 624ZZ).
   - Extrude a `CircleProfile` of radius `newRm` from `tLo-ε` over length `(tHi-tLo)+2ε`, wrap in `DesignBody.Create(part, "_bossd", cyl)`.
   - `grow`: `designBody.Shape.Unite([cylDb.Shape])`.
   - `shrink`: build `cylOuter(currentRm+ε)` and `cylInner(newRm)`; Subtract `cylOuter` then Unite `cylInner` (resize), OR Subtract a pre-built annulus. Keep shrink behind a separate gate since neither failing cell needs it.
2. Add `bool useBoolean = false` param to `ChangeBossDiameter`; when set, call the new method and skip `OffsetFaces` entirely (no poison).
3. Harness already has the retry mechanism for ChangeBossHeight (`useBoolean=true` on a fresh body) — extend the same retry trigger to ChangeBossDiameter's `OffsetFaces failed` / silent-no-op.

**Verifying oracle (kernel-truth).** After the op, assert via `kernel_truth.cyl_radii(diff(fp0,fp1))`: exactly one cylinder radius bucket should move `currentR → newR` (count −1 at `_q(currentR)`, +1 at `_q(newR)`), `dvol_m3` sign positive for grow. This is exactly the mutation-test pattern already validated (`gate_kernel_truth.py` on nist Hole 25→30). Drop the driver's `find boss at D=15.360` (extractor-dependent) and use `cyl_radii` delta — that alone would have flipped 624ZZ from a false "no boss" to a true pass *if* the Boolean engages.

**Gate experiment (de-risk first).** Probe on samplemodel2 B1 only: relocate live boss cylinder, extrude cyl(366mm) over its axial extent, `Unite` inside a `DesignBody.Create` wrapper, then read `cyl_radii(diff)`. Confirm (a) `relocated=true` with sane R≈305mm, (b) Unite returns without "General Failure", (c) radius bucket moves 305→366. If Unite poisons on this large part, fall back to per-attempt fresh-import (the body-poison landmine in §0 means no in-process recovery — gate must run on a clean body).

---

## 2. MoveHole / Rotate\* oracle → kernel-truth (RANK 2, TRACTABLE, oracle-only)

**Data.** `MoveHole` is **INCONCLUSIVE** on SampleModel1 (`"old slot pop 1->2"`) and boxy (`"old slot pop 2->2"`) — the hole *is* placed at the new position, but the oracle cannot confirm the old slot vacated. samplemodel2 MoveHole IC likewise (`pop 1->2`).

**Root cause (code).** The MoveHole *kernel op* is sound — the W4 fill-margin fix (`rFillM = rM + max(rM*2%, 1e-5)`, ModificationService.cs:1560 region, and `UniteFillerCylinder` at :1577) absorbs the bore wall so there is no coincident zero-thickness face. The IC comes entirely from the **driver oracle** (`mod_matrix_test.py` `verify`, :512-522), which counts holes via `count_holes_near(g2, …)` on the **re-extracted FeatureGraph `g2`**. On multi-hole parts the extractor re-recognizes a *phantom* or a neighbouring identical hole at the old slot, so `cntOldAfter` does not drop (`pop 1->2`) even though the live B-rep is correct. This is the same "extractor lies on re-extraction" failure mode that `kernel_truth.py` was built to bypass (it was explicitly validated to be `stable + sensitive + specific` where the extractor is not).

**TRACTABLE — pure verification change, zero kernel risk.** Replace the `count_holes_near(g2, …)` vacancy test with a kernel-truth probe of the **live body** at the old axis:
- Use `kernel_truth.find_cylinder_near(body, old_m, axis_u, radius_m)` — if it returns `None` (no live cylinder of that radius whose axis passes through `old`), the old bore is genuinely gone.
- Belt-and-braces: `body.Shape.ContainsPoint(old_midpoint)` should now be **solid** (filled). Old slot vacated ⟺ (no live cylinder at old) AND (old centre is solid).
- New slot present ⟺ `find_cylinder_near(body, new_m, axis_u, radius_m)` returns a record AND its axis-centre is **void**.

This removes the extractor from the loop entirely and converts the 2–3 MoveHole IC cells to VERIFIED without touching `ModificationService`. The same swap applies verbatim to the Rotate\*/MoveBoss `verify` fallbacks, which already *try* `find_live_cylinder_near` (driver:631-637) but as a secondary path behind the extractor — promote the kernel-truth path to primary.

**Caveat on SampleModel1 `.scdoc` quirk.** `§8` notes SampleModel1 MoveHole IC is a `.scdoc` extraction quirk. Reading the data, that quirk is precisely the extractor re-recognition phantom above — a live-body kernel-truth oracle does not consult `.scdoc` re-extraction and should resolve it. This is the single highest-confidence, lowest-risk win on the board.

**Gate experiment.** None needed for the kernel — it is read-only. One smoke check: on boxy MoveHole, after the (already-passing) op, assert `find_cylinder_near(body, old) is None` and `ContainsPoint(old_mid)==True`. If those hold, ship the oracle swap.

---

## 3. RotateBoss — `"nothing at rotated position"` (RANK 3, PARTLY TRACTABLE)

**Data.** FAILED on samplemodel2 and 11752 (`status OK`, `oracle "nothing at rotated position"`, `before/after null`). Note the contrast: **MoveBoss VERIFIED** on both samplemodel2 and 11752, and **RotateBoss VERIFIED** on face_recog and 624ZZ.

**Root cause (code).** `RotateBoss` (:2148-2180) is a thin wrapper: it computes the rotated base position with `RotatePointRodrigues` and delegates to `MoveBoss`. So the op is identical to the verified MoveBoss op — the difference is *displacement magnitude and landing spot*:
- MoveBoss test uses `shift = min(max(2d,3), sz*0.25, 20)` along +x — a small, on-face nudge (sm2 `+x 20.00`, verified).
- RotateBoss test rotates 15° about Z@center (driver:598-602). For samplemodel2's large boss far from centre, a 15° arc is a large tangential displacement; the re-added boss's base can land where the host face no longer supports it (AddBoss in MoveBoss doesn't engage → no new cylinder), OR the boss lands fine but the **extractor drops it** (the documented "boss re-recognition" failure).

Two distinct sub-causes, needing different fixes:

**(a) Extractor-drop sub-case → TRACTABLE (oracle).** Same fix as Rank 2: the RotateBoss `verify` (driver:608-637) already has a `find_live_cylinder_near` fallback, but it requires `rN and rO is None`. If the relocation/`find_live_cylinder_near` tolerance or axis filter is too tight for a rotated (and possibly slightly re-axised) boss, it returns `None` → FAILED. Switch this fallback to `kernel_truth.find_cylinder_near` (looser, axis-line perpendicular distance, validated) as the **primary** check. This will recover any cell where the op actually worked but the extractor/loose-finder missed it.

**(b) Off-face landing sub-case → HEURISTIC LIMIT.** If the rotated boss genuinely lands off the host planar face, MoveBoss's AddBoss cannot engage and there is no boss to find — a true failure, not a verification artifact. This is *not* worth a fragile heuristic (re-projecting the rotated point onto the nearest face changes the requested transform and would be confirmation-biased). The honest move: have RotateBoss **pre-check** that the rotated base is inside/over a supporting face (ContainsPoint straddle on the host) and, if not, return a clean `INCONCLUSIVE`/`FAILED` with reason `ROTATED_OFF_SUPPORT` rather than a silent no-op. de-risk: run the kernel-truth finder first (3a); only the residue after that is the genuine off-support class.

**Gate experiment.** On samplemodel2 RotateBoss: after the op, run `kernel_truth.find_cylinder_near(body, expp_m, axis, R)`. If it returns a record → this was an extractor-drop (fix 3a, oracle swap). If `None` and `ContainsPoint(expp_mid)` is solid → boss never got added (3b, off-support, accept as honest fail). This single probe classifies sm2 vs 11752 deterministically before writing any code.

---

## 4. 624ZZ ChangeWallThickness — KERNEL LIMIT (do not chase)

**Data.** `09_624ZZ_bearing_ChangeWallThickness.json`, `T 5.000->4.500`, msg:
`"WallSwap: inside writeblock: Operation failed. | symmetric: step 1/2 failed: The object is deleted. … A-only threw: The object is deleted. | B-only threw: The object is deleted. | Boolean: The object is deleted."`

This is the **full documented poison cascade** (`§1` cell #5, `§8` Phase 2): native-scale `OffsetFaces` fails on the curved bearing race and poisons the body, taking down every downstream strategy (WallSwap → symmetric → A-only → B-only → Boolean) with "The object is deleted." Phase 2 already proved this is *structural*, not tolerance — `1000×` ScaleNormalized fails identically. **Genuine kernel limit. No tractable fix in single-body scope.** The only path is per-attempt fresh-import with proactive scale *before* any OffsetFaces touches the body (W2-3 proactive), which Phase 2 found does not rescue the bearing-race class. Mark closed; do not propose a heuristic.

## 5. as1-oc MoveHole — ASSEMBLY LIMIT (W4-4)

**Data.** `04_as1-oc-214_MoveHole.json`: `"MoveHole.subtract failed: AddHole failed: Result may become non-manifold."` The fill succeeded; the re-drill at the new position crosses an assembly body boundary → non-manifold. This is the W4-4 body-targeted-assembly class (select the owning body, verify the new bore is fully inside it via ContainsPoint before drilling). Requires multi-body corpus work; out of scope for the single-body long-tail. Not tractable now.

## 6. 11752 Add\* on thin curved flange — KERNEL/PLACEMENT LIMIT (low ROI)

**Data.** `06_11752_AddBoss/AddHole`: `"no new boss/hole (volOk=False)"` (cutter/extrude doesn't engage); `AddRib`: `"dV=-4.83e-13, faces +0"` (no engagement); AddPocket/AddSlit INCONCLUSIVE with the same near-zero dV. Side-face placement on `n=(0,-1,0)` of a thin curved flange where the through-extent assumption (bbox projection) overshoots into air, so the prism never intersects material. `§8` already isolates this as the lone residual pathological model after W4-2 fixed the other 8 models' curved placement. The general engine is done; this is one part needing flange-specific through-extent measurement (IntersectCurve-based local solid depth, W4-2 style) rather than bbox projection. Tractable in principle but single-cell ROI — defer to corpus expansion.

---

## Recommended order of work

1. **Rank 2 (oracle → kernel-truth for MoveHole/Rotate\*)** — zero kernel risk, read-only, converts 2–4 IC cells to VERIFIED and de-risks Rank 3 classification. Do first.
2. **Rank 1 (ChangeBossDiameter coaxial Boolean)** — reuse `TryChangeBossHeightBoolean` shape; gate the Unite on sm2 first, verify with `cyl_radii(diff)`. Highest *new-pass* yield (2 F → V).
3. **Rank 3 (RotateBoss)** — after Rank 2's oracle swap, the kernel-truth finder reclassifies sm2/11752 into "extractor-drop" (recovered) vs "off-support" (honest fail); only then decide if 3b needs the support pre-check.

Closed as genuine limits: 624ZZ Wall (kernel poison cascade), as1-oc MoveHole (assembly, W4-4), 11752 Add\* (flange through-extent, defer). Do **not** build heuristics for these.
