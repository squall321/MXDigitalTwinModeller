# API Learnings, Gotchas, and Verification Status

Consolidated from project's existing reference docs and verification scripts.

**Sources synthesized:**
- `Mechanical/MXSimulator/ANSYS_Mechanical_API_Reference.txt` (1016 lines — exhaustive notes extracted from official ANSYS Student v252 XML docs and `BoltTools` reference code)
- `Mechanical/MXSimulator/verify_all_apis.py` (runtime existence checks)
- `Mechanical/MXSimulator/explore_geometry_api.py` (geometry/import sandbox)
- `Mechanical/MXSimulator/diagnose_act.py` (extension loading diagnostic)
- `Mechanical/MXSimulator/diagnose_extensions.py` (extension search-path diagnostic)
- `Mechanical/MXSimulator/diagnose_material_twin.py` (Material Twin pipeline diagnostic)
- `Mechanical/MXSimulator/TROUBLESHOOTING.md` (Material Twin troubleshooting)
- `lat.md/api-learnings.md` (memory-resident gotchas)

**Authoritative XML reference files (1st-party ground truth):**

| File | Purpose |
|---|---|
| `D:\Program Files\ANSYS Inc\ANSYS Student\v252\aisol\bin\winx64\Ansys.ACT.WB1.xml` | Core ACT — all `Add*` methods |
| `D:\Program Files\ANSYS Inc\ANSYS Student\v252\Addins\ACT\bin\Win64\Ansys.ACT.Interfaces.xml` | Interface definitions (`SelectionTypeEnum`, etc.) |
| `D:\Program Files\ANSYS Inc\ANSYS Student\v252\aisol\bin\winx64\Ansys.Mechanical.DataModel.xml` | DataModel types and enums |
| `D:\Program Files\ANSYS Inc\ANSYS Student\v252\Addins\ACT\preferences\Snippets_Mechanical.xml` | Official ANSYS Python snippets — GOLD STANDARD |
| `D:\Program Files\ANSYS Inc\ANSYS Student\v252\Addins\ACT\apis\Mechanical.py` | Python entry point — auto-loaded CLR refs |
| `D:\Program Files\ANSYS Inc\ANSYS Student\v252\scdm\SpaceClaim.Api.V252\API_Scripting_Class_Library.chm` | SpaceClaim CHM help |
| `D:\Program Files\ANSYS Inc\ANSYS Student\v252\Discovery\SpaceClaim.Api.V252\SpaceClaim.Api.V252.Scripting.xml` | SpaceClaim scripting API |
| `D:\Program Files\ANSYS Inc\ANSYS Student\v252\aisol\WBAddins\MechanicalExtensions\BoltTools\` | First-party `.py` extension — proven patterns |
| `D:\Program Files\ANSYS Inc\ANSYS Student\v252\aisol\DesignSpace\DSPages\Python\controllers\ansys_analysis_settings.py` | Official AnalysisSettings UI controller — exhaustive per-step API list |

---

## Known Wrong APIs — DO NOT USE

| Wrong | Correct | Notes |
|---|---|---|
| `face.GetFaceNormal(u, v)` | `face.NormalAtParam(u, v)` | `GetFaceNormal` does not exist on `IBaseGeoFace` in V252 (그건 `IDesignFace` extension) |
| `selection.Entities.Add(face)` | `selection.Entities = [face1, face2, ...]` | `Entities` is a settable list — assign whole list, do not call `.Add()` |
| `AnalysisSettings.RangeMaximum` | `AnalysisSettings.ModalRangeMaximum` | Modal-specific property name |
| `settings.StepEndTime = q` (per-step) | `settings.SetStepEndTime(1, Quantity(0.02, "sec"))` | Direct property cannot target a step; per-step method required. `step_index` is 1-based |
| `model.AddGeometryImport()` direct | Use `Geometry.AddGeometryImport()` + `Ansys.ACT.Mechanical.Utilities.GeometryImportPreference()` | Some standalone scripts cannot perform this — fall back to GUI File → Import |
| `TransientSolutionMethod.ModeSuperposition` enum set | (no scripting API) — set manually in GUI: Analysis Settings → Solution Method → Mode Superposition | `SolutionMethod` is not exposed in `WB1.xml` |
| Transient time controls without step index | All transient per-step controls require `(step_index, Quantity)` form | Same root cause as `StepEndTime` |
| `face.GetParam(point)` with assumed `(0.5, 0.5)` | Compute center via bounding box → `GetParam(center)` | `(0.5, 0.5)` is only valid for NURBS/parametric quads |
| Adding loads before `Analysis.Activate()` | `Analysis.Activate(); analysis.AddForce(...)` | BoltTools `PretensionHelper.py` confirms activation is required |
| `force.XComponent = Quantity(...)` direct | Use `force.XComponent.Inputs[0].DiscreteValues` + `.Output.DiscreteValues` | Tabular data requires both Inputs (time) and Output (magnitude) lists |
| `SetStoreResulsAtValue` (looks like typo) | Official method is spelled `SetStoreResulsAtValue` (sic) | Confirmed in `ansys_analysis_settings.py` — typo in the official API |

---

## Verified Working Patterns

All snippets below are extracted from `Snippets_Mechanical.xml`, `BoltTools/*.py`, or `ansys_analysis_settings.py` — first-party ANSYS code paths.

### 1. ACT runtime globals (auto-injected)

```python
ExtAPI         # MechanicalExtensionAPI
Model          # = ExtAPI.DataModel.Project.Model
DataModel      # = ExtAPI.DataModel
Tree           # Model Tree
Graphics       # Graphics context

model     = ExtAPI.DataModel.Project.Model
geometry  = model.Geometry
mesh      = model.Mesh
analyses  = model.Analyses
```

CLR references auto-loaded by `Mechanical.py`:
`Ansys.ACT.Core`, `Ansys.ACT.Interfaces`, `Ansys.ACT.WB1`,
`Ansys.Mechanical.Customization`, `Ansys.Mechanical.DataModel`, `Ansys.Mechanical.Interfaces`.

### 2. Geometry — Body enumeration

```python
from Ansys.Mechanical.DataModel.Enums import DataModelObjectCategory

# Preferred: recursive GetChildren
bodies = model.Geometry.GetChildren(DataModelObjectCategory.Body, True)

# Alt: walk Children
for part in model.Geometry.Children:
    for body in part.Children:
        ...

# Lowest level: IGeoAssembly
geo = ExtAPI.DataModel.GeoData
for part in geo.Parts:
    for body in part.Bodies:
        ...
```

### 3. Face normal (correct way)

```python
body     = model.Geometry.Children[0].Children[0]
geo_body = body.GetGeoBody()           # -> IBaseGeoBody
face     = geo_body.Faces[0]           # IBaseGeoFace

bbox    = face.GetBoundingBox(Matrix.Identity)
center  = bbox.Center
u, v    = face.GetParam(center)
normal  = face.NormalAtParam(u, v)
nx, ny, nz = normal[0], normal[1], normal[2]   # array-style access
```

Batch variant: `face.NormalsAtParams([u1,v1,u2,v2])`, `face.PointsAtParams([...])`.

### 4. Selection — SelectionManager

```python
from Ansys.ACT.Interfaces.Common import SelectionTypeEnum

sel_mgr   = ExtAPI.SelectionManager
selection = sel_mgr.CreateSelectionInfo(SelectionTypeEnum.GeometryEntities)
selection.Entities = [face1, face2, face3]   # list assignment, NOT .Add()
# alternatives: selection.Ids = [id1, id2]; selection.Id = single_id

sel_mgr.AddSelection(selection)
sel_mgr.NewSelection(selection)
sel_mgr.ClearSelection()
current = sel_mgr.CurrentSelection
```

Helper from `BoltTools/GeneralUtilities/SelectionInfoHelper.py`:

```python
def GeomSelInfo(Entities=None, Ids=None):
    SelInfo = ExtAPI.SelectionManager.CreateSelectionInfo(
        SelectionTypeEnum.GeometryEntities)
    if Entities is not None: SelInfo.Entities = Entities
    if Ids      is not None: SelInfo.Ids      = Ids
    return SelInfo
```

### 5. Named Selection — direct entity list

```python
ns = model.AddNamedSelection()
ns.Name          = "Contact_+Z"
ns.ScopingMethod = GeometryDefineByType.GeometryEntities
ns.SendToSolver  = True

sel = ExtAPI.SelectionManager.CreateSelectionInfo(SelectionTypeEnum.GeometryEntities)
sel.Entities = [face1, face2]
ns.Location  = sel
ns.Generate()

# Read back
bodies = list(ns.Entities)
```

### 6. Named Selection — Worksheet criteria

From `BoltTools/WorksheetNamesSelectionCreator.py`:

```python
from Ansys.Mechanical.DataModel import Enums

NS = model.AddNamedSelection()
NS.ScopingMethod = Enums.GeometryDefineByType.Worksheet
NS.SendToSolver  = False

WsRow = Ansys.ACT.Automation.Mechanical.NamedSelectionCriterion()
WsRow.Action     = Enums.SelectionActionType.Add      # Add | Filter | Convert
WsRow.EntityType = Enums.SelectionType.GeoFace        # GeoFace | GeoEdge | MeshNode | MeshElement | MeshElementFace
WsRow.Criterion  = Enums.SelectionCriterionType.Type  # Type | LocationX | LocationZ | NamedSelection | AllNodes
WsRow.Operator   = Enums.SelectionOperatorType.Equal  # Equal | RangeInclude
WsRow.Value      = 1                                  # 1=Plane, 2=Cylinder, 4=Cone
# RangeInclude: WsRow.LowerBound, WsRow.UpperBound, WsRow.CoordinateSystem

NS.GenerationCriteria.Add(WsRow)   # .Add() is correct here (different from Entities!)
NS.Generate()
```

### 7. Mesh — global + sizing

```python
from Ansys.Core.Units import Quantity

mesh = model.Mesh
mesh.ElementSize = Quantity(2.0, "mm")
mesh.GenerateMesh()
mesh.ClearGeneratedData()
mesh.PreviewSurfaceMesh()

sizing             = mesh.AddSizing()
sizing.Location    = body_or_named_selection
sizing.ElementSize = Quantity(1.0, "mm")

# Available controls: AddAutomaticMethod, AddInflation, AddRefinement,
# AddFaceMeshing, AddContactSizing, AddMatchControl, AddPinch, AddWeld,
# AddGasket, AddDeviation, AddMeshCopy
```

**GrowthRate gotcha:** There is no `mesh.GrowthRate` property — set it on a `Sizing` control instead.

### 8. Pressure — the only fully verified Snippets example

Direct quote from `Snippets_Mechanical.xml`:

```python
pressure = Model.Analyses[0].AddPressure()
part1    = Model.Geometry.Children[0]
body1    = part1.Children[0]
face1    = body1.GetGeoBody().Faces[0]

selection = ExtAPI.SelectionManager.CreateSelectionInfo(SelectionTypeEnum.GeometryEntities)
selection.Entities = [face1]
pressure.Location  = selection

pressure.Magnitude.Inputs[0].DiscreteValues = [Quantity("0 [s]"), Quantity("1 [s]")]
pressure.Magnitude.Output.DiscreteValues    = [Quantity("10 [Pa]"), Quantity("20 [Pa]")]
```

### 9. Force — tabular (components or vector)

```python
analysis.Activate()                       # MUST activate first
force = analysis.AddForce()
force.Name     = "Force_+Z"
force.Location = named_selection_or_selection_info

# Components mode
force.DefineBy = Ansys.Mechanical.DataModel.Enums.LoadDefineBy.Components
force.XComponent.Inputs[0].DiscreteValues = [Quantity("0 [s]"), Quantity("1 [s]")]
force.XComponent.Output.DiscreteValues    = [Quantity("100 [N]"), Quantity("200 [N]")]
# YComponent / ZComponent identical

# Vector mode
force.DefineBy = Ansys.Mechanical.DataModel.Enums.LoadDefineBy.Vector
force.Magnitude.Inputs[0].DiscreteValues = [Quantity("0 [s]"), Quantity("1 [s]")]
force.Magnitude.Output.DiscreteValues    = [Quantity("100 [N]"), Quantity("200 [N]")]
force.Direction = ...
```

### 10. Modal Analysis

```python
modal    = model.AddModalAnalysis()
settings = modal.AnalysisSettings

settings.MaximumModesToFind = 20
settings.ModalRangeMaximum  = Quantity(1000.0, "Hz")    # NOT RangeMaximum!
settings.ModalRangeMinimum  = Quantity(0.0, "Hz")
settings.LimitSearchToRange = True
settings.NumberOfModesToUse = 20
settings.KeepModalResults   = True
settings.Damped             = False
```

### 11. Transient Structural — per-step time controls

```python
transient = model.AddTransientStructuralAnalysis()
s         = transient.AnalysisSettings

step = 1   # 1-based!
s.SetStepEndTime     (step, Quantity(0.02,   "sec"))
s.SetInitialTimeStep (step, Quantity(0.0001, "sec"))
s.SetMinimumTimeStep (step, Quantity(0.0001, "sec"))
s.SetMaximumTimeStep (step, Quantity(0.0001, "sec"))
s.SetAutomaticTimeStepping(step, AutomaticTimeStepping.Off)
s.SetDefineBy        (step, TimeStepDefineByType.Time)
s.SetCarryOverTimeStep(step, False)

end_time = s.GetStepEndTime(step)

# Whole-analysis toggles
s.LargeDeflection = True
s.AutomaticTimeStepping = AutomaticTimeStepping.Off

# Damping
s.MassCoefficient       = ...   # Rayleigh alpha
s.StiffnessCoefficient  = ...   # Rayleigh beta
s.DampingRatio          = ...
s.ConstantDampingRatio  = ...

# Output toggles
s.Stress, s.Strain, s.CalculateVelocity, s.CalculateAcceleration, s.CalculateReactions
```

### 12. Quantity (units)

```python
from Ansys.Core.Units import Quantity

q1 = Quantity(1.0, "mm")
q2 = Quantity("1 [mm]")           # equivalent

# Common units: mm, Hz, N, sec, Pa, "" (unitless / Poisson)

time_vals  = [Quantity("0 [s]"), Quantity("0.01 [s]"), Quantity("0.02 [s]")]
force_vals = [Quantity("0 [N]"), Quantity("100 [N]"),  Quantity("50 [N]")]
```

### 13. Connections / Contact

```python
ConGroup = model.Connections.AddConnectionGroup()
Contact  = ConGroup.AddContactRegion()
Contact.Name = "Contact_Name"

t = ExtAPI.SelectionManager.CreateSelectionInfo(SelectionTypeEnum.GeometryEntities)
t.Entities = target_faces
Contact.TargetLocation = t

s = ExtAPI.SelectionManager.CreateSelectionInfo(SelectionTypeEnum.GeometryEntities)
s.Entities = source_faces
Contact.SourceLocation = s
```

### 14. STEP geometry import

```python
geometry    = ExtAPI.DataModel.Project.Model.Geometry
geom_import = geometry.AddGeometryImport()

import_pref = Ansys.ACT.Mechanical.Utilities.GeometryImportPreference()
import_pref.ProcessNamedSelections = True

geom_import.Import(
    step_path,
    GeometryImportPreference.Format.Automatic,
    import_pref
)
```

`Format.Automatic` handles `.stp`/`.step`. `explore_geometry_api.py` confirmed `model.GeometryImportGroup` does not exist in V252.

### 15. Bolt Pretension (advanced)

From `BoltTools/PretensionHelper.py`:

```python
Analysis.Activate()
Pretension = Analysis.AddBoltPretension()
Pretension.Location = sel_info

Pretension.SetDefineBy(stepIndex, BoltLoadDefineBy.Load)   # 1-based
mode = Pretension.GetDefineBy(stepIndex)

Pretension.Preload
Pretension.Preadjustment
Pretension.Increment
Pretension.CoordinateSystem = cs_obj

# Partial-edit tabular data
Vals = list(LoadComponent.Output.DiscreteValues)
Vals[stepIndex-1] = new_quantity
LoadComponent.Output.DiscreteValues = Vals

# Probe
Probe = Analysis.Solution.AddBoltPretensionProbe()
Probe.BoundaryConditionSelection = Pretension

# Escape hatch when API not exposed
Load.InternalObject.GeometryDefineBy        = 19
Load.InternalObject.BeamConnectionSelection = beam_id

# Properties path-based (ACT Extension context)
obj.Properties['Scope/DefineBy'].Value = 'ID_GeometrySelection'
obj.Properties['Scope/DefineBy/GeometrySelection'].Value = sel_info
```

`BoltLoadDefineBy` values: `Lock`, `Adjustment`, `Increment`, `Load`, `Open`.

### 16. Transaction batching

```python
with Transaction(True):
    NS = model.AddNamedSelection()
    NS.Location = sel_info
    # ...
```

### 17. Messaging

```python
ExtAPI.Application.Messages.Add(
    Ansys.Mechanical.Application.Message(
        'Your warning message text here.',
        MessageSeverityType.Warning))   # also .Error, .Info
```

### 18. DataModel queries

```python
ns_list      = ExtAPI.DataModel.GetObjectsByType(DataModelObjectCategory.NamedSelection)
pretensions  = ExtAPI.DataModel.GetObjectsByType(DataModelObjectCategory.BoltPretension)
cs_list      = ExtAPI.DataModel.GetObjectsByType(DataModelObjectCategory.CoordinateSystem)

objs = ExtAPI.DataModel.GetObjectsByName("Static Structural")
obj  = ExtAPI.DataModel.GetObjectById(some_id)

entity = ExtAPI.DataModel.GeoData.GeoEntityById(body_id)

first  = Tree.FirstActiveObject
active = Tree.ActiveObjects
sel    = ExtAPI.SelectionManager.CurrentSelection.Entities
```

### 19. AnalysisType comparison (string-safe)

```python
analysis.AnalysisType
str(analysis.AnalysisType) == 'Static'
# Enum values: Static, Transient, ExplicitDynamics, Modal, HarmonicResponse, ...
```

### 20. Type identity check

```python
PretensionType = Ansys.ACT.Automation.Mechanical.BoundaryConditions.BoltPretension
obj.GetType() == PretensionType
```

---

## AnalysisSettings — full per-step API surface

Confirmed from `D:\...\v252\aisol\DesignSpace\DSPages\Python\controllers\ansys_analysis_settings.py`.
`stepNumber` is **1-based**.

### Set methods
```
SetStepEndTime(n, Quantity)
SetAutomaticTimeStepping(n, AutomaticTimeStepping)
SetDefineBy(n, TimeStepDefineByType)
SetInitialTimeStep(n, Quantity) / SetMinimumTimeStep / SetMaximumTimeStep
SetInitialSubsteps(n, int) / SetMinimumSubsteps / SetMaximumSubsteps
SetCarryOverTimeStep(n, bool)
SetForceConvergenceType(n, ConvergenceToleranceType)
SetForceConvergenceValue(n, Quantity)
SetForceConvergenceTolerance(n, float)
SetForceConvergenceMinimumReference(n, Quantity)
SetMomentConvergenceType / SetDisplacementConvergenceType / SetRotationConvergenceType
SetLineSearch(n, LineSearchType)
SetStabilization(n, StabilizationType)
SetStabilizationMethod(n, StabilizationMethod)
SetStabilizationDampingFactor(n, float)
SetStabilizationEnergyDissipationRatio(n, float)
SetStabilizationFirstSubstepOption(n, StabilizationFirstSubstepOption)
SetStabilizationForceLimit(n, float)
SetStoreResultsAt(n, TimePointsOptions)
SetStoreResulsAtValue(n, value)   # official typo "Resuls"
```

### Get methods (parallel)
```
GetStepEndTime, GetAutomaticTimeStepping, GetDefineBy,
GetInitialTimeStep, GetMinimumTimeStep, GetMaximumTimeStep,
GetInitialSubsteps, GetMinimumSubsteps, GetMaximumSubsteps,
GetCarryOverTimeStep,
GetForceConvergenceType, GetForceConvergenceValue, GetForceConvergenceTolerance,
GetLineSearch, GetStabilization, GetStoreResultsAt
```

### Direct (non-step) properties
```
NumberOfSteps, StepEndTime, InitialTimeStep, MinimumTimeStep, MaximumTimeStep,
Stress, Strain, NodalForces, ContactMiscellaneous, GeneralMiscellaneous, ObjectId
```

### Confirmed enums
```
AutomaticTimeStepping       : ProgramControlled, On, Off
ConvergenceToleranceType    : ProgramControlled, On, Remove
LineSearchType              : ProgramControlled, On, Off
StabilizationType           : Off, Constant, Reduce
StabilizationMethod         : Energy, Damping
StabilizationFirstSubstepOption : No, OnNonConvergence, Yes
TimePointsOptions           : AllTimePoints, LastTimePoints, EquallySpacedPoints, SpecifiedRecurrenceRate
OutputControlsNodalForcesType : No, Yes
TimeStepDefineByType        : Substeps, Time
```

---

## Geometry — IBaseGeoFace / Edge / Vertex

```
face.Area, face.SurfaceType (Plane|Cylinder|Cone|Sphere|...)
face.Centroid (sequence — list(face.Centroid))
face.Points (flat — zip(*(iter(face.Points),) * 3))
face.Bodies / face.Edges / face.Vertices

face.NormalAtParam(u, v)           : array — normal[0..2]
face.NormalsAtParams([u1,v1,u2,v2])
face.PointAtParam(u, v) / PointsAtParams(...)
face.ParamAtPoint([x, y, z])

edge.Length / CurveType / StartVertex / EndVertex / Centroid / Faces / Bodies
vertex.X / Y / Z / Edges / Faces / Bodies

geo_assembly.Parts / AllParts / Unit / Dimension
```

---

## Verification Scripts in Project

All scripts live under `Mechanical/MXSimulator/`. Run from inside Mechanical Scripting Console (IronPython, `ExtAPI` auto-injected):

> File → Scripting → Open Script → `<script>` → Run

### `verify_all_apis.py` (254 lines)

**Purpose:** Single-shot smoke test that every API the codebase relies on actually exists at runtime.

**APIs probed (in order):**
1. `ExtAPI` global and type
2. `ExtAPI.DataModel.Project.Model` accessibility
3. `model.Geometry.GetChildren(DataModelObjectCategory.Body, True)`
4. `body.GetGeoBody()` → `geo_body.Faces.Count` → `face.GetFaceNormal(0.5, 0.5)` ← **known to fail** (script is meant to confirm the wrong-API hypothesis)
5. `model.AddNamedSelection()` (creates and deletes a probe NS)
6. `ExtAPI.SelectionManager.CreateSelectionInfo(SelectionTypeEnum.GeometryEntities)`
7. `model.Mesh` and `hasattr` checks for `ElementSize`, `GenerateMesh`, `AddSizing`, `Nodes`, `Elements`
8. `model.Analyses.AddModalAnalysis()` (creates and deletes if no analyses exist)
9. `from Ansys.Core.Units import Quantity` round-trip

**Expected output:** End-of-run summary table with `OK / FAIL / N/A` per API. A FAIL on `Face.GetFaceNormal` is the canonical signal — switch to `NormalAtParam`.

**Run requirements:** A loaded model with at least one body for face-probe steps; otherwise those steps report `N/A`.

### `explore_geometry_api.py` (101 lines)

**Purpose:** Sandbox to enumerate every `Geometry` / `Import`-named attribute reachable from `Model`, `Geometry`, and `Ansys.ACT.Mechanical.Utilities`. Confirmed that `model.GeometryImportGroup` does not exist in V252.

**Output:** Reflective `dir()` dumps.

**Run requirements:** Any open Mechanical session.

### `diagnose_act.py` (97 lines)

**Purpose:** Diagnose why an ACT extension fails to load.

**Checks:**
1. `ExtAPI.ExtensionManager.ProductVersion`
2. Deployment paths under `%APPDATA%` and `%LOCALAPPDATA%` — both `ANSYS` and `Ansys` casings — for `ACT\extensions\MXSimulator`
3. `ExtAPI.ExtensionManager.Extensions` enumeration (all loaded extensions, names, versions)
4. `MXSimulator` / `MX` substring scan with `Name`, `Version`, `Location`
5. Python interpreter version (`sys.version`, `sys.platform`)
6. Basic ACT API availability

**Run requirements:** Mechanical with scripting enabled. No model needed.

### `diagnose_extensions.py` (113 lines)

**Purpose:** Korean-language variant of `diagnose_act.py` focused on MX-family extensions.

**Additions vs. `diagnose_act.py`:**
- Lists every `extensions/` entry beginning with `MX` (DIR vs FILE annotated)
- Filters loaded list for `MX`, `Rocky`, `LSDYNA`
- Explicitly verifies `MXSimulator.xml` (binding XML) sits next to `MXSimulator/` (payload folder) at the deployment root
- Prints binding-XML size to detect zero-byte / partial-copy issues

**Run requirements:** Same as `diagnose_act.py`.

### `diagnose_material_twin.py` (136 lines)

**Purpose:** End-to-end diagnostic for the Material Twin pipeline (geometry detection → CSV parse → elastic calibration).

**Checks:**
1. `__file__` and resolved script directory
2. `calibration/` module folder existence + listing
3. `calibration_env/` venv existence + `Scripts/python.exe` presence
4. Import test: `utils.yaml_parser.parse_yaml`, `utils.csv_parser.parse_tensile_csv`, `elastic_calibrator.calibrate_elastic` (with `sys.path` prepend)
5. `ExtAPI.DataModel.Project.Model.Geometry` — probes `Source`, `SourceFile`, `DefinitionFile`; if path found, checks for sibling `specimen.yaml`. Dumps full `dir(geometry)`.
6. `model.Parameters` — counts and lists Workbench parameters (manual-input fallback uses `P1_GaugeLength`, `P2_GaugeWidth`, `P3_Thickness`, `P4_SpecimenType`)
7. `parse_tensile_csv` round-trip against `Examples/Steel_ASTM_E8_Elastic.csv` — prints point count + min/max of `displacement_mm` and `force_N`

**Run requirements:**
- Run inside Mechanical Scripting Console
- `calibration_env` set up via `setup_venv.bat` (one-time)
- Example CSV at `Examples/Steel_ASTM_E8_Elastic.csv`

**Expected output:** Each section reports OK/FAIL or path values. Use to localize Material Twin failures (yaml missing? Source None? venv missing? CSV malformed?).

---

## Common Errors and Fixes

### Canonical wrong-vs-right (from API reference)

| Error symptom | Root cause | Fix |
|---|---|---|
| `'face.GetFaceNormal' AttributeError` | Method does not exist in V252 | `face.NormalAtParam(u, v)` after `(u,v) = face.GetParam(face_center)` |
| `selection.Entities.Add` raises | `Entities` is not list-with-Add | `selection.Entities = [face1, face2]` |
| `AnalysisSettings.RangeMaximum` missing | Wrong property name | `settings.ModalRangeMaximum` |
| `AnalysisSettings.StepEndTime` can't be set per-step | Property has no step index | `settings.SetStepEndTime(1, Quantity(0.02, "sec"))` |
| Force tabular data not applied | Only one of Inputs/Output set | Set both `Inputs[0].DiscreteValues` and `Output.DiscreteValues` |
| `AddGeometryImport` AttributeError | API context-dependent | Fall back to GUI File → Import |
| `TransientSolutionMethod.ModeSuperposition` missing | Not exposed via scripting | Set in GUI: Analysis Settings → Solution Method → Mode Superposition |
| Load not applied after `Add*()` | Analysis not active | `Analysis.Activate()` before `AddForce()` / `AddBoltPretension()` |

### Material Twin pipeline (from TROUBLESHOOTING.md)

**Specimen detection fails:**
- No `specimen.yaml` next to `.scdoc` → re-create specimen in SpaceClaim, save document
- Geometry imported as STEP (not SpaceClaim Component) → either add SpaceClaim Component upstream, OR manually create Workbench Parameters `P1_GaugeLength`, `P2_GaugeWidth`, `P3_Thickness`, `P4_SpecimenType`
- `Geometry.Source` / `SourceFile` / `DefinitionFile` None → ensure Geometry component connected, save WB project, Update

Quick probe:
```python
g = ExtAPI.DataModel.Project.Model.Geometry
for a in ['Source', 'SourceFile', 'DefinitionFile']:
    print(a, "=", getattr(g, a, 'N/A'))
```

**CSV parsing fails:** Required format — header `displacement_mm,force_N`, ≥10 data points, UTF-8/ASCII.

**Calibration fails:**
- `Virtual environment not found!` → run `setup_venv.bat` from `Mechanical\MXSimulator\` (one-time)
- "Please detect specimen first." / "Please select CSV data first." → run Steps 1/2 first
- `Insufficient data points in elastic region (0~0.20%)` → collect denser early data, or adjust `max_elastic_strain` in source
- `Unrealistic Young's modulus: X MPa` → verify units: displacement [mm], force [N] → E [MPa]; cross-section area [mm²]; gauge length [mm]

**Material creation fails:**
- Calibration not yet run → Step 4 first
- Name collision → Overwrite or rename
- Engineering Data unreachable → ensure Mechanical Model is open

**Healthy run end-state (Steel example):** Gauge 50 mm, Width 12.5 mm, Thickness 3 mm; 100 CSV points; E ≈ 200,065 MPa, R² ≈ 0.999992; material `Structural_Steel` shows in Engineering Data.

---

## Environmental / Build Gotchas

- **SpaceClaim DLL lock:** SpaceClaim holds `MXDigitalTwinModeller.dll`; close before rebuild. `Mechanical/kill_ansys.bat` kills ANSYS processes.
- **Build configs:** `MXDigitalTwinModeller.csproj` defines only `Debug` and `Release`. `Debug-V252` mentions in README are stale.
- **MSBuild:** `C:\Program Files (x86)\Microsoft Visual Studio\2019\BuildTools\MSBuild\Current\Bin\MSBuild.exe`
- **Output:** `C:\ProgramData\SpaceClaim\AddIns\MXDigitalTwinModeller\V252\`
- **WinForms DataGridView in GroupBox:** `Dock = DockStyle.Fill` misbehaves — use explicit `Size` and `Location`.
- **Shared Core DLL load (IronPython):** guard with `os.path.exists`:
  ```python
  import clr, os
  dll = os.path.join(os.path.dirname(__file__), "bin", "MXDigitalTwinModeller.Core.dll")
  if os.path.exists(dll):
      clr.AddReferenceToFileAndPath(dll)
      from MXDigitalTwinModeller.Core.Spatial import SpatialIndex
  ```
- **ACT extension search paths:**
  - `%APPDATA%\ANSYS\v252\ACT\extensions\<Name>\` and `%LOCALAPPDATA%\…`
  - Both casings (`ANSYS` and `Ansys`)
  - Binding XML `<Name>.xml` lives at parent `extensions\`; payload folder beside it.

---

## Quick Reference — Common Enums

```python
from Ansys.Mechanical.DataModel.Enums import (
    DataModelObjectCategory, GeometryDefineByType, LoadDefineBy)

DataModelObjectCategory.Body / Face / Edge / Vertex / NamedSelection /
                        Analysis / Mesh / CoordinateSystem / Contact /
                        BoltPretension

GeometryDefineByType.Worksheet / GeometryEntities
LoadDefineBy.Components / Vector

from Ansys.ACT.Interfaces.Common import SelectionTypeEnum
SelectionTypeEnum.GeometryEntities / MeshNodes / MeshElements
```

---

## Complete Minimal Workflow (verified)

```python
from Ansys.Mechanical.DataModel.Enums import (
    DataModelObjectCategory, GeometryDefineByType, LoadDefineBy)
from Ansys.ACT.Interfaces.Common import SelectionTypeEnum
from Ansys.Core.Units import Quantity

# 1. Model
model = ExtAPI.DataModel.Project.Model

# 2. Bodies
bodies = model.Geometry.GetChildren(DataModelObjectCategory.Body, True)

# 3. Face-normal pass
positive_z_faces = []
for body in bodies:
    geo = body.GetGeoBody()
    if geo is None: continue
    for face in geo.Faces:
        center = face.GetBoundingBox(Matrix.Identity).Center
        u, v = face.GetParam(center)
        n = face.NormalAtParam(u, v)
        if n[2] > 0.99:
            positive_z_faces.append(face)

# 4. Named Selection
ns = model.AddNamedSelection()
ns.Name = "Contact_+Z"
sel = ExtAPI.SelectionManager.CreateSelectionInfo(SelectionTypeEnum.GeometryEntities)
sel.Entities = positive_z_faces
ns.Location = sel

# 5. Mesh
mesh = model.Mesh
mesh.ElementSize = Quantity(2.0, "mm")
mesh.GenerateMesh()

# 6. Modal
modal = model.AddModalAnalysis()
modal.AnalysisSettings.MaximumModesToFind = 20
modal.AnalysisSettings.ModalRangeMaximum  = Quantity(1000.0, "Hz")
modal.AnalysisSettings.LimitSearchToRange = True

# 7. Transient
transient = model.AddTransientStructuralAnalysis()
s = transient.AnalysisSettings
s.SetStepEndTime    (1, Quantity(0.02,   "sec"))
s.SetInitialTimeStep(1, Quantity(0.0001, "sec"))
s.SetMinimumTimeStep(1, Quantity(0.0001, "sec"))
s.SetMaximumTimeStep(1, Quantity(0.0001, "sec"))

# 8. Force (activate first!)
transient.Activate()
force = transient.AddForce()
force.Location = ns
force.DefineBy = LoadDefineBy.Components
force.ZComponent.Inputs[0].DiscreteValues = [Quantity("0 [s]"), Quantity("0.02 [s]")]
force.ZComponent.Output.DiscreteValues    = [Quantity("0 [N]"), Quantity("100 [N]")]
```

---

## Cross-references

- `mechanical/01_used_in_project.md` — actual usage catalog (this codebase's call sites)
- `mechanical/02_documented_surface.md` — full XML-derived API surface
- `lat.md/api-learnings.md` — memory-resident short-form gotchas
- `Mechanical/MXSimulator/ANSYS_Mechanical_API_Reference.txt` — exhaustive 1016-line notes
- `Mechanical/MXSimulator/TROUBLESHOOTING.md` — Material Twin pipeline troubleshooting
- Authoritative XML references: see top of this document

## Loft 솔리드 = Boolean 지뢰 (2026-07-10 probe_g25 실측)

`LoftProfiles`+캡`Fuse`로 만든 솔리드는 Volume/IsClosed/PieceCount 모두 정상으로 보고되지만
**Boolean 피연산자로 쓰면 오동작**: `core.Unite({loftDome})`이 합집합 대신 "돔−코어"(면 위
돌출부만)를 반환 (역방향 unite도 동일). 원인은 fuse된 셸의 내부 방향성 반전으로 추정.
→ **로프트 결과는 독립 바디로만 사용**하고, 불리언에 들어갈 프러스텀/돔은
`ExtrudeProfile + TaperFaces`(둘 다 불리언 안전 검증)로 재구축할 것.
재현: Test/RE_SelfTest/probe_g25.py.
