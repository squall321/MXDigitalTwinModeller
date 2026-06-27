# CAD Reverse Engineering + LLM-Driven Design Modification — Feasibility & Ideas

> Status: exploratory feasibility document. Not a build plan. Decisions deferred to the team.
> Context: extension of MX Digital Twin Modeller (`d:\MXDigitalTwinModeller\`). Companion to `PHONE_DESIGNER_PLAN.md`.

---

## 1. Problem Definition

### 1.1 What is "complex CAD" in this project's context

The user wants to point the system at an existing CAD file — typically one of:

| Format | Content | Source |
|---|---|---|
| **STEP (.stp / .step)** | B-rep only (faces, edges, vertices, surface geometry). No feature tree, no parameters, no design intent. | Vendor part libraries, customer-supplied parts, Mechanical-side imports (already wired — see `Commands/ConformalMesh/`) |
| **IGES (.igs)** | Surface-only or B-rep. Even less structure than STEP. | Older CAD exports |
| **SpaceClaim `.scdoc`** | Native — has `Part / Component / DesignBody` tree but **not a parametric feature tree**. SpaceClaim is direct-modeling, so even native files have no "Extrude → Hole → Fillet" history. | Internal MX work |
| **Multi-body assemblies (.stp, .scdoc)** | Multiple bodies + (optional) assembly mate constraints. | Smartphone-style models with 20+ components |
| **Tessellated formats (.stl, .gltf)** | Triangles only — no analytic surfaces at all. | Teardown scans, hobbyist downloads |

For the MX project specifically, the realistic input is **STEP imported into SpaceClaim**, because the existing pipeline (`Commands/Export/ExportStepCommand.cs`, `Services/ConformalMesh/ConformalMeshService.cs` with `Document.Open(...)`) is already designed around STEP I/O.

### 1.2 Why this is genuinely hard

- **STEP is a "dead" boundary representation.** A 200-line specimen STEP file describes ~30 faces, ~70 edges, ~50 vertices with raw parametric surface equations (planes, cylinders, b-splines). There is no `Hole(diameter=5mm, depth=10mm)` record. There is `CYLINDRICAL_SURFACE(...)` plus a topology graph. Recovering "this is a hole" requires geometric inference.
- **"Make this thinner" is ambiguous without intent.** Given a slab, "thinner" could mean: shrink Z, shrink Y, shrink whichever face the user is looking at, shrink uniformly via Offset. Each requires identifying *which face is the "thickness direction"*. Humans do this instantly from context; LLMs need geometric primitives + visual grounding.
- **Feature recognition (AFR — Automatic Feature Recognition) is a 40-year-old open problem.** Solidworks, NX, Inventor all ship feature-recognition that works ~60–80% on machined parts and falls apart on cast/organic/freeform geometry. There is no "solved" approach.
- **"Design intent" is not in the file.** Two parts can be geometrically identical but have completely different intent (one is a flange where the bolt circle is the load-bearing feature; the other is decorative). No purely geometric analysis can recover that.
- **Topology changes break downstream pipelines.** The MX project relies on stable face IDs for `Named Selection` → Mechanical Face Pair NS → Tied Contact Check. Any reconstruction-and-rebuild round-trip *renames every face*. The simulation chain breaks.

### 1.3 What "reverse engineering" means in this proposal

Three different ambitions, ordered by difficulty:

1. **Annotation** (easy). Identify and label geometric primitives — "this face is planar", "these 4 edges form a fillet at r=2mm", "this is a through-hole d=5mm". No reconstruction. The original B-rep is preserved.
2. **Parametric *overlay*** (medium). Build a side-table of parameters that drive *specific identified features* — e.g. "the through-hole cluster A has parameter `hole_diameter`, currently 5mm". Modification rewrites those features in-place via SpaceClaim API ops, keeping the original body and most face IDs intact.
3. **Full parametric reconstruction** (hard / research-grade). Re-create the body from scratch as a build123d / SpaceClaim script. Original face IDs lost. Geometry diverges in tolerance bands. Topology may collapse on near-tangent surfaces.

This document advocates **Ambition 2** as the realistic target.

---

## 2. Industry State of the Art

### 2.1 Commercial CAD feature recognition

| System | Approach | Quality |
|---|---|---|
| **Solidworks FeatureWorks** | Rule-based (planar adjacency, cylinder pairs, fillet edge loops). Optional interactive (user confirms each feature). | ~70% on machined parts. Fails on castings, lofts, blends. |
| **Siemens NX "Synchronous Modeling"** | Direct-edit on B-rep + on-the-fly recognition. No tree reconstruction; instead operates on the dead body with feature-like ops. | Industry-leading. The closest thing to what we want. Proprietary. |
| **Autodesk Inventor "Convert to Feature"** | Hole/fillet recognition, then user clicks through. | Comparable to Solidworks. |
| **SpaceClaim itself** | Direct modeling. Has limited recognition: `IdentifyHoles(IdentifyHoleOptions)` (confirmed in `02_documented_surface.md` line 117) which extracts cylindrical hole features from a B-rep. No general AFR tree. Has interactive "Detect → Round", "Detect → Boss/Pocket" tools in UI, but not all exposed via API. | Hole detection is **directly available to this project**. Other features must be home-grown. |

The critical project-relevant finding: SpaceClaim's `DesignBody.IdentifyHoles(...)` returns a `Hole` collection. This is exactly the kind of programmatic primitive we need for Option B below. Beyond that, the documented API surface (`02_documented_surface.md`) does **not** expose a general feature-tree extractor.

### 2.2 Research: geometry → CAD program synthesis

- **DeepCAD (MIT, 2021)** — Transformer that emits CAD operation sequences (sketch + extrude) from point clouds. Works on simple parts. Limited to a fixed op vocabulary.
- **BrepNet (Autodesk, 2021)** — Graph neural net on B-rep face adjacency. Classifies faces into feature categories. Useful as a building block, not end-to-end.
- **GenCAD / CAD-LLM (various, 2023–2025)** — LLMs that emit CadQuery / OpenSCAD / Onshape-FeatureScript code from images or text. Demo-quality. Brittle on anything beyond textbook parts.
- **UV-Net, ABC Dataset benchmarks** — large datasets exist; nobody has a robust general solution.

### 2.3 LLM + CAD recent work

- **OpenSCAD-LLM** — Several open-source projects feed OpenSCAD source to an LLM, get edits back. Works because OpenSCAD source IS the parametric tree.
- **CadQuery + LLM** — Same pattern with CadQuery. The Phone Designer plan uses build123d, which is closely related.
- **Anthropic Computer Use on Fusion 360** — Demo only. Latency and click-through cost make it non-production.
- **ANSYS Discovery AI** — As of v252 there is no documented LLM-driven design modification feature. The Discovery "Search" assistant is for documentation, not geometry edits.
- **Onshape "FeatureScript + ChatGPT" plug-ins** — Community experiments. Onshape's parametric tree is API-accessible, so the LLM edits the tree directly. Quality is decent because the tree is the source of truth.

**Pattern across all working cases:** LLMs succeed when the *parametric program* (OpenSCAD source / CadQuery script / FeatureScript) is the editable artifact. They fail when asked to edit a dead B-rep directly.

---

## 3. Approach Options — Honest Tradeoffs

### Option A — Pure LLM (feed STEP text to Claude)

**Concept.** Read `part.stp` as plain text, send to Claude, ask "make this thinner".

**Why it doesn't work.**

- A 500KB STEP file is ~10k–100k lines of `CARTESIAN_POINT('',(...))` records. A 1MB STEP can blow the entire context window with one part.
- STEP entity IDs are opaque (`#12345`). The LLM cannot meaningfully "edit #12345" without understanding the topology graph.
- Cost: at ~5 tokens/line × 50k lines = 250k tokens per part, every iteration. Brutal.
- Output: even if the LLM produced edited STEP text, it would almost certainly be topologically invalid (open shells, non-watertight bodies). STEP is not a language designed for human or LLM editing.

**Verdict: do not pursue.** This is the strawman everyone tries first.

### Option B — Programmatic feature extraction + LLM semantic labeling

**Concept.** A C# / Python layer walks the B-rep, extracts structured primitives, and hands a *compact* JSON description to Claude. Claude labels and modifies.

**Pipeline:**

1. **Geometric extraction (deterministic, no LLM):**
   - Use `DesignBody.IdentifyHoles(IdentifyHoleOptions)` — gives hole positions, diameters, depths, axes
   - Classify each `Face` by `face.Shape.Geometry` type (`Plane`, `Cylinder`, `Cone`, `Sphere`, `Torus`, `NurbsSurface`) — pattern already used in this project at `Services/Contact/ContactDetectionService.cs` and `Core/Geometry/FaceNamingHelper.cs`
   - Detect fillets by finding faces that are `Cylinder` or `Torus` and tangent to two neighbours (use `designFace.AdjacentFaces` from documented surface)
   - Detect planar slabs / walls by clustering parallel planar faces with similar area + computing the gap
   - Detect ribs by long narrow planar faces between two larger planar faces
   - Detect bosses by finding cylindrical bodies extruded perpendicular to a planar parent face
   - Build a graph: `nodes = faces`, `edges = adjacency`, `properties = surface type + dimensions`

2. **Compact descriptor sent to LLM:**
   ```json
   {
     "body_count": 1,
     "bbox_mm": [120.5, 60.0, 8.0],
     "faces": {"planar": 6, "cylindrical": 14, "nurbs": 0},
     "features": {
       "holes": [
         {"id": "H1", "pos_mm": [10, 10, 0], "axis": "Z", "d_mm": 5, "depth_mm": 8, "through": true},
         {"id": "H2", "pos_mm": [110, 10, 0], "axis": "Z", "d_mm": 5, "depth_mm": 8, "through": true}
       ],
       "fillets": [
         {"id": "F1", "edge_count": 4, "radius_mm": 2.0, "location": "top_corners_z+"}
       ],
       "walls": [
         {"id": "W1", "thickness_mm": 8.0, "normal": "Z", "area_mm2": 7230}
       ]
     }
   }
   ```
   Tens of KB, not MB. Fits in any context.

3. **LLM semantic labeling pass:**
   - Prompt: "Given this part description and these renders, label features semantically."
   - Claude responds: `H1..H4 = mounting holes (corner pattern, M5 bolt clearance); F1 = top fillet for ergonomics; W1 = main wall thickness`.
   - This is what LLMs are good at — pattern naming, not geometry computation.

4. **Modification:**
   - User: "Make the wall thinner — 6mm."
   - LLM tool call: `set_feature_param(feature_id="W1", param="thickness_mm", value=6.0)`.
   - Backend computes which faces of W1 to offset (the "thickness face pair") and calls `body.OffsetFaces(faces, -2.0e-3)` (documented in `02_documented_surface.md` line 67).
   - Face IDs preserved.

**Verdict:** This is the right approach. Mature concept, leverages SpaceClaim's `IdentifyHoles` + `OffsetFaces`, doesn't require reconstruction.

### Option C — Vision-based (Claude with multi-view renders)

**Concept.** Render the part from 6 canonical angles + perspective. Feed images to Claude. Ask it to identify features.

**Strengths.**

- Excellent for *semantic* identification — "this looks like a phone bezel", "this is clearly a mounting flange".
- Doesn't require any geometry parsing infrastructure.
- Trivial to prototype (1 day).

**Weaknesses.**

- Vision tokens are expensive (each 1024×1024 image ≈ 1500 tokens). 6 views × 5 chat turns = ~45k tokens of images alone.
- Cannot give exact dimensions. "That hole looks 5mm" is unreliable.
- Cannot map "the hole in the corner" back to a specific `DesignFace` reliably — needs face-tagged renders (color-coded faces, hover overlays).
- Round-trip on every edit is slow (re-render → re-send → re-respond → re-execute).

**Verdict:** Powerful as a *complement*, weak as a standalone strategy. Use for semantic naming and for the user-facing chat ("show the user a rendered preview"), not as the geometric ground truth.

### Option D — Hybrid: programmatic + vision + LLM

**Concept.** Layer all three.

1. Geometric extractor produces structured JSON (Option B).
2. Renderer produces face-tagged image with IDs overlaid (Option C).
3. LLM gets *both* — structured features for exact dimensions, image for semantic understanding.
4. LLM-driven modification goes through tool calls on structured features.

**Verdict: this is the recommended path.** Geometric layer is the source of truth; vision is the semantic prior; LLM is the orchestrator.

### Option E — Pre-parametric (no reverse engineering at all)

**Concept.** Forget existing CAD. Only allow modification of models that were built parametrically *by us* from the start. This is what `PHONE_DESIGNER_PLAN.md` does.

**Strengths.**

- Easiest. Avoids the entire AFR problem.
- Phone Designer plan already exists, well-scoped.
- Face IDs / parameter tree are author-controlled.

**Weaknesses.**

- Only works for **models you authored**. Customer STEP files, vendor parts, scanned geometry — out of scope.
- Doesn't answer the user's original question ("can we read a complex CAD and let LLM modify it").

**Verdict:** Valid sibling, not a substitute. Both can coexist.

---

## 4. Recommended Architecture (Hybrid Option D)

```
┌──────────────────────────────────────────────────────────────────────┐
│  SpaceClaim Session (existing MX Add-In, V252)                       │
│  - STEP imported via Document.Open(...)                              │
│  - Body sitting in MainPart                                          │
└──────────────────┬───────────────────────────────────────────────────┘
                   │ in-process (C# Add-In) or out-of-process (Python via PyAnsys)
                   ▼
┌──────────────────────────────────────────────────────────────────────┐
│  Layer 1 — Geometric Extractor (deterministic C# / Python)           │
│  - Walk body.Faces, classify by face.Shape.Geometry type             │
│  - Run DesignBody.IdentifyHoles → Hole[]                             │
│  - Detect fillets (Cylinder/Torus faces tangent to neighbours)       │
│  - Detect walls (parallel planar face pairs, distance < threshold)   │
│  - Detect ribs, bosses via heuristic graph queries on AdjacencyMap   │
│  - Output: FeatureGraph (JSON-serialisable)                          │
└──────────────────┬───────────────────────────────────────────────────┘
                   ▼
┌──────────────────────────────────────────────────────────────────────┐
│  Layer 2 — Vision Annotator (one-shot, optional)                     │
│  - Render N face-tagged views (color-coded by feature group)         │
│  - Save as PNG, attach to LLM context                                │
└──────────────────┬───────────────────────────────────────────────────┘
                   ▼
┌──────────────────────────────────────────────────────────────────────┐
│  Layer 3 — Semantic Enrichment (LLM, one pass)                       │
│  - Input: FeatureGraph JSON + tagged renders                         │
│  - Claude returns: semantic labels per feature                       │
│    ("H1..H4 = corner mounting holes", "W1 = back wall")              │
│  - Stored as side-table; NOT modifying geometry                      │
└──────────────────┬───────────────────────────────────────────────────┘
                   ▼
┌──────────────────────────────────────────────────────────────────────┐
│  Layer 4 — Modification Engine                                       │
│  - LLM Tool definitions (constrained, schema-validated):             │
│    * set_feature_dimension(feature_id, param, value)                 │
│    * offset_wall_thickness(wall_id, new_thickness_mm)                │
│    * change_hole_diameter(hole_id, new_diameter_mm)                  │
│    * add_fillet(edge_id, radius_mm)                                  │
│    * delete_feature(feature_id)                                      │
│  - Tool implementation calls SpaceClaim API:                         │
│    * Body.OffsetFaces, Body.RoundEdges, Body.Subtract for new holes  │
│    * Snapshot/rollback wrapper around every change                   │
└──────────────────┬───────────────────────────────────────────────────┘
                   ▼
┌──────────────────────────────────────────────────────────────────────┐
│  Layer 5 — Diff / Preview / Confirm                                  │
│  - Re-extract FeatureGraph after change                              │
│  - Render before/after side-by-side                                  │
│  - User confirms → commit; rejects → rollback via WriteBlock undo    │
└──────────────────────────────────────────────────────────────────────┘
```

### 4.1 Why this preserves face IDs

The architecture *never reconstructs* the body. Every modification is a B-rep operation on the existing body (`OffsetFaces`, `Subtract`, `RoundEdges`). Face IDs that are not directly affected by the modification remain valid. This is critical for the MX downstream pipeline (Face Pair NS, Mechanical-side Named Selections).

### 4.2 Where build123d fits

build123d is preferred for **Option E** (Phone Designer — building from scratch). For *this* proposal (reverse engineering existing CAD), build123d is *not* the primary kernel — SpaceClaim's own modeler is, because:

- The body already lives in SpaceClaim
- Round-tripping through OCCT (build123d's backend) risks tolerance loss
- SpaceClaim's `IdentifyHoles`, `AdjacentFaces`, `OffsetFaces`, `RoundEdges` are exactly the primitives needed

That said, if the workflow goes out-of-process (Python-driven), `pythonocc-core` or `cadquery` can replace SpaceClaim's modeler. build123d is fine for prototyping the feature graph independently of SpaceClaim.

### 4.3 Concrete library/tool recommendations

| Layer | C# in-process | Python out-of-process |
|---|---|---|
| B-rep kernel | SpaceClaim API V252 (already in project) | `pythonocc-core` (OpenCascade Python bindings) or `cadquery` |
| Feature extraction | Custom; leverage `IdentifyHoles` + face classification (project pattern in `FaceNamingHelper.cs`) | `pythonocc-core` exposes OCCT's `BRepFeat`, `ShapeAnalysis` |
| STEP I/O | `Document.Open` (already used in `Services/ConformalMesh/ConformalMeshService.cs`) | `OCP.STEPControl_Reader` (via build123d) or `cadquery.importers.importStep` |
| Tessellation for render | `body.GetTessellation(faces, TessellationOptions)` (doc'd line 62) | `OCC.Core.BRepMesh_IncrementalMesh` |
| Vision render | OffScreen Three.js (in Phone Designer UI) or `pyvista` / `vedo` | `pyvista`, `vedo`, or send STEP to `freecad-python-api` headless |
| LLM client | `Anthropic.SDK` NuGet (C#) or `anthropic` Python SDK | `anthropic` |

**Recommendation:** in-process C# Add-In for the geometric extractor (fast, no IPC). Out-of-process Python FastAPI server for the LLM orchestration + UI (matches `PHONE_DESIGNER_PLAN.md` pattern, code reuse).

---

## 5. Difficulty Assessment (Honest)

| Component | Difficulty (1–10) | Notes |
|---|---|---|
| Reading complex STEP | **2** | `Document.Open` already in project. OCCT/pythonocc trivial. |
| Face classification by surface type | **3** | Project already does this (`FaceNamingHelper.cs`). RTTI on `face.Shape.Geometry`. |
| Hole detection | **3** | `IdentifyHoles` is built-in. |
| Fillet detection (cylindrical/toroidal faces tangent to neighbours) | **5** | Tangency tolerance heuristic; reliable on machined parts, brittle on freeform. |
| Wall / thickness detection (parallel planar face pairs) | **5** | Project's Conformal Mesh interface detection (`Services/ConformalMesh/`) does something analogous — solid foundation. |
| Rib / boss / pocket detection | **7** | Requires heuristic graph queries. Brittle outside the heuristic domain. |
| Pattern recognition (hole arrays, repeated features) | **6** | Clustering on position + parameters. Doable for grids; hard for organic patterns. |
| Generic free-form / organic feature recognition | **9** | E.g. "this is a phone bezel curve". Not solvable without ML or domain priors. |
| Semantic labeling via LLM | **3** | LLMs are great at this if given structured input. |
| Modification of identified features via API | **4** | `OffsetFaces`, `RoundEdges`, `Subtract` are mature. Edge cases when feature topology overlaps. |
| LLM tool-use orchestration | **3** | Standard pattern. Phone Designer plan already validated. |
| Robustness across CAD diversity | **9** | The killer. Each part is unique. Heuristics need domain restrictions. |
| Preserving face IDs through modification | **6** | Doable for offset/round; lost for `Subtract` of new features unless tracked. |
| Diff visualization | **4** | Render before/after. Existing `MeshVisualizationService` is a starting point. |

**The killer is robustness across CAD diversity.** Any given part will trigger heuristic failures somewhere. Realistic strategy is to (a) gracefully fall back to "unlabeled face" when heuristics fail, and (b) let the user manually annotate ambiguous faces.

---

## 6. Realistic Phase Plan

### Phase 0 — Spike & decide (3 days)

- Pick a target part: an existing MX specimen STEP (DMA 3-point bending) — known geometry, internal control.
- Hand-write the FeatureGraph for it (so we know what "right" looks like).
- Prototype extractor that produces something close to it.
- **Gate:** can we recover the hand-written graph automatically? If no, narrow scope.

### Phase 1 — Geometric extractor for narrow domain (1–2 weeks)

- Target domain: **prismatic machined parts** (specimens, brackets, plates). Not free-form.
- Implement, with full unit tests:
  - Surface-type classification (Plane / Cylinder / Cone / Sphere / Torus / Nurbs)
  - `IdentifyHoles` wrapper with normalised output
  - Wall pair detection (parallel planes, area overlap > 80%, distance < threshold)
  - Fillet detection (cylindrical/toroidal face tangent to two neighbour faces)
  - Bounding-box-relative feature coordinates
- Output: `FeatureGraph` JSON.
- Gate: produces correct graph on 5 specimen types + 3 customer STEP files chosen by user.

### Phase 2 — LLM semantic labeling + structured representation (1 week)

- Define Claude tool schema for *read-only* labelling (`label_feature`, `group_features`, `name_group`).
- Single-pass enrichment over the FeatureGraph.
- Persist labels alongside the SpaceClaim document (Named Selection per labelled feature group — leverages existing `Group.Create` infrastructure).
- Gate: a user can ask "what features are in this part?" and get a coherent natural-language answer.

### Phase 3 — Modification via parameter tweaks (no reconstruction) (2 weeks)

- Define modification tool set (~10 tools): `change_hole_diameter`, `offset_wall`, `change_fillet_radius`, `delete_feature`, `pattern_holes`, etc.
- Each tool implemented as a SpaceClaim API call inside `WriteBlock.ExecuteTask`.
- Snapshot/undo wrapper for safe rollback.
- LLM chat loop in a WinForms dialog or a side panel.
- Gate: 10 canonical natural-language commands work (analogue of Phone Designer Phase F scenarios).

### Phase 4 — Domain-restricted parametric reconstruction (1–2 months, optional)

- For **specific part families only** (smartphones, brackets, simple housings), generate a build123d / SpaceClaim reconstruction script.
- Use Phone Designer's `Stage` + `Pattern` infrastructure (now justifying the original sub-project investment).
- Gate: reconstruction round-trip preserves bbox ±0.1mm and major features in target family.

### Phase 5 — Generalize to arbitrary CAD (not a project — a research effort)

- This is not solvable by one engineer in months. Requires either: (a) significant ML work (BrepNet-style training), (b) domain-by-domain heuristic accumulation over years, or (c) license a commercial AFR library.
- **Recommendation: don't promise it.** Sell the project as "narrow-domain parametric edit" and grow domains over time.

---

## 7. Risks

| Risk | Likelihood | Severity | Mitigation |
|---|---|---|---|
| Reconstruction diverges from original — simulation pipeline (face IDs) breaks | High in Phase 4+ | Critical (whole CAE chain re-validates) | Stay in Phase 3 territory — modify in place, don't reconstruct |
| Modification ambiguity ("make it thinner") | Always | Medium | LLM must confirm with a follow-up question or render-and-preview before commit |
| LLM hallucinates non-existent features | Medium | High | Structured tools only — LLM cannot reference a feature ID that wasn't in the FeatureGraph. Schema validation rejects unknown IDs. |
| Heuristic failure on unusual parts | High | Medium | Graceful fallback to "unidentified feature", user manual annotation, never crash |
| Large STEP parsing slow (>30s) | Medium | Low | Cache FeatureGraph in `.scdoc` user-data; only re-extract on geometry change |
| Vision render costs balloon | Medium | Medium | Render once per session, only re-render on commit |
| `IdentifyHoles` doesn't handle countersinks / threaded holes | Likely | Low | Augment with custom cylinder-pair detector |
| WriteBlock undo doesn't actually roll back complex chains | Medium | High | Save body via `body.Save(BodySaveFormat.Binary, ...)` before each tool call — explicit checkpoint |
| LLM API cost on iteration-heavy sessions | Medium | Low | Prompt caching (Claude API native), small per-turn payloads, no re-sending FeatureGraph if unchanged |
| Mechanical-side Named Selection invalidation after modification | High if face IDs change | Critical for MX | Restrict Phase 3 ops to those that preserve adjacent face IDs (`OffsetFaces` does; `Subtract` of a new tool body does not for affected faces) |

---

## 8. Comparison with PHONE_DESIGNER_PLAN.md

| Axis | Phone Designer (Option E) | This Proposal (Hybrid D) |
|---|---|---|
| Starting point | Empty scene | Existing imported STEP |
| Knows design intent? | Yes — author of stages | No — must infer |
| Difficulty | Medium (well-scoped) | Hard (open problem) |
| Library | build123d (Python) | SpaceClaim API (C#) primary; Python optional |
| Face ID stability | High (we control everything) | High in Phase 3, low in Phase 4+ |
| Generality | One model family at a time (iPhone 12, then 15) | One domain at a time (specimens, then brackets) |
| Best for | New designs, exploration | Modifying customer/vendor CAD |
| Shared code with the other? | **Yes — modification tool definitions, LLM session management, snapshot/rollback, FastAPI server, vision render, diff UI** | |

**Concrete shared layer (if both pursued):**

```
shared-llm-cad/
├── tools/                 # Tool schema + dispatch
├── session.py             # Conversation context + snapshot stack
├── render/                # glTF / image rendering
├── diff/                  # Before/after visualisation
└── server/                # FastAPI app shell
```

This shared package would be the right artifact to extract first.

---

## 9. Concrete Quick-Win Suggestions

If full reverse engineering is too ambitious for the current sprint, here are shorter-path ideas that capture much of the user value at a fraction of the difficulty.

### 9.1 "Pick + Modify" — skip RE entirely

- User imports STEP, **clicks a face** in SpaceClaim ("this is my wall thickness face")
- System: `face.Shape.Area`, `face.Shape.Geometry as Plane`, find parallel partner face → "Identified: wall thickness 8mm between Face_42 and Face_57"
- LLM: "OK, what would you like to change it to?"
- User: "6mm" → `body.OffsetFaces([Face_42], -2e-3)`
- **Effort: ~3 days. Coverage: hundreds of customer parts.**

### 9.2 "Annotation Mode" — manual semantic labels, LLM-driven modification

- User imports STEP. Geometric extractor runs (no LLM). FeatureGraph populated.
- User manually labels groups via dialog: "These 4 holes = mounting pattern A. This face pair = back wall. This fillet = ergonomic edge."
- Labels saved as Named Selections (`Group.Create` — existing project infrastructure).
- LLM uses labels for modification thereafter.
- **Effort: ~1 week. Robust because the semantic ambiguity is resolved by human.**

### 9.3 Domain-restricted RE — one part family at a time

- Pick one family first: e.g., "smartphone back covers" or "rectangular brackets with corner holes"
- Hand-tune heuristics for that family — much higher success rate
- Grow library family-by-family as new use cases appear
- **Effort: 1 family per 2 weeks. Honest about its limits.**

### 9.4 "Natural-language Named Selection" — minimal AI

- Skip parametric modification entirely
- LLM only helps the user create Named Selections: "select all holes on the top face" → returns face/edge selection
- This piggybacks on `Group.Create` + Face Pair NS flow already in the project
- **Effort: ~1 week. Genuinely useful for the existing CAE workflow.**

### 9.5 "Specimen reverse-detection"

- Specific to this project: if user imports a STEP that's actually one of the supported specimens (ASTM E8, D638, …), recognise it and re-parameterise.
- Match against the parametric specimen library (`Services/TensileTest/`, `Services/CAI/`, etc.) by bbox + face count signature.
- "This looks like ASTM E8 Standard. Snap to canonical parameters?"
- **Effort: ~3–5 days. Pure win for the existing specimen domain.**

---

## 10. Next-Steps Recommendation

### Should you do this?

**Partial yes, with strict scope.** A full general-purpose CAD reverse-engineering + LLM-driven editor is a research project, not a deliverable. But a *useful subset* is achievable in the existing project's timeframe.

### Recommended actual Phase 1

Two weeks. Concrete deliverables:

1. **Geometric extractor** (C# in the existing Add-In) emitting `FeatureGraph` JSON:
   - Surface-type classification
   - `IdentifyHoles` wrapper
   - Parallel-plane wall detection
   - Fillet (cylindrical face tangent) detection
   - Output written to `.scdoc` document-userdata + a sidecar JSON
2. **A new ribbon command** `MX Modeller > Analyse > Extract Features` that runs the extractor on the active body and shows the FeatureGraph in a dialog.
3. **Quick-win 9.1 ("Pick + Modify")** integrated as a sibling command — user clicks a face, system identifies thickness pair, prompts for new value, applies `OffsetFaces`.

That gives the user a tangible demo *next sprint* and validates the geometric extractor before committing to the LLM layer.

### Recommended NOT to do (yet)

- Do not commit to "parametric reconstruction from arbitrary STEP" as a deliverable.
- Do not feed raw STEP to an LLM.
- Do not promise "make this design look like an iPhone" or any open-vocabulary geometric transform on imported CAD.

### Relationship to PHONE_DESIGNER_PLAN.md

Both proposals are valid and address different needs:

- **Phone Designer (Option E)** — best for new designs, exploration, marketing demos. *Greenlight as planned.*
- **This proposal (Option D, narrow scope)** — best for actual customer/vendor CAD modification in the existing CAE workflow. *Pilot as a 2-week spike before committing to a full phase plan.*

Extract a **shared `llm-cad-modify` package** (tools / session / render / diff) the moment a second consumer (this proposal) materializes — don't pre-extract.

---

## Appendix A — APIs already in the project that this proposal needs

From `01_used_in_project.md` and `02_documented_surface.md` (verified working):

- `face.Shape.Geometry is Plane / Cylinder / Cone / Sphere / Torus / NurbsSurface` — RTTI surface classification (already used in `Services/Contact/ContactDetectionService.cs` lines 227ff, `Core/Geometry/FaceNamingHelper.cs` lines 36, 72)
- `designFace.AdjacentFaces` — documented (`02_documented_surface.md` line 162); needed for fillet/rib detection
- `DesignBody.IdentifyHoles(IdentifyHoleOptions) → Hole[]` — documented (line 117); single most valuable AFR primitive in the API
- `Body.OffsetFaces(ICollection<Face>, double)` — documented (line 67); the workhorse for "make wall thinner"
- `Body.RoundEdges(...)` — verified (used in `Services/VoidCut/VoidCutService.cs:328`); for fillet modification
- `body.GetTessellation(faces, TessellationOptions)` — documented (line 62); needed for face-tagged renders
- `body.Save(BodySaveFormat.Binary, path)` — documented (line 87); used for snapshot/rollback
- `Group.Create(part, name, IDocObject[])` — verified (used in `Core/Geometry/FaceNamingHelper.cs:107,135`); for persisting semantic labels as Named Selections
- `WriteBlock.ExecuteTask(label, action)` — verified throughout project; mandatory transaction wrapper

Everything for Phase 1–3 is available. No new ANSYS licensing or API exploration needed.

## Appendix B — APIs that would help but are not documented in the project

- General-purpose feature recognition (`IdentifyFillets`, `IdentifyBosses`, `IdentifyPockets`) — **not present** in the documented API surface. Must be home-grown.
- Boolean op with face-ID tracking — `Body.Subtract` mutates in place but does not return a face-map. Need custom tracking via tessellation hash or pre/post face-count diff.
- Named-feature persistence in `.scdoc` — only Named Selections are exposed. Sidecar JSON file recommended for the FeatureGraph and semantic labels.
