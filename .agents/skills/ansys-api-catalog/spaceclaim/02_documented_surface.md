# SpaceClaim API — Documented Surface (from XML reference)

Extracted from `SpaceClaim.Api.V252.xml` (top-level scripting reference for the SpaceClaim modeler — distinct from `Scripting.xml` which only contains command-pattern wrappers). Status: DOCUMENTED — cross-reference with [01_used_in_project.md](01_used_in_project.md) for verified-working entries marked ✓ verified.

> Note on namespaces — this XML places most "design" types (`DesignBody`, `DesignFace`, `DesignEdge`, `DesignCurve`, `DesignMesh`, `Part`, `IPart`, `Document`, `Window`, `Component`, `Group`, `WriteBlock`, `LibraryMaterial`, `DocumentMaterial`, `MaterialProperty`, `PartExportFormat`, etc.) directly under `SpaceClaim.Api.V252.*` (NOT under `SpaceClaim.Api.V252.Modeler.*`). The `Modeler` namespace holds only pure-shape types (`Body`, `Face`, `Edge`, `Vertex`, `Shell`, `Loop`, `Fin`, `EdgeRound`, `FixedRadiusRound`, `VariableRadiusRound`, `Tracker`, `BodySaveFormat`, `Mesh`/`MeshFace`/`MeshEdge`/`MeshVertex`/`MeshTopology`, `Topology`, `Facet`, `TessellationOptions`, `FaceTessellation`, `TransformedBody`, `ProjectionOptions`, `BooleanException`). The `Geometry` namespace holds purely mathematical types (`Point`, `Vector`, `Direction`, `Plane`, `Frame`, `Matrix`, `Box`, `Interval`, `Curve`, `CurveSegment`, `Surface`, `NurbsData`, `ControlPoint`, `Profile`, `Circle`, `Ellipse`, `Line`, `NurbsCurve`, `Cylinder`, `Cone`, `Sphere`, `Torus`, `RectangleProfile`, `CircleProfile`, `SquareProfile`, `OblongProfile`, `PolygonProfile`, `RegularPolygonProfile`, `StarProfile`, `ArrowProfile`, `ITrimmedCurve`, etc.).

> Note on undocumented members — XML doc only ships docs for members the authors annotated. Many heavily-used statics observed at runtime (`Matrix.Identity`, `Direction.DirX`, `Point.Origin`, `Point.Create`, `Vector.Create`, `Vector.Cross`, `Vector.Dot`, `Plane.PlaneXY` is documented but `Plane.Create(Frame)` only; `Interval.Create`, `Frame.Create` are documented; `Point.Create` is NOT, etc.) are absent here yet exist on the binary surface and are verified by usage in this project — those are marked `✓ verified (undocumented in XML)`.

---

## SpaceClaim.Api.V252.Modeler.Body

A modeler body (pure shape — no document affiliation; wrapped by `DesignBody` for persistence).

### Static factory methods
- `Body.CreatePlanarBody(Plane plane, ICollection<ITrimmedCurve> curves) → Body` ✓ verified — planar shell body from closed loop(s)
- `Body.CreatePlanarBody(Plane, ICollection<ITrimmedCurve>, bool, bool, bool, out IDictionary<ITrimmedCurve, ICollection<Edge>>) → Body` — overload returning edge map
- `Body.CreatePlanarBody(Plane, ICollection<PointUV>, ICollection<ICollection<PointUV>>, out ICollection<int>) → Body` — from UV polygons
- `Body.CreateSurfaceBody(Surface, BoxUV) → Body`
- `Body.CreateNurbsBody(Body) → Body`
- `Body.CreateNurbsBody(Face) → Body`
- `Body.CreateWireBody(ITrimmedCurve) → Body`
- `Body.ExtrudeProfile(Profile profile, double distance) → Body` ✓ verified — extrude planar profile by signed distance (meters); always extrudes +Z from profile plane
- `Body.SweepProfile(Profile, ICollection<ITrimmedCurve> path) → Body`
- `Body.SweepChain(ICollection<ITrimmedCurve> chain, ICollection<ITrimmedCurve> path) → Body`
- `Body.LoftProfiles(IList<ICollection<ITrimmedCurve>>, bool, bool) → Body`
- `Body.LoftProfiles(IList<ICollection<ITrimmedCurve>>, double, double) → Body`
- `Body.LoftProfiles(IList<ICollection<ITrimmedCurve>>, double, double, bool) → Body`
- `Body.LoftProfiles(IList<ICollection<ITrimmedCurve>>, double, double, bool, bool) → Body`
- `Body.LoftProfiles(Loop, IList<ICollection<ITrimmedCurve>>, Loop, ICollection<ITrimmedCurve>) → Body` — full loft with guide rails
- `Body.Import(ForeignBody, out IDictionary<string,Face>, out IDictionary<string,Edge>) → Body`

### Instance methods — Boolean operations
- `body.Unite(ICollection<Body> tools)` ✓ verified — boolean union; mutates receiver
- `body.Unite(ICollection<Body> tools, double tolerance)` — union with tolerance
- `body.Unite(Body[] tools)` ✓ verified — array overload
- `body.Subtract(ICollection<Body> tools)` ✓ verified — boolean subtract
- `body.Subtract(ICollection<Body> tools, double tolerance)`
- `body.Subtract(Body[] tools)` ✓ verified — array overload
- `body.Intersect(ICollection<Body> tools)` ✓ verified — boolean intersect
- `body.Intersect(ICollection<Body> tools, double tolerance)`
- `body.Intersect(Body[] tools)` ✓ verified — array overload
- `body.Fuse(ICollection<Body> tools, bool keepCutter, Tracker tracker)` — fuse / non-destructive boolean

### Instance methods — Topology / Imprint / Split / Pieces
- `body.SeparatePieces() → ICollection<Body>`
- `body.SeparateNonManifold() → ICollection<Body>`
- `body.CombinePieces(ICollection<Body>)`
- `body.Imprint(Body other)`
- `body.ImprintCurves(ICollection<ITrimmedCurve>, ICollection<Face>)`
- `body.GetImprintedCurves(ICollection<ITrimmedCurve>, ICollection<Face>)`
- `body.Split(Plane, Tracker)`
- `body.Reverse()`
- `body.IntersectCurve(Curve) → ICollection<IntPoint<Body,Curve>>`
- `body.GetIntersections(Body other) → ICollection<BodyIntersection>`

### Instance methods — Copy / Tessellation
- `body.Copy() → Body` ✓ verified — deep copy
- `body.Copy(out IDictionary<Face,Face>, out IDictionary<Edge,Edge>) → Body` — copy with face/edge map
- `body.CopyFaces(ICollection<Face>) → Body`
- `body.CopyFaces(ICollection<Face>, out IDictionary<Face,Face>, out IDictionary<Edge,Edge>) → Body`
- `body.GetTessellation(ICollection<Face>, TessellationOptions) → IDictionary<Face,FaceTessellation>`

### Instance methods — Round / Offset / Project / Wrap
- `body.RoundEdges(ICollection<KeyValuePair<Edge, EdgeRound>>)` ✓ verified — fillet edges
- `body.RoundEdges(ICollection<KeyValuePair<Edge, EdgeRound>>, Tracker)`
- `body.OffsetFaces(ICollection<Face>, double)`
- `body.OffsetFaces(IDictionary<Face, double>)`
- `body.TaperFaces(ICollection<Face>, Plane, double)`
- `body.ProjectCurves(ICollection<ITrimmedCurve>, Direction, ProjectionOptions)`
- `body.WrapPlanarCurves(ICollection<ITrimmedCurve>, Point, Point, ProjectionOptions, Plane)`
- `body.WrapPlanarCurvesParallel(ICollection<ITrimmedCurve>, Point, Point, int, Plane)`

### Instance methods — Spatial queries / Geometry
- `body.Transform(Matrix)` ✓ verified — apply 4×4 transform (mutates)
- `body.GetNormal(Point, out Direction)`
- `body.GetBoundingBox(Matrix) → Box` ✓ verified — AABB in given frame
- `body.GetBoundingBox(Matrix, bool) → Box`
- `body.GetExtremePoint(Direction, Direction, Direction) → Point`
- `body.ProjectPoint(Point) → Point`
- `body.ContainsPoint(Point) → bool`
- `body.GetCollision(ITrimmedSpace) → Collision`
- `body.GetClosestSeparation(ITrimmedGeometry) → Separation`
- `body.GetHiddenLineImage(ICollection<TransformedBody>, Direction) → ICollection<ITrimmedCurve>`

### Instance methods — Save / Dispose
- `body.Save(BodySaveFormat, string path)` — `Text` or `Binary`
- `body.Dispose()`

### Properties
- `body.Faces` → `ICollection<Face>` ✓ verified
- `body.Edges` → `ICollection<Edge>` ✓ verified
- `body.Vertices` → `ICollection<Vertex>` ✓ verified
- `body.Shells` → `ICollection<Shell>`
- `body.IsClosed` → `bool`
- `body.IsManifold` → `bool`
- `body.PieceCount` → `int`
- `body.Volume` → `double` ✓ verified — m³
- `body.SurfaceArea` → `double` — m²

---

## SpaceClaim.Api.V252.DesignBody

Document-resident wrapper around a modeler `Body` (with persistence, naming, color, suppression, layer).

### Static factory methods
- `DesignBody.Create(Part parent, string name, Body modelerBody) → DesignBody` ✓ verified — register modeler body into a part

### Instance methods
- `designBody.Transform(Matrix)`
- `designBody.Scale(Frame, double x, double y, double z)`
- `designBody.Save(BodySaveFormat, string path)`
- `designBody.GetDesignFace(Face) → DesignFace` — Face → owning DesignFace
- `designBody.GetDesignEdge(Edge) → DesignEdge`
- `designBody.GetDesignBody(Body) → DesignBody`
- `designBody.IdentifyHoles(IdentifyHoleOptions) → ICollection<Hole>`
- `designBody.GetTessellation(ICollection<Face>) → IDictionary<Face,FaceTessellation>`
- `designBody.GetTessellationBoundingBox(Matrix, bool) → Box`
- `designBody.GetEdgeTessellation(ICollection<Edge>) → IDictionary<Edge, ICollection<Point>>`
- `designBody.GetColor(IAppearanceContext) → Color?`
- `designBody.SetColor(IAppearanceContext, Color?)`
- `designBody.GetVisibility(IAppearanceContext) → bool?`
- `designBody.SetVisibility(IAppearanceContext, bool?)`
- `designBody.IsVisible(IAppearanceContext) → bool`
- (inherited from `DocObject`) `designBody.Delete()` ✓ verified

### Properties
- `designBody.Shape` → `Body` ✓ verified — underlying modeler body
- `designBody.Name` → `string` ✓ verified (r/w)
- `designBody.Parent` → `Part` ✓ verified
- `designBody.Faces` → `ICollection<DesignFace>` ✓ verified
- `designBody.Edges` → `ICollection<DesignEdge>`
- `designBody.Material` → `DocumentMaterial` (r/w; from `IHasMaterial`)
- `designBody.SurfaceMaterial` → `SurfaceMaterial`
- `designBody.Style` → `BodyStyle`
- `designBody.FinishStyle` → `BodyFinishStyle`
- `designBody.RenderingStyle` → `BodyRenderingStyle`
- `designBody.MidSurface` → `MidSurfaceAspect`
- `designBody.VolumeExtraction` → `VolumeExtractionAspect`
- `designBody.Enclosure` → `EnclosureAspect`
- `designBody.Layer` → `Layer`
- `designBody.DefaultVisibility` → `DefaultVisibility`
- `designBody.MassProperties` → `MassProperties`
- `designBody.CanSuppress` → `bool`
- `designBody.IsSuppressed` → `bool`
- `designBody.IsLocked` → `bool`

---

## SpaceClaim.Api.V252.DesignFace

### Instance methods
- `designFace.GetColor(IAppearanceContext) → Color?`
- `designFace.SetColor(IAppearanceContext, Color?)`

### Properties
- `designFace.Shape` → `Face` ✓ verified — underlying modeler face
- `designFace.Parent` → `DesignBody` ✓ verified (typed as `IDocObject` but castable)
- `designFace.Edges` → `ICollection<DesignEdge>` ✓ verified
- `designFace.AdjacentFaces` → `ICollection<DesignFace>`
- `designFace.SurfaceMaterial` → `SurfaceMaterial`
- `designFace.ExportIdentifier` → `string`
- `designFace.Area` → `double` (m²)
- `designFace.Perimeter` → `double` (m)

---

## SpaceClaim.Api.V252.DesignEdge

### Properties
- `designEdge.Shape` → `Edge` ✓ verified
- `designEdge.Parent` → `DesignBody`
- `designEdge.Faces` → `ICollection<DesignFace>`
- `designEdge.ExportIdentifier` → `string`

---

## SpaceClaim.Api.V252.DesignCurve

### Static factory methods
- `DesignCurve.Create(IDesignCurveParentMaster, ITrimmedCurve) → DesignCurve` ✓ verified
- `DesignCurve.Create(IDesignCurveParentMaster, ITrimmedCurve, bool?) → DesignCurve`
- `DesignCurve.Create(IDesignCurveParent, ITrimmedCurve) → DesignCurve`
- `DesignCurve.Create(IDesignCurveParent, ITrimmedCurve, bool?) → DesignCurve`

### Instance methods
- `designCurve.Copy() → DesignCurve`
- `designCurve.Replace(ICollection<DocObject>)`
- `designCurve.Transform(Matrix)`
- `designCurve.Scale(Frame, double x, double y, double z)`
- `designCurve.GetColor/SetColor/GetVisibility/SetVisibility/IsVisible(IAppearanceContext)`

### Properties
- `designCurve.Shape` → `CurveSegment`
- `designCurve.Parent` → `IDesignCurveParent`
- `designCurve.IsConstruction` → `bool`
- `designCurve.Layer` → `Layer`
- `designCurve.DefaultVisibility` → `DefaultVisibility`
- `designCurve.CanSuppress` → `bool`
- `designCurve.IsSuppressed` → `bool`
- `designCurve.IsLocked` → `bool`

---

## SpaceClaim.Api.V252.DesignMesh

### Static factory methods
- `DesignMesh.Create(Part, string name, IList<Point> vertices, IList<Facet> facets) → DesignMesh`
- `DesignMesh.Create(Part, string name, IList<PointF> vertices, IList<Facet> facets) → DesignMesh`
- `DesignMesh.Create(Part, string name, float[] vertices, int[] facetIndices) → DesignMesh` ✓ verified — flat-array overload

### Instance methods
- `designMesh.Transform(Matrix)`
- `designMesh.GetDesignMeshTopology(MeshTopology) → IDesignMeshTopology`
- `designMesh.GetDesignMeshRegion(ICollection<MeshFace>) → IDesignMeshRegion`
- `designMesh.GetCollision(IDesignMesh other, double tol) → Collision`
- `designMesh.GetFaceColor(MeshFace) → Color?`
- `designMesh.SetFaceColor(MeshFace, Color?)`
- (inherited) `designMesh.Delete()` ✓ verified

### Properties
- `designMesh.Shape` → `Mesh`
- `designMesh.Parent` → `Part`
- `designMesh.Name` → `string`
- `designMesh.Material` → `DocumentMaterial`

---

## SpaceClaim.Api.V252.Modeler.Face

### Instance methods
- `face.GetAdjacentFace(Edge) → Face`
- `face.SurfaceAsTrimmedSpline() → NurbsSurface`
- `face.SurfaceAsTrimmedSpline2(out bool) → NurbsSurface`
- `face.AsSpline(BoxUV) → NurbsSurface`
- `face.ProjectPoint(Point) → ProjectionResult` — closest point on face
- `face.ContainsParam(PointUV) → bool`
- `face.ContainsPoint(Point) → bool`
- `face.IntersectCurve(ITrimmedCurve) → ICollection<IntPoint<Face,ITrimmedCurve>>`
- `face.IntersectCurves(IList<ITrimmedCurve>) → ICollection<IntPoint>`
- `face.IntersectCurve(Curve) → ICollection<IntPoint<Face,Curve>>`
- `face.ImprintCurves(ICollection<ITrimmedCurve>) → IDictionary<ITrimmedCurve,ICollection<Edge>>`
- `face.GetBoundingBox(Matrix) → Box`
- `face.GetExtremePoint(Direction, Direction, Direction) → Point`
- `face.GetClosestSeparation(ITrimmedGeometry) → Separation`
- `face.GetGeometry<T>() → T` — typed geometry getter (e.g. `Plane`, `Cylinder`)

### Properties
- `face.Edges` → `ICollection<Edge>`
- `face.Loops` → `ICollection<Loop>`
- `face.AdjacentFaces` → `ICollection<Face>`
- `face.Shell` → `Shell`
- `face.Area` → `double` ✓ verified — m²
- `face.Perimeter` → `double` — m
- `face.Geometry` → `Surface` ✓ verified (RTTI to `Plane`/`Cylinder`/`Cone`/`Sphere`/`Torus`/`NurbsSurface`)
- `face.IsReversed` → `bool` ✓ verified — orientation flag (flip for true outward normal)
- `face.BoxUV` → `BoxUV`

---

## SpaceClaim.Api.V252.Modeler.Edge

### Instance methods
- `edge.ProjectPoint(Point) → ProjectionResult`
- `edge.ContainsPoint(Point) → bool`
- `edge.IntersectCurve(ITrimmedCurve) → ICollection<IntPoint>`
- `edge.IsCoincident(ITrimmedCurve) → bool`
- `edge.Offset(Plane, double) → CurveSegment`
- `edge.OffsetChain(Plane, double, ICollection<ITrimmedCurve>, OffsetCornerType)`
- `edge.GetPolyline(PolylineOptions) → ICollection<Point>`
- `edge.ProjectToPlane(Plane) → CurveSegment`
- `edge.GetBoundingBox(Matrix) → Box`
- `edge.GetExtremePoint(Direction, Direction, Direction) → Point`
- `edge.GetClosestSeparation(ITrimmedGeometry) → Separation`
- `edge.GetGeometry<T>() → T`

### Properties
- `edge.Faces` → `ICollection<Face>`
- `edge.Fins` → `ICollection<Fin>` — fin-per-incident-face
- `edge.Shell` → `Shell`
- `edge.StartVertex` → `Vertex`
- `edge.EndVertex` → `Vertex`
- `edge.StartPoint` → `Point` ✓ verified
- `edge.EndPoint` → `Point` ✓ verified
- `edge.Bounds` → `Interval` ✓ verified
- `edge.Length` → `double` (m)
- `edge.Geometry` → `Curve` ✓ verified (RTTI to `Line`/`Circle`/`Ellipse`/`NurbsCurve`)
- `edge.IsSmooth` → `bool` — G1-continuous boundary
- `edge.IsConcave` → `bool`
- `edge.IsReversed` → `bool`
- `edge.Precision` → `double`

---

## SpaceClaim.Api.V252.Modeler.Vertex

### Instance methods
- `vertex.ContainsPoint(Point) → bool`
- `vertex.GetBoundingBox(Matrix) → Box`
- `vertex.GetExtremePoint(Direction, Direction, Direction) → Point`

### Properties
- `vertex.Position` → `Point`
- `vertex.Faces` → `ICollection<Face>`
- `vertex.Edges` → `ICollection<Edge>`
- `vertex.Shell` → `Shell`
- `vertex.Precision` → `double`

---

## SpaceClaim.Api.V252.Modeler.EdgeRound (abstract)

Base class for round/fillet specifications consumed by `Body.RoundEdges`.

### Derived
- `FixedRadiusRound`
- `VariableRadiusRound`

## SpaceClaim.Api.V252.Modeler.FixedRadiusRound : EdgeRound

- `new FixedRadiusRound(double radius)` ✓ verified
- `.Radius` → `double` (r/w)

## SpaceClaim.Api.V252.Modeler.VariableRadiusRound : EdgeRound

- `new VariableRadiusRound(ICollection<RadiusPoint>)`
- `.RadiusPoints` → `ICollection<RadiusPoint>`

---

## SpaceClaim.Api.V252.Geometry.Plane

### Static factory methods
- `Plane.Create(Frame) → Plane` ✓ verified
- `Plane.TryCreate(ICollection<ITrimmedCurve>) → Plane?` — fit a plane through coplanar curves

### Static properties (built-in planes)
- `Plane.PlaneXY` → `Plane` ✓ verified — Z = 0
- `Plane.PlaneYZ` → `Plane` — X = 0
- `Plane.PlaneZX` → `Plane` — Y = 0

### Properties
- `plane.Frame` → `Frame` ✓ verified — origin + axes

---

## SpaceClaim.Api.V252.Geometry.Frame

### Static factory methods
- `Frame.Create(Point origin, Direction dirX, Direction dirY) → Frame` ✓ verified — third axis derived by right-hand rule
- `Frame.Create(Point origin, Direction dirZ) → Frame` — arbitrary dirX/dirY perpendicular to dirZ

### Properties (undocumented in XML but verified)
- `frame.Origin` → `Point` ✓ verified
- `frame.DirX` → `Direction` ✓ verified
- `frame.DirY` → `Direction` ✓ verified
- `frame.DirZ` → `Direction` ✓ verified
- `frame.Plane` → `Plane`

---

## SpaceClaim.Api.V252.Geometry.Point

### Static factory methods (verified by usage, undocumented in XML)
- `Point.Create(double x, double y, double z) → Point` ✓ verified
- `Point.Origin` → `Point` ✓ verified (static) — origin (0,0,0)

### Properties
- `point.Vector` → `Vector` — vector from origin to point
- `point.X` / `point.Y` / `point.Z` → `double` ✓ verified (undocumented)

### Operators (undocumented in XML)
- `Point - Point → Vector` ✓ verified
- `Point + Vector → Point` ✓ verified
- `Matrix * Point → Point`

---

## SpaceClaim.Api.V252.Geometry.Direction

### Static factory methods / constants (verified)
- `Direction.Create(double x, double y, double z) → Direction` ✓ verified
- `Direction.DirX` → `Direction` ✓ verified (static) — (1,0,0)
- `Direction.DirY` → `Direction` ✓ verified (static)
- `Direction.DirZ` → `Direction` ✓ verified (static)
- `Direction.Cross(Direction, Direction) → Direction` (static)

### Properties
- `direction.UnitVector` → `Vector` ✓ verified — unit-magnitude `Vector`
- `direction.IsZero` → `bool`
- `direction.ArbitraryPerpendicular` → `Direction` — any vector perpendicular to this

### Operators
- `Matrix * Direction → Direction`
- Unary `-direction`

---

## SpaceClaim.Api.V252.Geometry.Vector

### Static (verified by usage)
- `Vector.Create(double x, double y, double z) → Vector` ✓ verified
- `Vector.Dot(Vector, Vector) → double` ✓ verified
- `Vector.Cross(Vector, Vector) → Vector` ✓ verified
- `Vector.Zero` → `Vector` (static)

### Instance methods
- `vector.Project(Direction) → Vector`

### Properties
- `vector.Direction` → `Direction`
- `vector.Magnitude` → `double` ✓ verified
- `vector.X` / `.Y` / `.Z` → `double` ✓ verified

### Operators
- `Matrix * Vector → Vector`
- `-vector`, `vector + vector`, `vector - vector`, `vector * scalar`

---

## SpaceClaim.Api.V252.Geometry.Matrix

### Static factory methods
- `Matrix.CreateTranslation(Vector) → Matrix` ✓ verified
- `Matrix.CreateScale(double scale, Point center) → Matrix` ✓ verified
- `Matrix.CreateScale(double scale) → Matrix` — uniform scale about origin
- `Matrix.CreateRotation(Line axis, double radians) → Matrix`
- `Matrix.CreateMapping(Frame) → Matrix` — frame → world
- `Matrix.Identity` → `Matrix` ✓ verified (static)

### Instance methods
- `matrix.Decompose(out Vector translation, out double scale, out Matrix rotation)`
- `matrix.TryGetRotation(out Line axis, out double angle) → bool`

### Properties
- `matrix.IsIdentity` → `bool`
- `matrix.HasTranslation` → `bool`
- `matrix.HasScale` → `bool`
- `matrix.HasRotation` → `bool`
- `matrix.IsMirror` → `bool`
- `matrix.Translation` → `Vector`
- `matrix.Scale` → `double`
- `matrix.Rotation` → `Matrix`
- `matrix.Inverse` → `Matrix`
- `matrix[i, j]` → `double` — 4×4 element accessor

### Operators
- `Matrix * Matrix → Matrix`
- `Matrix * Frame → Frame`
- `Matrix * Box → Box`
- `Matrix * Point → Point`
- `Matrix * Vector → Vector`
- `Matrix * Direction → Direction`
- `Matrix * double → Matrix`

---

## SpaceClaim.Api.V252.Geometry.Box

### Static factory methods
- `Box.Empty` → `Box` (static field)
- `Box.Create(Point) → Box` — degenerate (single-point) box
- `Box.Create(Point[]) → Box` — enclosing AABB of points
- `Box.Create(double minX, double minY, double minZ, double maxX, double maxY, double maxZ) → Box`
- `Box.Create(ICollection<Point>) → Box`

### Instance methods
- `box.ContainsPoint(Point) → bool`
- `box.IntersectsPlane(Plane) → bool`
- `box.IntersectsLine(ITrimmedCurve) → bool`
- `box.IntersectsLines(IList<ITrimmedCurve>) → bool`
- `box.Inflate(double margin) → Box`

### Properties
- `box.MinCorner` → `Point` ✓ verified
- `box.MaxCorner` → `Point` ✓ verified
- `box.Center` → `Point`
- `box.Size` → `Vector`
- `box.Corners` → `Point[]`
- `box.IsEmpty` → `bool`

### Operators
- `Box | Box → Box` — union (`op_BitwiseOr`)
- `Box & Box → Box` — intersection (`op_BitwiseAnd`)

---

## SpaceClaim.Api.V252.Geometry.Interval

### Static (verified by usage)
- `Interval.Create(double start, double end) → Interval` ✓ verified

### Instance methods
- `interval.Contains(double) → bool`
- `interval.GetProportion(double param) → double` — `(param - Start) / Span`
- `interval.GetParameter(double proportion) → double` — `Start + proportion * Span`

### Properties
- `interval.Start` → `double` ✓ verified
- `interval.End` → `double` ✓ verified
- `interval.Span` → `double`
- `interval[double]` → `double` — same as `GetParameter`

---

## SpaceClaim.Api.V252.Geometry.Profile

Base profile class — closed-loop sketch on a plane.

### Constructor
- `new Profile(Plane plane, IList<ITrimmedCurve> boundary)` ✓ verified

### Instance methods
- `profile.ProjectPoint(Point) → PointUV`
- `profile.ContainsParam(PointUV) → bool`
- `profile.ContainsPoint(Point) → bool`
- `profile.GetBoundingBox(Matrix) → Box`
- `profile.GetExtremePoint(Direction, Direction, Direction) → Point`

### Properties
- `profile.Plane` → `Plane`
- `profile.Boundary` → `IList<ITrimmedCurve>`
- `profile.Area` → `double`
- `profile.Perimeter` → `double`
- `profile.BoxUV` → `BoxUV`

---

## SpaceClaim.Api.V252.Geometry.RectangleProfile : Profile

- `new RectangleProfile(Plane plane, double width, double height, PointUV location, double angle)` ✓ verified — 5-arg overload (project also calls a 3-arg `(plane, w, h)` convenience form)
- `.Width` / `.Height` → `double`
- `.Location` → `PointUV`
- `.Angle` → `double` (radians)
- `.Area` / `.Perimeter` → `double`

## SpaceClaim.Api.V252.Geometry.CircleProfile : Profile

- `new CircleProfile(Plane plane, double radius, PointUV location, double angle)` ✓ verified — 4-arg overload
- `.Radius` → `double`
- `.Location` → `PointUV`
- `.Angle` → `double`

## Other built-in profile classes (less common)

- `SquareProfile`, `OblongProfile`, `PolygonProfile`, `RegularPolygonProfile`, `StarProfile`, `ArrowProfile`

---

## SpaceClaim.Api.V252.Geometry.Circle (curve, not profile)

### Static factory methods
- `Circle.Create(Frame, double radius) → Circle` ✓ verified
- `Circle.CreateThroughPoints(Plane, Point, Point, Point) → Circle`
- `Circle.CreateTangentToThree(Plane, CurveParam, CurveParam, CurveParam) → Circle`
- `Circle.CreateTangentToTwo(Plane, CurveParam, CurveParam, Point) → Circle`
- `Circle.CreateTangentToOne(Plane, CurveParam, Point, Point) → Circle`

### Properties
- `circle.Radius` → `double` ✓ verified
- `circle.Frame` → `Frame` ✓ verified
- `circle.Plane` → `Plane`
- `circle.Axis` → `Line`

---

## SpaceClaim.Api.V252.Geometry.Ellipse

### Properties
- `ellipse.Frame` → `Frame` ✓ verified
- `ellipse.Plane` → `Plane`
- `ellipse.Axis` → `Line`
- (verified by usage) `.MajorRadius`, `.MinorRadius` → `double`
- Static `Ellipse.Create(Frame, double majorR, double minorR) → Ellipse` ✓ verified (undocumented in XML)

---

## SpaceClaim.Api.V252.Geometry.Line

### Static factory methods
- `Line.Create(Point origin, Direction direction) → Line`
- `Line.CreateThroughPoints(Point, Point) → Line`
- `Line.CreateTangentToTwo(Plane, CurveParam, CurveParam) → Line`
- `Line.CreateTangentToOne(Plane, CurveParam, Point) → Line`

### Properties
- `line.Origin` → `Point`
- `line.Direction` → `Direction`

---

## SpaceClaim.Api.V252.Geometry.Curve (abstract base)

### Instance methods
- `curve.Evaluate(double param) → CurveEvaluation` ✓ verified (use `.Point`, `.Tangent`, `.Curvature`)
- `curve.ProjectPoint(Point) → CurveEvaluation`
- `curve.GetLength(Interval) → double`
- `curve.GetPolyline(Interval, PolylineOptions) → ICollection<Point>`
- `curve.TryOffsetParam(double, double, out double) → bool`
- `curve.ContainsParam(double) → bool`
- `curve.IntersectCurve(Curve) → ICollection<IntPoint<Curve,Curve>>`
- `curve.AsSpline(Interval) → NurbsCurve`

### Operators
- `Matrix * Curve → Curve`

---

## SpaceClaim.Api.V252.Geometry.CurveSegment (trimmed curve = curve + interval)

### Static factory methods
- `CurveSegment.Create(Curve, Interval) → CurveSegment` ✓ verified (used for arc segments via `Circle/Ellipse/NurbsCurve` + `Interval`)
- `CurveSegment.Create(Curve, Interval, bool reversed) → CurveSegment`
- `CurveSegment.Create(ITrimmedCurve) → CurveSegment` — copy constructor
- `CurveSegment.Create(Curve) → CurveSegment`
- `CurveSegment.Create(Point start, Point end) → CurveSegment` ✓ verified — straight line segment
- `CurveSegment.Create(IList<Tuple<Point,Point>>) → IList<CurveSegment>` — polyline batch
- `CurveSegment.Create(LineSegment) → CurveSegment`
- `CurveSegment.CreateArc(Point start, Point end, Point center, Direction axis) → CurveSegment`
- `CurveSegment.CreateHelix(Line axis, Point start, double pitch, double turns, double taper, CircularSense) → CurveSegment`

### Instance methods
- `cs.ProjectPoint(Point) → CurveEvaluation`
- `cs.IntersectCurve(ITrimmedCurve) → ICollection<IntPoint>`
- `cs.Offset(Plane, double) → CurveSegment`
- `cs.OffsetChain(Plane, double, ICollection<ITrimmedCurve>, OffsetCornerType)`
- `cs.GetPolyline(PolylineOptions) → ICollection<Point>`
- `cs.IsCoincident(ITrimmedCurve) → bool`
- `cs.ProjectToPlane(Plane) → CurveSegment`
- `cs.GetBoundingBox(Matrix) → Box`
- `cs.GetExtremePoint(Direction, Direction, Direction) → Point`
- `cs.ContainsPoint(Point) → bool`
- `cs.GetClosestSeparation(ITrimmedGeometry) → Separation`
- `cs.Dispose()`

### Properties
- `cs.Geometry` → `Curve` ✓ verified
- `cs.Bounds` → `Interval` ✓ verified
- `cs.Length` → `double`
- `cs.StartPoint` → `Point` ✓ verified
- `cs.EndPoint` → `Point` ✓ verified
- `cs.IsReversed` → `bool`

---

## SpaceClaim.Api.V252.Geometry.NurbsCurve

### Static factory methods
- `NurbsCurve.CreateFromControlPoints(NurbsData, ControlPoint[]) → NurbsCurve` ✓ verified
- `NurbsCurve.CreateFromKnotPoints(bool closed, IList<Point>, Vector? startTangent, Vector? endTangent) → NurbsCurve`
- `NurbsCurve.CreateThroughPoints(bool closed, IList<Point>, double tolerance, Vector? startTangent, Vector? endTangent) → NurbsCurve`
- `NurbsCurve.CreateThroughPoints(bool closed, IList<Point>, double tolerance) → NurbsCurve` ✓ verified

### Properties
- `nurbsCurve.IsRational` → `bool`
- `nurbsCurve.Data` → `NurbsData` ✓ verified
- `nurbsCurve.ControlPoints` → `IList<ControlPoint>` ✓ verified
- `nurbsCurve.Parameterization` → `Parameterization` (inherited) ✓ verified

## SpaceClaim.Api.V252.Geometry.NurbsData

- `new NurbsData(int order, bool isClosed, bool isPeriodic, Knot[] knots)`
- `.Order` → `int`
- `.Degree` → `int`
- `.IsClosed` → `bool`
- `.IsPeriodic` → `bool`
- `.Knots` → `Knot[]`

## SpaceClaim.Api.V252.Geometry.ControlPoint

- `new ControlPoint(Point position, double weight)` ✓ verified
- `.Position` → `Point`
- `.Weight` → `double`

## SpaceClaim.Api.V252.Geometry.Parameterization

- `parameterization.Form` → `ParamForm`
- `parameterization.Bounds` → `Interval` ✓ verified

---

## Surface types (Cylinder / Sphere / Cone / Torus)

- `cylinder.Frame` → `Frame` ✓ verified, `cylinder.Axis` → `Line`, (verified) `.Radius` → `double`
- `sphere.Frame` → `Frame`, `.Radius`
- `cone.Radius` → `double`, `.HalfAngle`, `.Frame`, `.Axis`
- `torus.Frame`, `.Plane`, `.Axis`, `.MajorRadius`, `.MinorRadius`

---

## SpaceClaim.Api.V252.Geometry.Surface (abstract base)

- `surface.Evaluate(PointUV) → SurfaceEvaluation`
- `surface.ProjectPoint(Point) → SurfaceEvaluation`
- `surface.GetLength(PointUV, PointUV) → double`
- `surface.ContainsParam(PointUV) → bool`
- `surface.IntersectCurve(Curve) → ICollection<IntPoint<Surface,Curve>>`

### Properties
- `surface.IsRuled` → `bool`
- `surface.IsSingular` → `bool`

### Operators
- `Matrix * Surface → Surface`

---

## SpaceClaim.Api.V252.Part

### Static factory methods
- `Part.Create(Document, string name) → Part`
- `Part.CreateBeamProfile(Document, string, Profile) → BeamProfile`

### Instance methods
- `part.Export(PartExportFormat format, string path, bool overwrite, ExportOptions options)` ✓ verified
- `part.ConvertToSheetMetal()`
- `part.GetDesignCurvesInPlane(Plane) → ICollection<DesignCurve>`
- `part.IsSketchCurve(DesignCurve) → bool`
- `part.SetSketchCurve(DesignCurve, bool)`
- `part.TryGetCollision(ICollection<IDesignFace>, ICollection<IDesignFace>, out IDesignFace, out IDesignFace) → bool`
- `part.Delete()`

### Properties
- `part.Bodies` → `ICollection<DesignBody>` ✓ verified
- `part.Components` → `ICollection<Component>` ✓ verified
- `part.Meshes` → `ICollection<DesignMesh>`
- `part.Beams` → `ICollection<Beam>`
- `part.Bolts` → `ICollection<Bolt>`
- `part.CoordinateSystems` → `ICollection<CoordinateSystem>`
- `part.DatumPlanes` / `.DatumLines` / `.DatumPoints` / `.DatumFeatures`
- `part.Curves` → `ICollection<DesignCurve>`
- `part.CurveGroups` → `ICollection<DesignCurveGroup>`
- `part.SpotWeldJoints` → `ICollection<SpotWeldJoint>`
- `part.Material` → `DocumentMaterial`
- `part.Type` → `PartType`
- `part.SheetMetal` → `SheetMetalAspect`
- `part.IsSheetMetalSuspended` → `bool`
- `part.FlatPattern` → `FlatPatternAspect`
- `part.ShareTopology` → `ShareTopology` (enum)
- `part.Name` → `string`
- `part.Parent` → `IPart`/`Document`
- `part.IsEmpty` → `bool`
- `part.CustomProperties` → `CustomPropertyDictionary`
- `part.Groups` → `ICollection<Group>` ✓ verified

---

## SpaceClaim.Api.V252.Component

### Static factory methods
- `Component.CreateFromFile(Part parent, string path, ImportOptions) → Component` — external linked instance
- `Component.Create(Part parent, Part template) → Component` — instance of another part

### Instance methods
- `component.ReplaceFromFile(string path)`
- `component.Transform(Matrix)`

### Properties
- `component.Parent` → `Part`
- `component.Content` → `IPart` ✓ verified — the referenced part (**access only inside `WriteBlock.ExecuteTask`**)
- `component.Components` → `ICollection<Component>`
- `component.Template` → `Part`
- `component.Placement` → `Matrix`
- `component.Name` → `string`
- `component.CustomProperties` → `CustomPropertyDictionary`

---

## SpaceClaim.Api.V252.Document

### Static factory methods
- `Document.Create() → Document`
- `Document.Open(string path, ImportOptions options) → Document` ✓ verified
- `Document.GetDocument(string path) → Document` — find already-open document

### Instance methods
- `document.Save()`
- `document.SaveAs(string path)` ✓ verified
- `document.SaveAsSnapshot(string path)`
- `document.GetReferencedDocuments(bool recursive) → ICollection<Document>`
- `document.InternalizeParts(ICollection<Part>, bool)`
- `document.ExternalizeParts(ICollection<Part>, string)`
- `document.DeleteObjects<T>(ICollection<T>)` — bulk delete of `IDocObject`-derived
- `document.GetLayer(string name) → Layer`

### Properties
- `document.MainPart` → `Part` ✓ verified
- `document.Parts` → `ICollection<Part>` — all parts (top-level)
- `document.Path` → `string` ✓ verified
- `document.Materials` → `DocumentMaterialDictionary` ✓ verified
- `document.Layers` → `ICollection<Layer>`
- `document.Units` → `UnitsSystem`
- `document.IsModified` → `bool`
- `document.IsComplete` → `bool`
- `document.IsLocked` → `bool`
- `document.CoreProperties` → `CoreProperties`
- `document.CustomProperties` → `CustomPropertyDictionary`

---

## SpaceClaim.Api.V252.Window

### Static properties
- `Window.ActiveWindow` → `Window` ✓ verified
- `Window.AllWindows` → `ICollection<Window>`

### Static factory methods
- `Window.Create(Part, bool) → Window`
- `Window.Create(DrawingSheet) → Window`
- `Window.GetWindows(Document) → ICollection<Window>`

### Instance methods
- `window.Copy() → Window`
- `window.ZoomExtents()` ✓ verified
- `window.ZoomSelection()`
- `window.SetProjection(Matrix, bool animate, bool fit)`
- `window.SetProjection(Frame, double scale)`
- `window.GetCameraFrame() → Frame`
- `window.CreateBitmap(Size) → Bitmap`
- `window.Export(WindowExportFormat, string path)`
- `window.ExportPart(PartWindowExportFormat, string path)`
- `window.Close()`
- `window.Delete()`

### Properties
- `window.Document` → `Document` ✓ verified
- `window.ActiveContext` → `InteractionContext` ✓ verified — exposes `.Selection`, `.SingleSelection`
- `window.SceneContext` → `InteractionContext`
- `window.ActiveTool` → `Tool`
- `window.ActiveLayer` → `Layer`
- `window.SceneBox` → `Box`
- `window.Camera` → `Camera`
- `window.Projection` → `Matrix`
- `window.SelectionFilter` → `SelectionFilterType`
- `window.Groups` → `ICollection<Group>`
- `window.SelectedGroups` → `ICollection<Group>`
- `window.IsVisible` → `bool`
- `window.InteractionMode` → `InteractionMode`
- `window.Units` → `UnitsSystem`

---

## SpaceClaim.Api.V252.InteractionContext

(`window.ActiveContext` / `window.SceneContext`)

### Instance methods
- `ctx.SelectById(string[] ids)`
- `ctx.SelectByGroup(string[] groupNames)`
- `ctx.GetSelection<T>() → ICollection<T>`
- `ctx.GetSelectionPoint(IDocObject) → Point`
- `ctx.MapToContext<T>(T) → T`
- `ctx.MapToScene<T>(T) → T`
- `ctx.GetVisibleCurves(Plane) → ICollection<ITrimmedCurve>`
- `ctx.GetSection(IDesignBody) → ICollection<ITrimmedCurve>`

### Properties
- `ctx.Selection` → `ICollection<IDocObject>` ✓ verified
- `ctx.SingleSelection` → `IDocObject` ✓ verified
- `ctx.SecondarySelection` → `ICollection<IDocObject>`
- `ctx.Preselection` → `IDocObject`
- `ctx.PreselectionPoint` → `Point`
- `ctx.Window` → `Window`
- `ctx.ActivePart` → `Part`
- `ctx.VisibleBodies` → `ICollection<IDesignBody>`
- `ctx.SectionPlane` → `Plane?`

---

## SpaceClaim.Api.V252.Group

(SpaceClaim "Group" = Named Selection.)

### Static factory methods
- `Group.Create(Part, string name, ICollection<IDocObject> members) → Group` ✓ verified
- `Group.Create(Part, string name, ICollection<IDocObject> primary, ICollection<IDocObject> secondary) → Group`
- `Group.Create(Part, string name, ICollection<IDocObject> primary, ICollection<IDocObject> secondary, string customId, string description) → Group`
- `Group.Create(DrawingSheet, string name, ICollection<IDocObject> members) → Group`

### Instance methods
- `group.TryGetDimensionValue(out double, out DimensionType) → bool`
- `group.SetDimensionValue(double)`

### Properties
- `group.Members` → `ICollection<IDocObject>`
- `group.SecondaryMembers` → `ICollection<IDocObject>`
- `group.Name` → `string` ✓ verified
- `group.HasDimension` → `bool`

---

## SpaceClaim.Api.V252.LibraryMaterial

### Properties
- `LibraryMaterial.Library` → `LibraryMaterialDictionary` (static) ✓ verified
- `libraryMaterial.Properties` → `MaterialPropertyDictionary`

## SpaceClaim.Api.V252.DocumentMaterial : Material

### Static factory methods
- `DocumentMaterial.Create(Document, string name, double density) → DocumentMaterial`
- `DocumentMaterial.Copy(Document, Material source) → DocumentMaterial` ✓ verified

### Instance methods
- `docMaterial.SetProperty(MaterialProperty)` ✓ verified
- `docMaterial.RemoveProperty(string id)`
- `docMaterial.Delete()`

### Properties
- `docMaterial.Name` → `string` ✓ verified (r/w)
- `docMaterial.Density` → `double` ✓ verified (r/w, SI kg/m³)
- `docMaterial.Properties` → `MaterialPropertyDictionary`
- `docMaterial.Document` → `Document`

## SpaceClaim.Api.V252.MaterialProperty

- `new MaterialProperty(string id, string displayName, double value, string units)` ✓ verified
- (verified) `new MaterialProperty(MaterialPropertyId id, string displayName, double value, string units)` ✓ verified

### Properties
- `.Id`, `.DisplayName`, `.Value`, `.Units`

## SpaceClaim.Api.V252.MaterialPropertyId (enum)

- `Density` ✓ verified
- `TensileStrength` ✓ verified
- `ElasticModulus` ✓ verified
- `ShearModulus` ✓ verified
- `PoissonsRatio` ✓ verified
- `ThermalConductivity` ✓ verified
- `SpecificHeat` ✓ verified

## SpaceClaim.Api.V252.IHasMaterial (interface)

- `.Material` → `DocumentMaterial` (r/w) ✓ verified — implemented by `DesignBody`, `DesignMesh`, etc.

---

## SpaceClaim.Api.V252.IDocObject (interface)

(Base interface — every document-resident object implements it.)

### Properties
- `.Root` → `IDocObject`
- `.Parent` → `IDocObject`
- `.Moniker` → `Moniker`
- `.Original` → `IDocObject`
- `.Master` → `IDocObject`
- `.TransformToMaster` → `Matrix`
- `.PathToMaster` → `IList<IInstance>`
- `.Instance` → `IInstance`

### Methods
- `.GetAncestor<T>() → T`
- `.GetChildren<T>() → ICollection<T>`
- `.GetDescendants<T>() → ICollection<T>`
- `.GetOccurrence(IList<Instance>) → IDocObject`

## SpaceClaim.Api.V252.DocObject (concrete base)

Adds:
- `.NumberAttributes`, `.TextAttributes` — string-keyed bag
- `.SetNumberAttribute(string key, double)`, `.TryGetNumberAttribute(string, out double)`
- `.SetTextAttribute(string, string)`, `.TryGetTextAttribute(string, out string)`
- `.UpdateState` → `UpdateState`
- `.Document` → `Document`
- `.Delete()` ✓ verified
- `.IsDeleted` → `bool`

---

## SpaceClaim.Api.V252.WriteBlock (transaction wrapper)

**REQUIRED** wrapper for all geometry mutation, body creation, group creation, and `Component.Content` access.

### Static methods
- `WriteBlock.ExecuteTask(string label, Task body)` ✓ verified — runs `body` as a single undo step. The project's `Task` is a delegate (typically `Action`).
- `WriteBlock.AppendTask(Task body)` — append to an existing active block

### Static properties
- `WriteBlock.IsAvailable` → `bool`
- `WriteBlock.IsActive` → `bool`
- `WriteBlock.IsInterrupted` → `bool`

---

## SpaceClaim.Api.V252.PartExportFormat (enum)

- `CatiaV5Part` / `CatiaV5Assembly` / `CatiaV5Graphics`
- `Iges`
- `Step` ✓ verified
- `Vda`
- `JtOpen`
- `ParasolidText` / `ParasolidBinary`
- `AcisText` / `AcisBinary`
- `Rhino`
- `PdfGeometry` / `PdfFacets`
- `SketchUp`
- `FMD`
- `Stride`
- `MCNP`

---

## Notes / Gotchas

- The XML covers only what the SpaceClaim devs explicitly documented. The .NET binary surface is much larger; in particular many high-traffic constructors / statics (`Point.Create`, `Vector.Create`, `Vector.Dot`, `Vector.Cross`, `Direction.DirX/Y/Z`, `Matrix.Identity`, `Ellipse.Create`, `Frame.Origin` etc.) live on the assembly but are absent from this XML. The project's `01_used_in_project.md` lists them as verified working.
- `Body.ExtrudeProfile(profile, distance)` is the workhorse for all extruded solids — always extrudes +Z from the profile plane; signed `distance` flips direction; meters only.
- Boolean ops (`Subtract`/`Unite`/`Intersect`) **mutate the receiver** (`target.Subtract(new[] { tool })`) — the tool body is consumed unless `Fuse` is used.
- `body.RoundEdges(IDictionary<Edge, EdgeRound>)` is the single fillet/round entry point; `FixedRadiusRound(r)` for constant, `VariableRadiusRound(points)` for variable.
- `DesignBody.Create(part, name, body)` is the bridge: any modeler `Body` produced by extrude/loft/boolean must be wrapped via `DesignBody.Create` to become a persistable document object.
- `Component.Content` and any body mutation MUST run inside `WriteBlock.ExecuteTask("label", () => { ... })` — outside a write block these throw `InvalidOperationException`.
- `face.Geometry` returns the underlying `Surface` for RTTI (`is Plane`, `is Cylinder`, `is NurbsSurface`, …) — the project leverages this in `FaceNamingHelper`/`ContactDetectionService`.
- `edge.Geometry` similarly returns the underlying `Curve` (`Line` / `Circle` / `Ellipse` / `NurbsCurve`).
- `face.IsReversed` indicates whether the face's geometric normal points opposite the topology normal; final outward normal = `geometry-derived-normal * (IsReversed ? -1 : 1)`.
- `Group` is SpaceClaim's "Named Selection". Creation is `Group.Create(part, name, IDocObject[])`; the project's `NamedSelection.Delete(string[])` is the static bulk-delete helper.
- `Box | Box` (union) and `Box & Box` (intersection) operators exist on `Box` — useful for accumulating multi-body AABBs.
- `Matrix * Frame`, `Matrix * Box`, `Matrix * Point`, `Matrix * Vector`, `Matrix * Direction`, `Matrix * Curve`, `Matrix * Surface` are all defined — apply transform to any geometry primitive.
