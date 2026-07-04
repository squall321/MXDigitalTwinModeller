# MX Digital Twin Modeller — High-Level Result Post-Processing Automation
### Ideas + Architecture Decision Document
*Author: MX CAE Group · Date: 2026-05-30 · Status: Draft for review*

---

## 0. TL;DR

- The current popup pattern (ACT button → JSON/CSV dump → external `MXPostViewer.exe`) is sound and covers ~30% of what a modern CAE post-processing pipeline should do. Keep it as the **fast-path viewer**.
- For deeper analyses (DPF mesh fields, modal participation, multi-case parametric, rainflow, SRS) the project will hit a wall with the ACT-only path — the ACT `Solution.Add*` surface is rich but only exposes *what Mechanical itself plots*, not raw mesh fields.
- **Recommendation: Hybrid (Option C)**. Keep popup for interactive viz; add PyAnsys/DPF as a **batch sidecar** for the heavy work; add an LLM-driven Markdown/PDF reporter as a third leg.
- The single biggest unknown is the **DPF Server license for ANSYS Student**. Verify this *before* committing to a PyAnsys roadmap (see §4).

---

## 1. Current State Analysis

### 1.1 `PostProcessDialog` (main.py:1651-2179) does today
| Stage | What it does | API used |
|---|---|---|
| 1. Analysis pick | Lists Transient analyses, lets user pick one | `Model.GetChildren(Analysis,True)`, `a.AnalysisType` |
| 2. Add results | Adds one `TotalDeformation` + one `EquivalentStress` for **all bodies** (overview), then one of each **per body** scoped via `GetGeoBody()` + `SelectionInfo` | `sol.AddTotalDeformation()`, `sol.AddEquivalentStress()`, `result.Location = sel` |
| 3. Evaluate | `sol.EvaluateAllResults()` | — |
| 4. Rank | Pulls `MaximumOfMaximumOverTime` (scalar Quantity) from each result, sorts by max deformation, keeps Top-N | `_safe_float` parser around `Quantity` |
| 5. Export | For each ranked result: `result.ExportToTextFile(path)` → ANSYS time-history CSV. Writes `metadata.json`. | `ExportToTextFile` |
| 6. Launch viewer | `Process.Start(MXPostViewer.exe, meta_path)` — falls back to venv-Python + `runner.py` | `System.Diagnostics.Process` |

### 1.2 `MXPostViewer.exe` (`postprocess/visualizer.py`, 535 LOC) does today
Four PyQt5 tabs, all matplotlib-based:
- **Summary** — table with rank, max deformation, max VM stress, P2P, RMS, color-coded by `thresh_red_mm`/`thresh_yellow_mm`.
- **Time History** — per-body or overlay-all, dashed RMS lines, force trace below.
- **FFT** — single-sided magnitude, top-5 peak annotations, `f_op` marker.
- **FRF (Bode)** — H1 estimator (`Gxy/Gxx` via SciPy `welch`/`csd`), magnitude + phase + coherence γ², modal params via half-power bandwidth (`fn`, `ζ`, `Q`).

### 1.3 Gaps — what's missing today
| Gap | Impact |
|---|---|
| Only scalar reductions extracted (`MaximumOfMaximumOverTime`) — **no field data leaves Mechanical** | Cannot do hot-spot clustering, stress linearization, strain energy maps, mode-shape comparison |
| Time-history is body-scoped only; no nodal probes, no path extraction | No PSD-by-node, no membrane/bending decomposition |
| No multi-case / parametric loop | Sensitivity, DOE, Pareto all impossible |
| No fatigue / DEL / rainflow | Project name is "Fatigue" specimen and there's no fatigue post-processor |
| No modal participation, no effective mass | "Which mode dominates at operating freq?" — unanswerable |
| Force CSV is optional and external — not extracted from the load itself | FRF only works if user remembers to point to it |
| No report export (PDF/MD/HTML) | Every result requires a human to screenshot and write up |
| No comparison against previous run | Regression / what-changed analysis impossible |
| No LLM layer | All interpretation is on the engineer |
| Viewer is read-only on JSON snapshot | Cannot re-query model — must rerun ① to add anything new |
| PyInstaller bundle is heavy (~150 MB), rebuild is fragile (hidden imports, Qt plugins) | Deployment friction |

---

## 2. High-Level Analysis Catalog (Ideas)

Legend — **Cmplx**: implementation effort (L/M/H). **Val**: engineering value (L/M/H). **Source**: `ACT` (works with current pattern), `DPF` (needs raw mesh field → PyAnsys-DPF), `CPU` (pure Python on extracted data).

### 2.1 Modal / Vibration
| # | Analysis | What it computes | API needed | Cmplx | Val |
|---|---|---|---|---|---|
| M1 | Modal participation factors (per direction X/Y/Z/RX/RY/RZ) | Σ φᵢᵀMr for each rigid-body direction r | `solution.AddDirectionalDeformation`/post .rst via DPF | DPF | M | H |
| M2 | Effective mass per mode per direction | mᵉff = (φᵢᵀMr)²/(φᵢᵀMφᵢ) | DPF | M | H |
| M3 | Cumulative mass participation table | Σ mᵉff(i)/M_total per direction | DPF + CPU | L | H |
| M4 | Mode classification (bending / torsion / breathing / local) | MAC-style shape clustering using mode shape vectors | DPF + scikit-learn KMeans | M | H |
| M5 | Mode shape similarity (MAC matrix) between two configurations | MAC(i,j)=\|φᵢᵀφⱼ\|²/((φᵢᵀφᵢ)(φⱼᵀφⱼ)) | DPF | M | M |
| M6 | Frequency response per excitation direction | Per-direction Bode, scoped to NS | `AddDeformationFrequencyResponse` (✓ documented) | ACT | M | H |
| M7 | Equivalent radiated power (ERP) | ∫(ρcv²)dA over outer surface | DPF (velocity field) | H | M |
| M8 | Mode shape PNG export per mode | Animate + screenshot deformation | `model.AddFigure()` / DPF + PyVista | M | M |
| M9 | Cross-validation against test FRF | MAC + frequency error matrix vs measured | DPF + CPU | M | H |
| M10 | Strain-energy fraction per mode per body | Σ_elem(½ε:σ)/Σ_total | DPF | H | M |

### 2.2 Transient / Time-Domain
| # | Analysis | What | API | Cmplx | Val |
|---|---|---|---|---|---|
| T1 | PSD of nodal response | Welch PSD per node-of-interest | CPU (extends current `compute_fft`) | L | H |
| T2 | Damping ratio from decay (log-decrement) | δ = (1/n) ln(x₀/xₙ), ζ = δ/√(4π²+δ²) | CPU | L | M |
| T3 | Peak picking with hysteresis | scipy `find_peaks(prominence, distance, height)` per channel | CPU | L | M |
| T4 | Energy balance over time | KE + SE + dissipated; check sum ≈ work-in | `AddElementalStrainEnergy`, `AddEnergyProbe` | ACT | M | H |
| T5 | DEL (Damage Equivalent Load) | DEL = (ΣSᵢᵐnᵢ/N_ref)^(1/m) post-rainflow | CPU (`fatpack`) | M | H |
| T6 | Rainflow counting | ASTM E1049 (or fatpack `rainflow_counting`) | CPU | L | H |
| T7 | Shock Response Spectrum (SRS) | SDOF transfer-function family vs nat-freq | CPU (`pyrocketsrs` or hand-rolled) | M | M |
| T8 | Fatigue life via S-N (Basquin) | Goodman + Miner; bin counts × cycles-to-failure | CPU | M | H |
| T9 | Cumulative damage (Palmgren-Miner) | D = Σ nᵢ/Nᵢ ; flag D≥1 | CPU | L | H |
| T10 | Crest factor / kurtosis | xₚ/x_rms ; spotting impulsive content | CPU | L | M |

### 2.3 Static
| # | Analysis | What | API | Cmplx | Val |
|---|---|---|---|---|---|
| S1 | Hot-spot identification with DBSCAN clustering | Cluster top-X% of stress nodes spatially | DPF + sklearn | M | H |
| S2 | Stress linearization (membrane + bending) | ASME VIII / NB-3000 line decomposition | DPF (need path-line nodal data) | H | H |
| S3 | Safety-factor map (σ_y / σ_VM) | Per-element scalar field | DPF | L | H |
| S4 | Reaction force / moment summation per NS | Σ over NS nodes | `AddForceSummationProbe`, `AddForceReaction` | ACT | L | H |
| S5 | Strain-energy-density distribution | SED = ½σ:ε per element, sorted | DPF | M | M |
| S6 | Path-based extraction along edge | Stress vs. arc-length plot | DPF (`construction.Path`) | M | M |
| S7 | Singularity detection | h-refinement convergence check on local stress | DPF or 2 mesh runs | H | M |

### 2.4 Multi-case / Parametric
| # | Analysis | What | API | Cmplx | Val |
|---|---|---|---|---|---|
| P1 | Sensitivity matrix (input → output) | ∂y/∂xⱼ via finite-difference over Parameters | PyAnsys (drive Workbench params) | M | H |
| P2 | DOE surrogate (RSM, Kriging) | Fit Gaussian Process on N runs | PyAnsys + `scikit-learn`/`smt` | H | H |
| P3 | Pareto front | Multi-objective NSGA-II over surrogate | CPU (`pymoo`) | H | M |
| P4 | Anomaly detection across runs | Isolation Forest on per-run metrics | CPU | L | M |
| P5 | Run-to-run diff ("what changed?") | Δmax_def, ΔRMS, Δmodal-freq table | CPU on JSON | L | H |

### 2.5 Composite / Multi-Physics
| # | Analysis | What | API | Cmplx | Val |
|---|---|---|---|---|---|
| C1 | Tsai-Wu, Tsai-Hill, Hashin failure per ply | Composite failure index | `Model.AddCompositeFailureCriteria` (documented) | ACT/DPF | M | M |
| C2 | Inter-laminar shear stress (ILSS) | τ_xz, τ_yz at ply interfaces | DPF | H | M |
| C3 | Thermal coupling check | Map ΔT to thermal strain, compare to mechanical strain | DPF | H | M |
| C4 | Modal-stress coupling | Which mode contributes most to ω-domain stress peak | DPF + harmonic | H | H |

### 2.6 Reporting / LLM-driven
| # | Analysis | What | API | Cmplx | Val |
|---|---|---|---|---|---|
| R1 | Auto-report (PDF/MD/HTML) | Templated jinja2 report with embedded PNGs | `reportlab`/`weasyprint`/`pandoc` | M | H |
| R2 | Plain-language summary (Korean+English) | LLM prompt with structured JSON → narrative | `anthropic` SDK | L | H |
| R3 | Run-comparison report | Side-by-side previous vs current, LLM commentary | LLM + CPU | M | H |
| R4 | "Why did this fail?" diagnostic | LLM with tool-use: query metric → explain | LLM + tools | M | H |
| R5 | Plot interpretation | Send PNG + numerical context to Claude vision | `anthropic` (vision) | L | M |
| R6 | Action recommendations | "Increase rib thickness 30%" type suggestions, grounded in CAE result | LLM + project memory | M | H |

**Total: 41 analyses**. Roughly: 13 work with current ACT path, 18 need DPF, 6 are pure CPU on extracted data, 4 need LLM.

---

## 3. Architecture Options — Detailed Comparison

### 3.1 Option A — Pure PyAnsys (`ansys-mechanical-core` + `ansys-dpf-core`)
External Python script connects to ANSYS via gRPC, drives Mechanical headless, extracts results, computes analyses, generates reports. **No** Mechanical UI involvement at runtime.

```
[CLI / Jupyter]
     │ gRPC
     ▼
[ansys-mechanical-core] ── drives ──► [Mechanical Server]
     │
     │ DPF gRPC
     ▼
[ansys-dpf-core / pyDPF] ── reads ──► [.rst / .rfrq / .rdsp]
```

**Pros**
- Fully scriptable / reproducible / CI-able
- Modern Python ecosystem (NumPy, SciPy, Pandas, Plotly, Jupyter)
- DPF gives **raw mesh + field data** — unlocks 18 DPF-only analyses
- Can run remote / headless / containerized
- No PyInstaller bundling
- Direct Anthropic SDK integration
- Reproducible: same script → same numbers (no "remember to click ①")

**Cons**
- Requires PyAnsys install — **license dependency unknown for ANSYS Student** (CRITICAL, see §4)
- gRPC startup latency (1-3s per session) — bad for interactive
- Full Mechanical install required on the runner machine
- DPF has its own operator graph paradigm — non-trivial learning curve
- Some operations are slower than in-process ACT (gRPC marshalling)
- Versioning: PyAnsys version must match Mechanical version (v252 → `ansys-dpf-core ~= 0.13`)

### 3.2 Option B — Current Popup Pattern (ACT button → JSON → external EXE)
Click in Mechanical → ACT exec API → dump CSV+JSON → launch separate PyInstaller'd Python EXE.

```
[Mechanical UI]
     │ button click → IronPython
     ▼
[ACT main.py] ── extracts via Add* ──► CSVs + metadata.json
     │ Process.Start
     ▼
[MXPostViewer.exe (PyInstaller)] ── reads JSON ──► PyQt5 + matplotlib
```

**Pros**
- No PyAnsys dependency
- Works on every ANSYS version (Student, Mech-Enterprise) without license check
- Already implemented and proven
- External Python has full CPython ecosystem (PyQt5, matplotlib, scipy)
- Decoupled: viewer can be updated independently of the ACT extension
- Single-binary deployment for end-users via PyInstaller

**Cons**
- Data extraction **strictly limited to ACT API** — no raw mesh fields
- JSON+CSV round-trip is lossy for large meshes (currently only scalar+time-history)
- No live re-query — viewer must be killed and ① rerun
- PyInstaller bundle is ~150 MB; rebuild fragile (hidden imports, Qt plugins, scipy datasets)
- All analyses bounded by what Solution.Add* can produce
- Multi-case work impossible (would need user to manually click N times)

### 3.3 Option C — Hybrid (PyAnsys for heavy/batch, popup for interactive)
Light-weight popup remains the default fast-path. PyAnsys CLI/Jupyter sidecar for batch, DPF-only, and parametric work. Same JSON schema as glue.

```
[Mechanical UI]                       [CLI sidecar]
     │                                      │
     ▼                                      ▼
[ACT button (popup)]              [pyansys + DPF]
     │                                      │
     └────► JSON/CSV + .rst ◄───────────────┘
                  │
                  ▼
        [MXPostViewer.exe] ── tabs: Summary / TH / FFT / FRF / Modal / Fatigue / LLM-Report
```

**Pros**
- Best-of-both: 80% of users get one-click; 20% get full power
- Incremental growth — start with popup-only, add DPF tabs as needed
- Shared viewer means engineers don't learn two UIs
- PyAnsys is opt-in — Student license risk is bounded
- LLM reporter is a single component that consumes the same JSON

**Cons**
- Two code paths to maintain (popup + PyAnsys)
- Documentation burden ("when do I click ① vs. run `mx_batch.py`?")
- JSON schema must be carefully versioned
- Testing matrix doubles (Student vs full ANSYS install)

### 3.4 Option D — Jupyter + PyAnsys (interactive notebook)
ACT button writes a templated notebook with results pre-loaded and pops Jupyter Lab.

**Pros**
- Maximum flexibility — engineers can write custom analyses inline
- Reproducible: notebook = code + output
- Great for one-off investigations / ad-hoc what-ifs

**Cons**
- Requires Python literacy from end-users (Mechanical users typically don't have this)
- Jupyter server lifecycle management (port collisions, kernel deaths)
- Notebooks don't generate clean reports without extra effort (`nbconvert`, parameterized via `papermill`)
- Sub-optimal for non-developer engineers in the project's target audience

### 3.5 Decision Matrix

| Criterion | Weight | A (Pure PyAnsys) | B (Popup-only) | C (Hybrid) | D (Jupyter) |
|---|---:|:-:|:-:|:-:|:-:|
| Works on ANSYS Student | 5 | ❓ | ✅ | ✅ (popup leg) | ❓ |
| Unlocks DPF-only analyses | 5 | ✅ | ❌ | ✅ | ✅ |
| Easy for non-Python engineers | 4 | ❌ | ✅ | ✅ | ❌ |
| One-click from Mechanical | 3 | ❌ | ✅ | ✅ | ⚠️ |
| Reproducible / CI-able | 4 | ✅ | ❌ | ✅ | ✅ |
| Multi-case / parametric | 3 | ✅ | ❌ | ✅ | ✅ |
| Maintenance simplicity | 3 | ✅ | ✅ | ⚠️ | ⚠️ |
| Already implemented | 2 | ❌ | ✅ | ⚠️ | ❌ |
| LLM integration | 3 | ✅ | ⚠️ | ✅ | ✅ |
| Weighted (rough) | | 24 | 19 | **30** | 22 |

**Winner: C (Hybrid).**

---

## 4. License / Setup Investigation (CRITICAL)

This must be verified empirically — published policy can lag the actual feature flags.

### 4.1 PyAnsys-Mechanical (`ansys-mechanical-core`)
- Uses standard Mechanical license (`ansys` increment / `mech_2`).
- ANSYS Student installs ship `mech_2` — but with **mesh-size limits** (≤32k nodes typical) and **no batch license**. Headless gRPC may or may not be allowed.
- **Test**: `python -c "from ansys.mechanical.core import launch_mechanical; m=launch_mechanical(version=252); print(m)"`. If license error → Student build does not support it.

### 4.2 PyAnsys-DPF (`ansys-dpf-core`)
- Two flavors:
  - **DPF Server** — bundled with ANSYS install, uses `dpf` license increment (or shares solver license).
  - **DPF Standalone** (`ansys-dpf-server-2025-2-pre0`) — free download, **mesh-size limited** (the so-called "Community" tier).
- For ANSYS Student, the Community Standalone DPF Server is **likely the only viable path** — large meshes will fail with a license error, small meshes (a few thousand nodes) work fine.
- **Test**: `pip install ansys-dpf-core ansys-dpf-server-2025-2-pre0`, then `from ansys.dpf.core import Model; m=Model(r"path\to\file.rst"); print(m)`. If it can read a Student-produced `.rst` → green light.

### 4.3 Conclusion (preliminary, must confirm)
- Pure popup (Option B): no license concerns.
- Hybrid (Option C): the PyAnsys leg may run in **degraded** mode under Student; design it so it gracefully falls back to "open-pre-solved-.rst-with-Community-DPF" mode.
- Decision rule: ship Option C with the PyAnsys leg as *optional*. If `ansys-dpf-core` import fails → only popup tabs are enabled.

---

## 5. Recommendation

**Adopt Option C (Hybrid)** in three phases:

| Phase | Scope | What it unlocks |
|---|---|---|
| 1 | Extend MXPostViewer with ACT-only analyses + LLM reporter | M1 (partial via ACT), M6, T4, T6, T9, S4, R1, R2, R5 |
| 2 | Add PyAnsys/DPF sidecar (`mx_batch.py`) for headless/batch + DPF-only analyses | M1-M5, S1-S6, P1-P5, C1-C4 |
| 3 | Multi-case + LLM-driven action recommendations | P3, R3, R4, R6 |

The popup keeps working at every step — Phase 1 alone roughly doubles current capability.

---

## 6. Concrete Implementation Roadmap

### Phase 1 — Extend Popup + Reporter (2-3 weeks)

**Files to modify**
- `Mechanical/MXSimulator/main.py` (PostProcessDialog: lines 1651-2179)
  - Add result types: `AddDirectionalDeformation`, `AddElementalStrainEnergy`, `AddForceSummationProbe` for each NS
  - Extract additional reductions: `Minimum`, `Maximum`, `Average` (already on Result interface)
  - Auto-detect force from `analysis.Children → Force` instead of asking user
- `Mechanical/MXSimulator/postprocess/analyzer.py`
  - Add `rainflow_count(signal)`, `damage_miner(counts, SN_curve)`, `del_load(signal, m=5)` — use `fatpack`
  - Add `psd_welch(signal, fs)`, `srs(signal, fs, fn_range)`
  - Add `log_decrement_damping(peaks)`
- `Mechanical/MXSimulator/postprocess/visualizer.py`
  - New tabs: `Fatigue`, `EnergyBalance`, `Reactions`, `Report`
  - `Report` tab calls `report_gen.py` (new) → produces Markdown+PNG
- `Mechanical/MXSimulator/postprocess/report_gen.py` **(new, ~250 LOC)**
  - Templated jinja2 markdown
  - LLM (Anthropic SDK) called with structured summary JSON
  - Tool definitions: `summarize_modal`, `flag_anomalies`, `recommend_action`
  - Korean output via system-prompt flag
- `Mechanical/MXSimulator/postprocess/build_viewer.bat`
  - Update PyInstaller spec to include `fatpack`, `anthropic`, jinja2 templates

**Deliverables**
- `MXPostViewer.exe` v2 with 7 tabs (Summary, TH, FFT, FRF, Fatigue, Energy, Report)
- Markdown report saved next to JSON, embeddable PNGs
- Korean+English narratives

**New dependencies**: `fatpack`, `anthropic`, `jinja2`, `markdown-it-py` (no large deps beyond current set)

### Phase 2 — PyAnsys/DPF Sidecar (3-4 weeks, gated by §4 license check)

**Files to create**
- `Mechanical/MXSimulator/batch/mx_batch.py` — CLI entry; arg = path to `.mechdb` or `.rst` + `metadata.json`
- `Mechanical/MXSimulator/batch/dpf_extractors.py` — DPF operator graphs for: modal participation, strain energy field, hot-spot clustering, MAC
- `Mechanical/MXSimulator/batch/pyansys_runner.py` — drives Mechanical via `ansys-mechanical-core` when re-solve needed (parametric)
- `Mechanical/MXSimulator/batch/requirements.txt` — pinned `ansys-dpf-core==0.13.*`, `ansys-mechanical-core==0.11.*`, `pyvista`, `meshio`

**Integration with popup**
- `PostProcessDialog` gets a 3rd button `③ Run Deep Analysis` → spawns `mx_batch.py` headless → updates same `metadata.json` with new `deep_metrics` section → viewer auto-detects and adds Modal / HotSpot tabs
- Graceful degradation: if `mx_batch.py` exits non-zero ("DPF license error") → popup logs warning, deep tabs remain disabled

**Deliverables**
- Modal participation table (cumulative + per-mode-per-direction)
- 3D hot-spot viewer (PyVista headless render → PNG into viewer)
- MAC matrix vs previous run

**New dependencies**: `ansys-dpf-core`, `ansys-mechanical-core`, `ansys-dpf-server-2025-2-pre0` (Community), `pyvista`, `meshio`, `scikit-learn`

### Phase 3 — Parametric + LLM Action Recommendations (3-4 weeks)

**Files to create**
- `Mechanical/MXSimulator/batch/doe.py` — wraps `ansys-mechanical-core` parameter loop; outputs `runs.parquet`
- `Mechanical/MXSimulator/batch/surrogate.py` — `smt` Kriging fit
- `Mechanical/MXSimulator/postprocess/llm_advisor.py` — Claude tool-use loop:
  - Tool: `get_metric(name)` → returns scalar from JSON
  - Tool: `plot_image(name)` → returns base64 PNG path
  - Tool: `compare_with_baseline(name)` → returns Δ
  - Tool: `propose_geometry_change(parameter, delta)` → recorded as suggestion
  - System prompt grounds Claude in CAE conventions + project memory (`CLAUDE.md`)

**Deliverables**
- DOE driver, Pareto front plot
- "Engineer-grade" narrative report with prioritized action list (e.g., "Increase rib thickness by 0.5 mm — predicted Δmax_VM = -18%, confidence 0.82")

---

## 7. LLM-Driven Report Generation Specifics

### 7.1 Call shape
```python
import anthropic
client = anthropic.Anthropic()
resp = client.messages.create(
    model="claude-opus-4-7",
    max_tokens=8000,
    system=PROMPT_SYS_KO,  # Korean system prompt; CAE conventions
    tools=[T_SUMMARIZE_MODAL, T_FLAG_ANOMALIES, T_COMPARE_CASES, T_RECOMMEND],
    messages=[{"role": "user", "content": [
        {"type": "text", "text": "다음 결과를 분석하고 한국어로 보고서를 작성해줘."},
        {"type": "text", "text": json.dumps(metadata, ensure_ascii=False)},
        {"type": "image", "source": {"type":"base64", "media_type":"image/png", "data": b64_frf}},
    ]}],
)
```

### 7.2 Tool definitions (sketch)
- `summarize_modal(top_n_modes, freq_range)` → returns ranked list with classification (M4)
- `flag_anomalies(metric, threshold)` → returns nodes/bodies/modes that exceed
- `compare_cases(current_id, baseline_id, metrics[])` → returns Δ-table
- `recommend_action(target_metric, direction)` → returns ranked geometry-or-material changes with predicted effect (uses surrogate from Phase 3)
- `query_aidatahub(q)` → optional MCP-style lookup against project's `aidatahub` server for historical similar cases

### 7.3 Output format
- Markdown with embedded base64 PNG (or path-relative for HTML/PDF renderer)
- Top section: 3-sentence executive summary (Korean)
- Body sections: per-NS findings, plots, severity, recommended actions
- Footer: methodology + reproducibility hash (git SHA + metadata.json SHA)

### 7.4 Multi-language
- System prompt accepts `lang=ko|en|both`. For `both`, output is two-column Markdown.
- Project default = `ko` (user's primary language).

---

## 8. Tool / Library Recommendations

### Pure-CPython / popup leg
| Library | Use | Notes |
|---|---|---|
| `numpy >= 1.24` | basics | already used |
| `scipy >= 1.10` | FFT, welch, csd, find_peaks | already used |
| `pandas >= 2.0` | tables, parquet | new |
| `matplotlib >= 3.7` | plots | already used |
| `PyQt5 == 5.15.*` | GUI | already used; resist Qt6 migration to avoid PyInstaller churn |
| `fatpack >= 0.7` | rainflow, DEL, S-N | new, pure-Python |
| `jinja2 >= 3.1` | report templating | new |
| `anthropic >= 0.40` | LLM | new |
| `markdown-it-py` + `weasyprint` | MD → PDF | optional |

### PyAnsys leg (Phase 2+)
| Library | Pin | Notes |
|---|---|---|
| `ansys-mechanical-core` | `~= 0.11` for v252 | check release matrix |
| `ansys-dpf-core` | `~= 0.13` for v252 | |
| `ansys-dpf-post` | `~= 0.7` | higher-level wrapper over dpf-core |
| `ansys-dpf-server-2025-2-pre0` | Community tier | for Student fallback |
| `pyvista >= 0.43` | 3D mesh viz + headless render to PNG | |
| `meshio >= 5.3` | universal mesh I/O | |
| `scikit-learn >= 1.4` | DBSCAN, KMeans, IsolationForest | for hot-spot, mode-class, anomaly |
| `smt >= 2.0` | Kriging / Gaussian Process | DOE surrogate |
| `pymoo >= 0.6` | NSGA-II | Pareto |

### Should NOT add (yet)
- `plotly` — interactive but bloats PyInstaller by ~80 MB; defer until web-based viewer
- `dash` / `streamlit` — requires server; not aligned with current single-binary deployment

---

## 9. Risks

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| ANSYS Student forbids PyAnsys/DPF gRPC | M | H | Verify §4 first; Phase 2 is opt-in. Fallback: still works on user's Mechanical Enterprise installs |
| PyAnsys version drift vs ANSYS v252 | M | M | Pin in `requirements.txt`; CI install-and-import smoke test |
| gRPC startup adds 1-3s latency | H | L | Use only for batch; popup stays in-process ACT |
| Maintenance burden of 2 code paths | M | M | Shared JSON schema versioned (`schema_version` field); single canonical viewer |
| PyInstaller bundle bloat (fatpack, jinja2, anthropic) | L | M | Audit; consider `pyinstaller --exclude-module` for unused |
| LLM hallucination on engineering metrics | M | H | Tool-use only (no free-form numeric claims); LLM cites tool outputs verbatim; report includes "raw metrics" appendix |
| Korean text encoding in PyQt/matplotlib | L | L | Already handled in project (UTF-8 + Consolas fallback). Use Noto Sans CJK for matplotlib font |
| DPF Community mesh-size cap surprises Student users | M | M | Detect and surface in viewer with a friendly message: "Deep analysis disabled — mesh exceeds Community DPF limit (try Phase-1 metrics)" |
| Anthropic API key handling on engineer machines | M | M | Read from `%APPDATA%\MXDigitalTwin\anthropic.key` or env var; add a one-time prompt; warn never-to-commit |

---

## 10. Next Steps (this week)

1. **License probe (1 day)** — On the dev machine with ANSYS Student v252 installed:
   ```powershell
   python -m venv .venv-pyansys
   .\.venv-pyansys\Scripts\Activate.ps1
   pip install ansys-mechanical-core ansys-dpf-core ansys-dpf-post
   python -c "from ansys.dpf import core as dpf; print(dpf.SERVER)"
   python -c "from ansys.mechanical.core import launch_mechanical; m=launch_mechanical(version=252); print('OK'); m.exit()"
   ```
   Document outcome in `Mechanical/MXSimulator/postprocess/README_pyansys_probe.md`.

2. **Add 2 quick wins to MXPostViewer (1-2 days)** — entirely within Phase 1 scope:
   - `analyzer.py`: add `rainflow_count`, `damage_miner` using `fatpack`
   - `visualizer.py`: new tab "Fatigue" showing rainflow histogram + Miner D summary
   - Validates the analyzer/visualizer extension pattern.

3. **Spike the LLM reporter (1 day)** — single function `make_report(metadata_path) → report.md`:
   - Reads existing `metadata.json` from a real run
   - Single Claude call (no tools yet) → Korean Markdown out
   - Confirms `anthropic` SDK plays nicely with PyInstaller
   - Demonstrates value to stakeholders before committing to Phase 2/3

4. **Schema lock (½ day)** — Bump `metadata.json` to include `schema_version: "2.0"`, document expected fields. This unblocks parallel work on PyAnsys leg later without breaking the popup.

5. **Decision gate** — Review results of (1) with the team. Green-light Phase 2 only if PyAnsys works on Student or if Mechanical Enterprise licenses are assumed for power users.

---

## 진행 기록 (2026-07-05)

### §10.1 / §10.5 PyAnsys/DPF Student 라이선스 프로브 — 결정: **Phase 2 GREEN (DPF-over-.rst)**

ANSYS Student v252 / Python 3.13.7 에서 실측 (`postprocess/pyansys_probe.ps1` + `pyansys_probe2.py`,
전체 결과는 `postprocess/README_pyansys_probe.md`):

| 단계 | 결과 |
|---|---|
| venv + `pip install ansys-mechanical-core ansys-dpf-core ansys-dpf-post` | ✅ **Python 3.13 cp313 wheel 존재** (3.13 gap 없음). dpf-core 0.16.1 |
| `import ansys.dpf.core` | ✅ |
| **DPF 로컬 서버 기동** | ✅ `Ans.Dpf.Grpc.exe` (v252\aisol\bin\winx64), server_version 10.0 |
| **DPF .rst 필드 읽기** (변위/응력) | ✅ static.rst → 81 노드 disp, 64 stress 엔티티 |
| `launch_mechanical(version=252)` | ❌ "v252 does not support secure transport, update to SP03+" |

**핵심**: Phase 2 (modal participation / strain-energy field / MAC / hot-spot)는 전부 DPF 로 solved
`.rst` 를 읽는 것 → 이 시트에서 **작동**. launch_mechanical(라이브 PyMechanical 세션)만 SP03 벽에
막히는데 Phase 2 엔 불필요(사용자가 Mechanical/ACT 로 이미 solve 한 결과파일을 읽음). 결정: **Phase 2 를
DPF-over-.rst sidecar 로 착수**, ACT 확장이 `System.Diagnostics.Process` 로 launch(MXPostViewer 패턴),
`.venv-pyansys` 부재 시 popup-only 로 우아하게 폴백(opt-in).

### §10.4 metadata schema_version 2.0 — DONE
`main.py` PostProcessDialog export 에 `schema_version:"2.0"` + `generated_at`(ISO8601) + `units`
+ `source` 추가. `runner.py` 가 schema_version 을 읽어 미래 major 버전은 best-effort 경고 로드.

### §10.2 fatigue quick-win (rainflow + Miner) — DONE
`postprocess/analyzer.py`:
- `rainflow_count(signal, nbins)` — 검증된 `rainflow` PyPI 패키지(ASTM E1049) 우선, 부재 시 번들
  `_rainflow_astm` 폴백. **폴백을 rainflow 패키지와 1:1 대조 검증**(랜덤워크 500pt: 135 엔트리·모든
  range 동일, Miner D 완전 일치). ⚠️ **fatpack 은 채택 안 함** — 그 사이클 세트가 ASTM E1049 와 달라
  Miner D 가 ~16x 틀림(실측).
- `damage_miner(ranges, counts, A, m, endurance)` — power-law S-N `N=A·S^-m` Miner 누적손상.
  검증: 1000 cyc @100MPa (N=1e6) → D=1e-3, repeats=1000 정확.
`postprocess/visualizer.py`: **Fatigue 탭** 추가(rainflow 히스토그램 + S-N 상수/scale/endurance/bins
입력 + Miner D·수명 요약). headless 검증 완료. `requirements.txt`+`rainflow>=3.2`, `build_viewer.bat`
+`--hidden-import rainflow`.

### 다음 (미착수)
- §10.3 LLM 리포터 스파이크 (`make_report(metadata) → report.md`)
- Phase 1 나머지: popup 추출 확장 + Energy/Reactions 탭
- **Phase 2**: `Mechanical/MXSimulator/batch/mx_batch.py` (DPF sidecar) — 위 프로브로 GREEN 확정

---

*End of document.*
