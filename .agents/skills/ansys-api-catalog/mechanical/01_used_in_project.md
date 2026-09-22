# Mechanical ACT API — Used in This Project (Verified Working)

Inventory of every ANSYS Mechanical ACT API call used in this codebase. All entries are PROVEN working (project builds + runs).

## 🚨 Top 5 Gotchas (real bugs in this project)
1. `face.NormalAtParam(u, v)` is correct on GeoFace from `body.GetGeoBody().Faces[i]`. `GetFaceNormal(u,v)` (in `verify_all_apis.py`) targets a different face object — do NOT use on GeoFace.
2. `AnalysisSettings.ModalRangeMaximum`, NOT `RangeMaximum`.
3. Transient time-step API is **methods**, not properties: `SetStepEndTime(1, Quantity(...))`, `SetInitialTimeStep`, `SetMinimumTimeStep`, `SetMaximumTimeStep`. First arg = step number (1-based).
4. `Quantity` accepts BOTH `(value, unit_str)` and `("2.5 [s]")` — both used here.
5. Material filter needs C# generic: `eng_data.GetChildren[Ansys.ACT.Automation.Mechanical.Material](True)`.

## Imports & Top-Level

```python
import clr; import System; import Ansys
clr.AddReference("PresentationFramework"); clr.AddReference("PresentationCore")
clr.AddReference("WindowsBase");  clr.AddReference("System.Xaml")
clr.AddReference("System.Windows.Forms")
from Ansys.Mechanical.DataModel.Enums import DataModelObjectCategory, GeometryDefineByType
from Ansys.Core.Units import Quantity
from Ansys.ACT.Interfaces.Common import SelectionTypeEnum
# Inline FQNs used:
#   Ansys.ACT.Interfaces.Common.SelectionTypeEnum.GeometryEntities    (main.py:448, 1554, 1608)
#   Ansys.Mechanical.DataModel.Enums.LoadDefineBy.Components          (main.py:1042)
#   Ansys.Mechanical.DataModel.Enums.ContactType.Bonded               (main.py:3956)
#   Ansys.ACT.Automation.Mechanical.TransientSolutionMethod.ModeSuperposition (main.py:986)
#   Ansys.ACT.Automation.Mechanical.Material                          (main.py:5205, 5217)
```

## ExtAPI Root
- `ExtAPI.DataModel.Project.Model` — root Model (main.py:325, 414, 660, 841, 964, 1280, 1540, 1814, 1878, 2330, 2842, 3072, 3438, 3493, 3909, 4148, 4186, 5202)
- `ExtAPI.DataModel.Project.Model.Geometry` (main.py:4596)
- `ExtAPI.DataModel.Project.Model.Materials` (main.py:5202)
- `ExtAPI.DataModel.Project.Model.Parameters` (main.py:4561)
- `ExtAPI.DataModel.Project.ProjectDirectory` (main.py:4874)
- `ExtAPI.DataModel.MeshDataByName('Global')` (main.py:2334)
- `ExtAPI.SelectionManager.CreateSelectionInfo(SelectionTypeEnum.GeometryEntities)` (main.py:447, 1553, 1607, 1941, 3769, 3943, 3949)
- `ExtAPI.ExtensionManager.ProductVersion` (diagnose_act.py:18)
- `ExtAPI.ExtensionManager.Extensions` → iter of `ext.Name`, `ext.Version`, `ext.Location`
- `ExtAPI.Application` (existence-check)

## Selection / SelectionInfo
- `SelectionTypeEnum.GeometryEntities`
- `sel.Entities = [face_or_body, ...]` — **list assignment, NOT `.Add()`** (main.py:450, 1556, 1610, 1944, 3771, 3945, 3951)
- `sel.Entities` (iterate) (main.py:3703)
- `sel.Ids` — region IDs (main.py:2801, 2834, 3022, 3722)

## Geometry / Body / Face
- `model.Geometry` — root; `geometry.Source` / `geometry.SourceFile` / `geometry.Children` (main.py:4609-4624)
- `model.Geometry.GetChildren(DataModelObjectCategory.Body, True)` (main.py:326, 1281, 1910, 2504, 2687, 2843, 3501, 3599, 3912, 4163, 4189)
- `model.Geometry.UpdateGeometry()` (main.py:4157, 4197)
- `body.Name`, `body.Id`, `body.Material`
- `body.Suppressed` (read+write, main.py:3502, 3600, 3901, 4152, 4163, 4190-4191)
- `body.GetGeoBody()` → IGeoBody (main.py:487, 515, 1362, 1393, 1940, 2852, 3707, 3725, 3771, 3989, 4021)
- `geo_body.Faces` (.Count, [i], iterable)
- `geo_body.BoundingBox` with `.MinCorner.X|Y|Z` / `.MaxCorner.X|Y|Z` (main.py:4002, 4027, 4083-4086)
- `face.NormalAtParam(u, v)` → `.X .Y .Z` (main.py:495, 524, 1370, 1402, 2882, 4109-4110)
- `face.Centroid` → `.X .Y .Z` (main.py:494, 523, 1369, 1401, 2883, 4111-4112)
- `face.PersistentId` (main.py:2854); `face.Id` (main.py:3727)

## Named Selection
- `model.NamedSelections.Children` (main.py:418, 862)
- `model.NamedSelections.GetChildren(DataModelObjectCategory.NamedSelection, True)` (main.py:2741)
- `model.AddNamedSelection()` (main.py:439, 1545, 1599)
- `ns.Name`, `ns.ScopingMethod = GeometryDefineByType.GeometryEntities` (main.py:442, 1548, 1602)
- `ns.Location = sel` (main.py:451, 1557, 1611)
- `ns.Location.Ids` (main.py:2801, 2834)
- `ns.Generate()` (main.py:2798); `ns.Delete()` (verify_all_apis.py:135)

## Analyses
- `model.Analyses` (iter) (main.py:636, 842, 968, 998, 3441)
- `model.GetChildren(DataModelObjectCategory.Analysis, True)` (main.py:1815)
- `model.AddModalAnalysis()` (main.py:661, 3447)
- `model.AddTransientStructuralAnalysis()` (main.py:974)
- `analysis.Activate()` (main.py:1018); `analysis.Delete()` (main.py:3444)
- `analysis.Solution` (main.py:3460, 3494)
- `analysis.GetChildren(DataModelObjectCategory.Solution, True)` (main.py:1882)
- `model.Link(transient, modal)` (main.py:1003) — fallback `transient.SetLinkedAnalysis(modal, 0)` (main.py:1009)

## AnalysisSettings
- `modal.AnalysisSettings.MaximumModesToFind = N` (main.py:666, 3453)
- `modal.AnalysisSettings.ModalRangeMaximum = Quantity(maxHz, "Hz")` (main.py:667)
- `ts.SetStepEndTime(1, Quantity(t,"sec"))` / `SetInitialTimeStep` / `SetMinimumTimeStep` / `SetMaximumTimeStep` (main.py:979-982)
- `ts.SolutionMethod = TransientSolutionMethod.ModeSuperposition` (main.py:985-988)

## Loads
- `analysis.AddForce()` (main.py:1037)
- `force.Location = ns` (named selection direct, main.py:1039)
- `force.DefineBy = LoadDefineBy.Components` (main.py:1041)
- Per-axis tabular: `force.{X|Y|Z}Component.Inputs[0].DiscreteValues = [Quantity(...)...]` (time), `.Output.DiscreteValues = [...]` (value) (main.py:1050-1057)

## Contact / Connections
- `model.Connections` (main.py:3073, 3616, 3934)
- `connections.GetChildren(DataModelObjectCategory.ContactRegion, True)` (main.py:3074, 3617)
- `connections.AddContactRegion()` (main.py:3935)
- `contact.SourceLocation` / `contact.TargetLocation` (set+get, main.py:3092-3094, 3636, 3644, 3946, 3952)
- `contact.ContactType = ContactType.Bonded` (main.py:3956); `str(cr.ContactType)` for classification (main.py:3126, 3130, 3140)
- `cr.FrictionCoefficient` (main.py:3142)
- Defensive probes: `contact.ContactGeometry`, `contact.TargetGeometry` (main.py:3637, 3645)

## Solution & Results
- `solution.Status` → `str()` then check 'Done'/'Solved' (main.py:1892)
- `solution.Solve(True)` blocking (main.py:3461)
- `solution.EvaluateAllResults()` (main.py:1968, 3514, 3762)
- `solution.AddTotalDeformation()` (main.py:1925, 1946, 3497)
- `solution.AddEquivalentStress()` (main.py:1928, 1950)
- `solution.AddEquivalentElasticStrain()` (main.py:3757)
- `result.Location = sel` (main.py:1948, 1952, 3772)
- `result.Mode = n` (main.py:3518, 3759)
- `result.Frequency.Value` (main.py:3521)
- `result.Maximum` → Quantity (main.py:3776)
- `result.MaximumOfMaximumOverTime` → Quantity (main.py:1976-1977)
- `result.Delete()` (main.py:3538, 3797)

## MeshData (Solved Mesh)
- `mesh_data = ExtAPI.DataModel.MeshDataByName('Global')` (main.py:2334)
- `.NodeCount`, `.ElementCount` (main.py:2337-2338)
- `.NodeById(nid)` (1-based, main.py:2459), `.NodeByIndex(i)` (0-based fallback, main.py:2479)
- `.ElementById(eid)` (main.py:2522, 2942), `.ElementByIndex(i)` (main.py:2548, 2955)
- `.GetNodeIdsFromRegionIds(region_ids)` → Dictionary<int, IList<int>> (main.py:2809, 3027) — KEY mapping API
- `node.Id`, `node.X`, `node.Y`, `node.Z` (main.py:2452)
- `elem.Id`, `elem.NodeIds`, `elem.ElementType` (main.py:2531-2536)
- Defensive: `elem.PartId` / `BodyId` / `Part` / `Body` (main.py:2525)
- `model.Mesh` — exists; attributes probed: `ElementSize`, `GenerateMesh`, `AddSizing`, `Nodes`, `Elements` (verify_all_apis.py:178-182)

## Engineering Data / Materials
- `eng_data = model.Materials` (main.py:5202)
- `eng_data.GetChildren[Ansys.ACT.Automation.Mechanical.Material](True)` — **generic-typed** (main.py:5205, 5217)
- `eng_data.AddMaterial()` (main.py:5223)
- `mat.SetPropertyByName("Young's Modulus", Quantity(E, "MPa"))` (main.py:5227)
- `mat.SetPropertyByName("Poisson's Ratio", Quantity(nu, ""))` — empty unit string for dimensionless (main.py:5228)
- `mat.SetPropertyByName("Density", Quantity(rho, "kg m^-3"))` (main.py:5231)
- `mat.Delete()` (main.py:5219)

## Parameters (Workbench)
- `params = model.Parameters` (main.py:4561) — iterable
- `params["P1_GaugeLength"].Value.Value` → double (main.py:4565)
- `params["P4_SpecimenType"].Expression` → raw string (main.py:4568)

## Quantity / Units
- `Quantity(value, "Hz"|"sec"|"MPa"|"mm"|"kg m^-3"|"")` two-arg form
- `Quantity("0.001 [s]")` / `Quantity("5.0 [N]")` single-string form (main.py:1046-1047)
- `qty.Value`, `qty.Unit` (verify_all_apis.py:235)
- Defensive `_safe_float` parses `"2.34 [mm]"` → `2.34` (main.py:1846-1858)

## DataModelObjectCategory enum values used
`Body`, `NamedSelection`, `Analysis`, `Solution`, `ContactRegion`

## Tree / Hierarchy helpers
- `obj.Name` (read/write); `obj.Delete()` (universal); `obj.Children` (direct iter); `obj.GetChildren(cat, recursive)`; `hasattr(obj, 'X')` defensive probes everywhere

## Workflow → APIs map

| Workflow | Key APIs |
|---|---|
| NS dialog (face dir grouping) | `GetGeoBody`, `Faces`, `NormalAtParam`, `Centroid`, `AddNamedSelection`, `ScopingMethod`, `SelectionInfo.Entities`, `Location=sel` |
| Modal create | `AddModalAnalysis`, `AnalysisSettings.MaximumModesToFind`, `ModalRangeMaximum` |
| Transient CSV scenario | `AddTransientStructuralAnalysis`, `SetStepEndTime`/`SetInitialTimeStep`/`SetMin`/`SetMax`, `SolutionMethod=ModeSuperposition`, `model.Link` / `SetLinkedAnalysis`, `AddForce`, `XComponent.Inputs[0]/Output.DiscreteValues` |
| Post-process Top-N | `analysis.GetChildren(Solution,True)`, `AddTotalDeformation`+`AddEquivalentStress` per body, `EvaluateAllResults`, `MaximumOfMaximumOverTime` |
| K-file export | `MeshDataByName('Global')`, `NodeById/NodeByIndex`, `ElementById/ElementByIndex`, `GetNodeIdsFromRegionIds`, `ns.Generate`, `ns.Location.Ids`, `cr.SourceLocation.Ids` |
| Tied-contact check | `AddModalAnalysis`, `Solve(True)`, `AddTotalDeformation.Mode/Frequency`, `AddEquivalentElasticStrain.Mode/Maximum`, BFS over `ContactRegion.SourceLocation/TargetLocation` |
| Material twin | `eng_data.GetChildren[Material](True)`, `AddMaterial`, `SetPropertyByName` |
| Specimen detect | `model.Parameters[name].Value.Value`, `.Expression` |

## Project files containing ANSYS APIs

- `main.py` — all 9 dialogs; primary surface (~140 unique API calls)
- `verify_all_apis.py` — pure existence-probing harness
- `explore_geometry_api.py` — `dir()`-based discovery (no real calls)
- `run_cap_vibration.py` — thin `execfile` loader
- `diagnose_act.py`, `diagnose_extensions.py` — only `ExtAPI.ExtensionManager`
- `diagnose_material_twin.py` — Parameters + Geometry attr discovery

**CPython-only (NO ANSYS APIs)**: `calibration/runner.py`, `calibration/elastic_calibrator.py`, `postprocess/*.py`.

## Top 10 Project-Specific Gotchas (consolidated)

1. `NormalAtParam(0.5,0.5)` on GeoFace, NOT `GetFaceNormal`.
2. `ModalRangeMaximum`, NOT `RangeMaximum`.
3. Time steps are methods (`SetStepEndTime(1, Qty)`), not properties.
4. Generic Material filter: `GetChildren[Ansys.ACT.Automation.Mechanical.Material](True)`.
5. Both Quantity ctors valid: `Quantity(2.0,"mm")` AND `Quantity("2.0 [mm]")`.
6. `SelectionTypeEnum` is in `Ansys.ACT.Interfaces.Common`, NOT `DataModel.Enums`.
7. Must call `ns.Generate()` before reading `ns.Location.Ids`.
8. `result.Maximum` returns Quantity — guard with `hasattr(val,'Value')`.
9. Solution status check is string-contains: `'Done' in str(sol.Status)`.
10. `model.Link(a,b)` not guaranteed; fallback `transient.SetLinkedAnalysis(modal, 0)`.
