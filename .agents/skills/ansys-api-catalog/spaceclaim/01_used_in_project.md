# SpaceClaim API — Used in This Project (Verified Working)

Inventory of every SpaceClaim API class/method called in this codebase. All entries are PROVEN working (project builds + runs).

**Methodology:** Grepped all `Services/*/*.cs`, all `Commands/*/*.cs`, `Core/Geometry/*.cs`, `Core/IO/*.cs`, `Core/Commands/*.cs`, `UI/Dialogs/*.cs`, `AddIn.cs`. ~100 distinct classes/methods/properties/enum-values.

## Namespaces in active use

- `SpaceClaim.Api.V252.Geometry` — `Point`, `Vector`, `Direction`, `Plane`, `Frame`, `Matrix`, `Box`, `Interval`, `Circle`, `Ellipse`, `Line`, `NurbsCurve`, `Cylinder`, `CurveSegment`, `ITrimmedCurve`, `Curve`, `Profile`, `RectangleProfile`, `CircleProfile`, `ControlPoint`
- `SpaceClaim.Api.V252.Modeler` — `Body`, `Edge`, `DesignBody`, `DesignFace`, `DesignEdge`, `DesignCurve`, `DesignMesh`, `Part`, `IPart`, `Document`, `Window`, `Group`, `NamedSelection`, `Component`, `IDocObject`, `LibraryMaterial`, `DocumentMaterial`, `MaterialProperty`, `MaterialPropertyId`, `IHasMaterial`, `EdgeRound`, `FixedRadiusRound`, `VariableRadiusRound`, `WriteBlock`, `Animation`, `Moniker`, `PartExportFormat`
- `SpaceClaim.Api.V252.Scripting.Commands` — `InitMeshSettings`, `CreateMesh`, `CreateEdgeSizeControl`, `SetBodyMeshType`, `MeshMethods`
- `SpaceClaim.Api.V252.Scripting.Commands.CommandOptions` — `CreateMeshOptions`, `CreateEdgeSizeControlOptions`, `ElementShapeType`, `MidsideNodesType`, `SizeFunctionType`, `PhysicsType`, `BlockingDecompositionType`
- `SpaceClaim.Api.V252.Scripting.Commands.CommandResults` — `CreateMeshResult`, `InitMeshSettingsResult`
- `SpaceClaim.Api.V252.Scripting.Selection` — `Selection`
- `SpaceClaim.Api.V252.Analysis` — `ElementShape`, `MidsideNodes`, `SizeFunction`
- `SpaceClaim.Api.V252.Extensibility` — `AddIn`, `IExtensibility`, `ICommandExtensibility`, `IRibbonExtensibility`, `CommandCapsule`, `Command`, `ExecutionContext`

## Body / Geometry Creation

- **`Body.ExtrudeProfile(Profile, double depth)` → `Body`** — extrude 2D profile to a solid (meters). Most-used API.
  - `Core/Geometry/BodyBuilder.cs:29,41,53`; `Services/TensileTest/SpecimenModelingService.cs:160,305,327,419,609,651,767`; `Services/CAI/CAISpecimenService.cs:90,129,176,239,259`; `Services/VoidCut/VoidCutService.cs:273,296,318` (+14 other services)
- **`Body.CreatePlanarBody(Plane, IEnumerable<ITrimmedCurve>)` → `Body`** — planar shell body
  - `Services/Simplify/SimplifyService.cs:229` (mid-surface for solid→shell)
- **`body.Copy()` → `Body`** — deep copy
  - `Services/Laminate/SolidLaminateService.cs:182,212`; `Services/BendingFixture/BendingFixtureService.cs:321,322,376,377`; `Services/VoidCut/VoidCutService.cs:157`
- **`body.Subtract(Body[] tools)`** — boolean subtract (mutates target)
  - `Services/TensileTest/SpecimenModelingService.cs:425,428,463,469,657`; `Services/Fatigue/FatigueSpecimenService.cs:114,118,189,211,259,260,323,327,332,406,443,453,454`; `Services/CAI/CAISpecimenService.cs:262`; `Services/VoidCut/VoidCutService.cs:538`
- **`body.Unite(Body[])`** — boolean union — `Services/VoidCut/VoidCutService.cs:532`
- **`body.Intersect(Body[])`** — boolean intersect
  - `Services/Laminate/SolidLaminateService.cs:219`; `Services/BendingFixture/BendingFixtureService.cs:323,378`; `Services/VoidCut/VoidCutService.cs:535`
- **`body.Transform(Matrix)`** — apply 4×4 transform — `Services/VoidCut/VoidCutService.cs:274,297,319,593`
- **`body.RoundEdges(IEnumerable<KeyValuePair<Edge, EdgeRound>>)`** — fillet edges — `Services/VoidCut/VoidCutService.cs:328`
- **`designBody.Delete()`** — remove from part
  - `Services/Simplify/SimplifyService.cs:133,232`; `Services/Laminate/SolidLaminateService.cs:194`; `Services/VoidCut/VoidCutService.cs:640`; `Services/GmshMesher/MeshVisualizationService.cs:98`
- **`designBody.Shape`** → `Body` — underlying modeler body (used throughout)
- **`body.Shape.GetBoundingBox(Matrix) → Box`** — AABB
  - `Services/BendingFixture/BendingFixtureService.cs:60,327`; `Services/Mesh/MeshSettingsService.cs:37`; `Services/ConformalMesh/ConformalMeshService.cs:399,797`
- **`face.Shape.Area`** (m²) — `Services/Contact/ContactDetectionService.cs:251,363`; `Services/ConformalMesh/ConformalMeshService.cs:295,307`; `Services/Laminate/SolidLaminateService.cs:79`; `Services/Simplify/SimplifyService.cs:280`
- **`body.Shape.Volume`** (m³) — `Services/VoidCut/VoidCutService.cs:567`
- **`face.Shape.IsReversed`** — orientation flag
  - `Services/Contact/ContactDetectionService.cs:247,359`; `Services/Laminate/SolidLaminateService.cs:83`; `Services/Simplify/SimplifyService.cs:276`; `Services/ConformalMesh/ConformalMeshService.cs:789`

## Profile / Sketch

- **`new Profile(Plane, IEnumerable<ITrimmedCurve>)`** — closed-loop profile (used in nearly every specimen service)
- **`new RectangleProfile(Plane, double w, double h)`** — `Core/Geometry/BodyBuilder.cs:40`; `Services/VoidCut/VoidCutService.cs:272,317`; `Services/Laminate/RectangularLaminateService.cs:113`
- **`new CircleProfile(Plane, double radius)`** — `Core/Geometry/BodyBuilder.cs:52`
- **`CurveSegment.Create(Point, Point) → ITrimmedCurve`** — straight segment
- **`CurveSegment.Create(Circle, Interval) → ITrimmedCurve`** — arc segment
- **`CurveSegment.Create(Ellipse, Interval)` / `CurveSegment.Create(NurbsCurve, Interval)`** — `Services/Laminate/SurfaceLaminateService.cs:186,196,217`
- **`Circle.Create(Frame, double radius) → Circle`**
- **`Ellipse.Create(Frame, double majorR, double minorR)`** — `Services/Laminate/SurfaceLaminateService.cs:185`
- **`NurbsCurve.CreateFromControlPoints(NurbsData, ControlPoint[])`** — `Services/Laminate/SurfaceLaminateService.cs:195`
- **`NurbsCurve.CreateThroughPoints(bool closed, IEnumerable<Point>, double tol)`** — `Services/Laminate/SurfaceLaminateService.cs:211`
- **`new ControlPoint(Point, double weight)`** — `Services/Laminate/SurfaceLaminateService.cs:194`
- **`ITrimmedCurve.Geometry`**, **`.StartPoint`**, **`.EndPoint`**, **`.Bounds`**
- **`Curve.Evaluate(double t).Point`** — point at parameter
- **`NurbsCurve.Parameterization.Bounds`** — `Services/Laminate/SurfaceLaminateService.cs:214`
- **`Edge.Bounds`, `.Geometry`, `.StartPoint`, `.EndPoint`**

## Plane / Frame / Point / Direction / Vector / Matrix / Interval / Box

- **`Plane.PlaneXY`** (static)
- **`Plane.Create(Frame) → Plane`**
- **`plane.Frame`** (Origin + DirX/Y/Z)
- **`Frame.Create(Point origin, Direction dirX, Direction dirY) → Frame`**
- **`frame.Origin`, `frame.DirX`, `frame.DirY`, `frame.DirZ`** — accessors
- **`Direction.DirX`, `Direction.DirY`, `Direction.DirZ`** (static) — extensive use
- **`Direction.Create(double x, double y, double z) → Direction`**
- **`direction.UnitVector`** → `Vector`
- **`Point.Origin`** (static) — `Services/Compression/CompressionSpecimenService.cs:105`
- **`Point.Create(double, double, double)`** — used in nearly every service
- **`Point.X`, `Point.Y`, `Point.Z`** — accessors
- **`Point - Point → Vector`** and **`Point + Vector → Point`** (operator overloads)
- **`Vector.Create(double, double, double)`**
- **`Vector.Dot(Vector, Vector) → double`** (static)
- **`Vector.Cross(Vector, Vector) → Vector`** (static)
- **`vector.Magnitude`**
- **`vector.X/.Y/.Z`** and unary `-vector`
- **`Matrix.Identity`** (static)
- **`Matrix.CreateTranslation(Vector) → Matrix`** — `Services/VoidCut/VoidCutService.cs:274,297,319`
- **`Matrix.CreateScale(double scale, Point center) → Matrix`** — `Services/VoidCut/VoidCutService.cs:593`
- **`Box.MinCorner`, `Box.MaxCorner`** → `Point`
- **`Interval.Create(double start, double end) → Interval`**
- **`interval.Start`, `interval.End`**

## Surface / Curve types (RTTI checks via `is`/`as` on `face.Shape.Geometry`)

- **`Plane`** — `Core/Geometry/FaceNamingHelper.cs:36,72`; `Services/Contact/ContactDetectionService.cs:227ff`; `Services/ConformalMesh/ConformalMeshService.cs:289,706`
- **`Cylinder`** (`cyl.Frame.DirZ`, `cyl.Frame.Origin`) — `Services/Contact/ContactDetectionService.cs:319,328-329`
- **`Circle`** (`circle.Radius`, `circle.Frame`)
- **`Line`** — `Services/Laminate/SurfaceLaminateService.cs:163`
- **`Ellipse`** (`.Frame`, `.MajorRadius`, `.MinorRadius`)
- **`NurbsCurve`** (`.ControlPoints`, `.Data`, `.Parameterization`)

## DesignBody / DesignFace / DesignEdge / DesignMesh / DesignCurve

- **`DesignBody.Create(Part, string name, Body) → DesignBody`** — `Core/Geometry/BodyBuilder.cs:70`; `Services/VoidCut/VoidCutService.cs:360,440`
- **`designBody.Faces`**
- **`designBody.Name`** (read/write)
- **`designBody.Parent`** — `Services/Compression/CompressionSpecimenService.cs:171,201`; `UI/Dialogs/LaminateDialog.cs:208`
- **`designFace.Shape`** → `Face`; **`designFace.Shape.Geometry`** → `Surface`
- **`designFace.Edges`** → `IEnumerable<DesignEdge>`
- **`designFace.Parent as DesignBody`** — `Commands/BendingFixture/ApplyBendingFixtureCommand.cs:80`; `UI/Dialogs/LaminateDialog.cs:208`
- **`designEdge.Shape`** → `Edge`
- **`DesignCurve.Create(Part, CurveSegment)`** — `TestCreateBlock.cs:93-108` (test only)
- **`DesignMesh.Create(Part, string name, float[] vertices, int[] facetIndices) → DesignMesh`** — `Services/GmshMesher/MeshVisualizationService.cs:81`
- **`designMesh.Delete()`** — `Services/GmshMesher/MeshVisualizationService.cs:98`

## Part / Document / Window / Component

- **`Window.ActiveWindow`** → `Window` — `Core/Commands/BaseCommandCapsule.cs:29,37,45`
- **`window.Document`** → `Document`
- **`window.ZoomExtents()`** — `UI/Dialogs/TensileSpecimenDialog.cs:523`; many dialogs
- **`window.ActiveContext.Selection`** — `UI/Dialogs/ContactDetectionDialog.cs:539`
- **`window.ActiveContext.SingleSelection`** — `Commands/BendingFixture/ApplyBendingFixtureCommand.cs:74`
- **`document.MainPart`** → root `Part`
- **`document.Path`**
- **`document.Materials`** (Dictionary<string,DocumentMaterial>) — `Services/Material/MaterialService.cs:122`
- **`document.SaveAs(string path)`** — `Services/TensileTest/SpecimenModelingService.cs:1037`
- **`Document.Open(string path, OpenOptions)` → `Document`** — `Services/ConformalMesh/ConformalMeshService.cs:130,143`
- **`part.Bodies`**
- **`part.Components`**
- **`component.Content`** → `IPart` (read inside `WriteBlock`)
- **`IPart`** interface — used for recursive body traversal
- **`part.Groups`** → groups/Named Selections — `Services/Contact/ContactDetectionService.cs:2393,2435`
- **`part.Export(PartExportFormat fmt, string path, bool overwrite, ExportOptions opts)`** — `Commands/Export/ExportStepCommand.cs:58`
- **`PartExportFormat.Step`** — only enum value used

## Selection / Named Selection / Groups

- **`Selection.Create(IDocObject[]) → Selection`** — `Services/Mesh/MeshSettingsService.cs:71,228`; `Services/ConformalMesh/ConformalMeshService.cs:527,620,640,669`
- **`Selection.Empty() → Selection`** — `Services/Mesh/MeshSettingsService.cs:72`; `Services/ConformalMesh/ConformalMeshService.cs:621,670`
- **`Group.Create(Part, string name, IEnumerable<IDocObject>)`** — create Named Selection
  - `Core/Geometry/FaceNamingHelper.cs:107,135`; `Services/CAI/CAISpecimenService.cs:141`; `Services/Joint/JointSpecimenService.cs:385`; `Services/VoidCut/VoidCutService.cs:614`
- **`group.Name`** — `Services/Contact/ContactDetectionService.cs:2395,2437`
- **`NamedSelection.Delete(string[] names)`** (static) — `Services/Contact/ContactDetectionService.cs:2418`

## Materials

- **`LibraryMaterial.Library`** (`IDictionary<string, LibraryMaterial>`) — `Services/Material/MaterialService.cs:128,140`
- **`DocumentMaterial.Copy(Document, LibraryMaterial)`** → `DocumentMaterial` — `Services/Material/MaterialService.cs:134,142`
- **`docMaterial.Name`** (r/w), **`docMaterial.Density`** (SI) — `Services/Material/MaterialService.cs:50,51,135,143`
- **`docMaterial.SetProperty(MaterialProperty)`** — `Services/Material/MaterialService.cs:171,176,180,187,195,202,210,219`
- **`new MaterialProperty(MaterialPropertyId, string, double, string unit)`** — id-enum ctor
- **`new MaterialProperty(string idString, string, double, string unit)`** — string-id ctor (e.g. CTE = `"General.ThermalExpansion.Isotropic.CTE"`)
- **`MaterialPropertyId`** values used: `Density`, `ElasticModulus`, `PoissonsRatio`, `ShearModulus`, `TensileStrength`, `ThermalConductivity`, `SpecificHeat`
- **`IHasMaterial`** interface; **`hasMat.Material`** (r/w `DocumentMaterial`)

## Mesh (Scripting commands)

- **`InitMeshSettings.Execute(PhysicsType.Structural, null) → InitMeshSettingsResult`** — `Services/Mesh/MeshSettingsService.cs:67`; `Services/ConformalMesh/ConformalMeshService.cs:583`
- **`new CreateMeshOptions()`** with fields `.ElementSize` (m), `.SolidElementShape`, `.MidsideNodes`, `.GrowthRate`, `.SizeFunctionType`
- **`CreateMesh.Execute(Selection bodies, Selection empty, CreateMeshOptions, null) → CreateMeshResult`**
- **`createMeshResult.Success`**
- **`new CreateEdgeSizeControlOptions()`** with `.ElementSize`
- **`CreateEdgeSizeControl.Execute(Selection edges, CreateEdgeSizeControlOptions, null)`**
- **`SetBodyMeshType.Execute(Selection, Selection, BlockingDecompositionType, ElementShapeType, null)`**
- **`ElementShapeType`** values: `Tetrahedral`, `Hexahedral`, `QuadDominant`, `Triangle`
- **`MidsideNodesType`** values: `Dropped`, `Kept`, `BasedOnPhysics`
- **`SizeFunctionType`** values: `CurvatureAndProximity`, `Curvature`, `Proximity`, `Fixed`
- **`BlockingDecompositionType`** values: `Free`, `Automatic`
- **`PhysicsType.Structural`**
- **`MeshMethods.SaveDYNA / SaveANSYS / SaveAbaqus / SaveFluentMesh / SaveCGNS(string path)`** — mesh export

## Fillets / Rounds

- **`new FixedRadiusRound(double radius)` : `EdgeRound`** — `Services/VoidCut/VoidCutService.cs:325`
- **`new VariableRadiusRound(...)`** — `TestCreateBlock.cs:138` (test only)
- **`KeyValuePair<Edge, EdgeRound>`** for `body.RoundEdges(...)`

## Transaction wrapper (CRITICAL)

- **`WriteBlock.ExecuteTask(string label, Action body)`** — **REQUIRED** for all geometry mutation, body deletion, group creation, and `Component.Content` access
  - Used in **every** specimen/fixture/mesh/contact/void-cut dialog and command
  - Notable: `TensileSpecimenDialog.cs:512,544,583`; `BendingSpecimenDialog.cs:977,1014,1055`; `ApplyBendingFixtureDialog.cs:101,179,431,468,501`; `LaminateDialog.cs:641,674,693,737`; `ContactDetectionDialog.cs:329,683,737,771`; `ConformalMeshDialog.cs:308,327,380,429`; `BatchPipelineDialog.cs:505,562,623,680,720`; `MaterialDialog.cs:540`; `VoidCutDialog.cs:724,759,824`; `GmshMesherDialog.cs:395,442`; `SimplifyDialog.cs:352`

## Add-In Framework (Extensibility)

- **`class : AddIn, IExtensibility, ICommandExtensibility, IRibbonExtensibility`** — `AddIn.cs:34`
- **`Connect() / Disconnect() / Initialize() / GetCustomUI()`** overrides — `AddIn.cs:123-193`
- **`class : CommandCapsule`** — `Core/Commands/BaseCommandCapsule.cs:9`
- **`CommandCapsule(string commandName, string text, Image image, string hint)`** ctor
- **`OnInitialize(Command) / OnUpdate(Command) / OnExecute(Command, ExecutionContext, Rectangle)`** overrides — every `Commands/*/*Command.cs`
- **`command.IsEnabled = bool`** — every `OnUpdate`
- **`Command.GetCommand(string)`** — `TestCreateBlock.cs:34` (test only)
- Ribbon XML returned from `GetCustomUI()` — `AddIn.cs:200+`

## Notes / Gotchas

- **All geometry is in meters internally**. `Core/Geometry/GeometryUtils.MmToMeters(double)` converts mm UI input before `Body.ExtrudeProfile`.
- This codebase derives face normals from **`plane.Frame.DirZ.UnitVector`** with `face.Shape.IsReversed` flip — NOT from `NormalAtParam(u,v)` calls. See `Services/Contact/ContactDetectionService.cs:246-247` and `Services/Simplify/SimplifyService.cs:275-277`.
  - **NOTE**: This differs from Mechanical side where `NormalAtParam` is needed. SpaceClaim uses face-type-based extraction.
- **`Component.Content` must be accessed inside a `WriteBlock.ExecuteTask`** (comment at `UI/Dialogs/ApplyBendingFixtureDialog.cs:91,98`).
- `Body.ExtrudeProfile` always extrudes **+Z** from the profile plane. Centering on Z uses `body.Transform(Matrix.CreateTranslation(0, 0, -d/2))`.
- **Boolean ops mutate the target body in place** (`target.Shape.Subtract(new Body[] { tool })`).
- The Modeler `NamedSelection.Delete(string[])` is the **static** deletion API; **creation** goes through `Group.Create(part, name, IDocObject[])`.
- `Animation.*` and `Moniker<T>` exist in the API but only `TestCreateBlock.cs` uses them — not in production services.

## CAD Primitives (2026-07-10 g23 게이트로 신규 검증 ✓ — CadPrimitivesService)

| API | 검증 내용 | 주의 |
|---|---|---|
| `Body.SweepProfile(Profile, ICollection<ITrimmedCurve> path)` | 원호 경로 = **회전체**(와셔 π(R²−r²)h 정확, 부분각/임의축 OK); 폴리라인+코너아크 경로 = 파이프(Pappus 정확) | 경로는 탄젠트 연속 권장(코너는 아크 블렌딩), 프로파일 평면 ⊥ 경로 시작 탄젠트 |
| `Body.LoftProfiles(IList<ICollection<ITrimmedCurve>>, bool periodic, bool ruled)` | 원/사각/다각 섹션 로프트 (프러스텀 체적 정확) | ⚠️ **열린 시트 반환(Volume=0)** → 끝단 `CreatePlanarBody` 캡 2장 + `Fuse(caps,false,null)` 스티치하면 솔리드화. `IsClosed`로 확인 |
| `Body.CreatePlanarBody(Plane, ICollection<ITrimmedCurve>)` | 로프트 캡 용 평면 시트 생성 | |
| `body.Fuse(ICollection<Body>, bool skipProblem, Tracker)` | 시트 스티치 → 닫히면 솔리드 | |
| `body.TaperFaces(ICollection<Face>, Plane neutral, double rad)` | 구배(드래프트): 측면 4面 5° → 체적 적분식과 정확 일치 | 양각 = **shrink**(중립면에서 멀수록 좁아짐) 방향이 기본 |
| `Line.Create(Point, Direction)` + `Matrix.CreateRotation(Line, rad)` | 바디 회전/원형 패턴 | |
| `Matrix.CreateMapping(Frame)` | 원점 +Z 빌드 → 임의 축 프레임 1회 매핑 (fastener/revolve 공용) | Frame.Create(o,u,v)의 DirZ = u×v |
| `Matrix.CreateScale(f, Point center)` | 바디 스케일 (체적 f³ 정확) | |
| Split-by-plane | ⚠️ `Body.Split(Plane,Tracker)`는 조각 분리 API 불명 → **copy + 반공간 Intersect ×2** (laminate 이디엄) 사용 | |
