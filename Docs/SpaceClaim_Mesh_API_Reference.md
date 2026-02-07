# SpaceClaim API V252 - Mesh 관련 API 레퍼런스

> DLL: `SpaceClaim.Api.V252.dll`
> 리플렉션 기반 조사 (2026-02-04)
> 네임스페이스: `SpaceClaim.Api.V252.Analysis`, `SpaceClaim.Api.V252.Modeler`, `SpaceClaim.Api.V252.Display`

---

## 목차

1. [메시 설정 (Mesh Body Settings)](#1-메시-설정-mesh-body-settings)
2. [엣지 사이징 (Edge Size Control)](#2-엣지-사이징-edge-size-control)
3. [면 사이징 (Face Size Control)](#3-면-사이징-face-size-control)
4. [면 스타일 제어 (Mesh Face Style Control)](#4-면-스타일-제어-mesh-face-style-control)
5. [메시 실행/내보내기 (MeshMethods)](#5-메시-실행내보내기-meshmethods)
6. [헥사 블로킹 (HexaBlocking)](#6-헥사-블로킹-hexablocking)
7. [메시 결과 조회](#7-메시-결과-조회)
8. [Enum 목록](#8-enum-목록)
9. [테셀레이션 (Tessellation)](#9-테셀레이션-tessellation)
10. [DesignMesh (메시 객체)](#10-designmesh-메시-객체)
11. [MeshPrimitive (시각화용)](#11-meshprimitive-시각화용)
12. [Modeler.Mesh (토폴로지)](#12-modelermesh-토폴로지)
13. [블로킹 (Blocking)](#13-블로킹-blocking)
14. [사용 예시 코드](#14-사용-예시-코드)

---

## 1. 메시 설정 (Mesh Body Settings)

**네임스페이스**: `SpaceClaim.Api.V252.Analysis`
**클래스**: `MeshBodySettings`
**역할**: 바디 단위 메시 파라미터 설정 (SpaceClaim Mesh 탭의 핵심 기능)

### 생성

```csharp
MeshBodySettings.Create(DesignBody referenceBody)
IMeshBodySettings.Create(IDesignBody referenceBody)
```

### 프로퍼티 (모두 get/set)

| 프로퍼티 | 타입 | 설명 |
|----------|------|------|
| `ElementSize` | `double?` | 요소 크기 (m) |
| `SolidElementShape` | `ElementShape?` | 솔리드 요소 형상 (Tet, Hex 등) |
| `FaceElementShape` | `ElementShape?` | 면 요소 형상 (Tri, Quad 등) |
| `GrowthRate` | `double?` | 메시 성장률 |
| `DefeaturingTolerance` | `double?` | 디피처링 허용치 |
| `SizeFunction` | `SizeFunction?` | 크기 함수 (Curvature/Proximity/Fixed) |
| `CurvatureMinimumSize` | `double?` | 곡률 기반 최소 요소 크기 |
| `CurvatureNormalAngle` | `double?` | 곡률 노말 각도 |
| `ProximityMinimumSize` | `double?` | 근접도 기반 최소 요소 크기 |
| `NumberOfCellsAcrossGap` | `int?` | 갭 당 셀 수 |
| `ConnectTolerance` | `double?` | 연결 허용치 |
| `MidsideNodes` | `MidsideNodes` | 중간절점 (Dropped/Kept/BasedOnPhysics) |
| `RemoveSmallHoles` | `bool?` | 작은 구멍 자동 제거 |
| `HoleRemovalTolerance` | `double?` | 구멍 제거 허용치 |
| `ForceStraightEdgeSides` | `bool?` | 직선 엣지 측면 강제 |
| `ProximitySizeFunctionSources` | `ProximitySizeFunctionSources` | 근접도 크기 함수 소스 |
| `ProximityDirection` | `ProximityDirection` | 근접도 방향 |
| `MeshBasedDefeaturing` | `MeshBasedDefeaturing` | 메시 기반 디피처링 |
| `PatchConformingTriangleSurfaceMesher` | `PatchConformingTriangleSurfaceMesher` | 삼각형 면 메셔 유형 |
| `BlockingDecomposition` | `BlockingDecomposition` | 블로킹 분해 방법 |
| `Bodies` | `ICollection<DesignBody>` | 적용 대상 바디 (읽기 전용) |
| `SweepSourceFaces` | `ICollection<DesignFace>` | 스윕 소스 면 (읽기 전용) |

### 메서드

| 메서드 | 설명 |
|--------|------|
| `Apply()` | 설정 적용 |
| `SetSweepSourceFaces(ICollection<DesignFace>)` | 스윕 메시용 소스 면 지정 |

---

## 2. 엣지 사이징 (Edge Size Control)

**클래스**: `EdgeSizeControl`
**역할**: 특정 엣지에 메시 크기/분할 수 지정

### 생성

```csharp
EdgeSizeControl.Create(DesignEdge referenceEdge)
```

### 프로퍼티

| 프로퍼티 | 타입 | 설명 |
|----------|------|------|
| `ElementSize` | `double?` | 엣지 요소 크기 |
| `ElementCount` | `int?` | 엣지 분할 수 |
| `BiasType` | `BiasType` | 바이어스 유형 (Right/Left/Out/In/None/Edge) |
| `BiasOption` | `BiasOption` | 바이어스 옵션 (BiasFactor/SmoothTransition) |
| `BiasFactor` | `double?` | 바이어스 팩터 |
| `BiasGrowthRate` | `double?` | 바이어스 성장률 |
| `Edges` | `ICollection<DesignEdge>` | 적용 대상 엣지 (읽기 전용) |

---

## 3. 면 사이징 (Face Size Control)

**클래스**: `FaceSizeControl`
**역할**: 특정 면에 메시 크기 지정

### 생성

```csharp
FaceSizeControl.Create(DesignFace referenceFace)
```

### 프로퍼티

| 프로퍼티 | 타입 | 설명 |
|----------|------|------|
| `ElementSize` | `double?` | 면 요소 크기 |
| `Faces` | `ICollection<DesignFace>` | 적용 대상 면 (읽기 전용) |

---

## 4. 면 스타일 제어 (Mesh Face Style Control)

**클래스**: `MeshFaceStyleControl`
**역할**: 면의 메시 스타일 제어

### 프로퍼티

| 프로퍼티 | 타입 | 설명 |
|----------|------|------|
| `Faces` | `ICollection<DesignFace>` | 적용 대상 면 |
| `Parent` | `Part` | 부모 파트 |

---

## 5. 메시 실행/내보내기 (MeshMethods)

**클래스**: `MeshMethods` / `MeshMethodsStatic`
**역할**: 메시 생성 실행 및 다양한 솔버 포맷 내보내기

### 메시 생성/업데이트

| 메서드 | 설명 |
|--------|------|
| `CreateMesh(Window window, ICollection<IDocObject> objects)` | 메시 생성 실행 |
| `UpdateMesh()` | 메시 업데이트 |
| `EnableMeshing()` | 메싱 활성화 |
| `InitializeDelayedLoadedMeshing()` | 지연 로드 메싱 초기화 |

### 메시 내보내기

| 메서드 | 포맷 | 확장자 |
|--------|------|--------|
| `SaveANSYS(string path)` | ANSYS Mechanical | `.cdb` |
| `SaveFluentMesh(string path)` | ANSYS Fluent | `.msh` |
| `SaveAbaqus(string path)` | Abaqus | `.inp` |
| `SaveDYNA(string path)` | LS-DYNA | `.k` |
| `SaveCGNS(string path)` | CGNS | `.cgns` |

> 반환값 `int`: 0이면 성공

### 메시 조회

| 메서드 | 설명 |
|--------|------|
| `GetAssemblyMesh(Document document)` | 어셈블리 전체 메시 조회 |
| `GetBodyMesh(IDocObject obj)` | 바디 메시 조회 |
| `GetHexaBlocking(IDocObject obj)` | 헥사 블로킹 조회 |
| `UpdateBlocking(IDocObject obj)` | 블로킹 업데이트 |
| `GetNodesForTopology(IDocObject obj)` | 토폴로지별 노드 조회 |

---

## 6. 헥사 블로킹 (HexaBlocking)

**클래스**: `HexaBlocking`
**역할**: 구조적 헥사헤드럴 메시 (블록 기반)

### 메서드

| 메서드 | 설명 |
|--------|------|
| `ProcessCommand(string cmd)` | 블로킹 명령 실행 (bool 반환) |
| `ProcessCommand(string cmd, out string errorMessage)` | 명령 실행 + 에러 메시지 |
| `CreateMesh()` | 메시 생성 |

### 프로퍼티 (읽기 전용)

| 프로퍼티 | 타입 | 설명 |
|----------|------|------|
| `SuperNodeCount` | `int` | 슈퍼 노드 수 |
| `SuperEdgeCount` | `int` | 슈퍼 엣지 수 |
| `SuperFaceCount` | `int` | 슈퍼 면 수 |
| `CornerNodeCount` | `int` | 코너 노드 수 |
| `ElementCount` | `int` | 요소 수 |
| `MappedBlockCount` | `int` | 매핑 블록 수 |
| `SweptBlockCount` | `int` | 스윕 블록 수 |
| `FreeBlockCount` | `int` | 자유 블록 수 |
| `MinElementQuality` | `double` | 최소 요소 품질 |

---

## 7. 메시 결과 조회

### AssemblyMesh

| 프로퍼티 | 타입 | 설명 |
|----------|------|------|
| `NodeCount` | `int` | 전체 노드 수 |
| `ElementCount` | `int` | 전체 요소 수 |
| `PartMeshes` | `ICollection<PartMesh>` | 파트별 메시 |

### PartMesh

| 프로퍼티 | 타입 | 설명 |
|----------|------|------|
| `Id` | `int` | 파트 ID |
| `NodeCount` | `int` | 노드 수 |
| `ElementCount` | `int` | 요소 수 |
| `BodyMeshes` | `ICollection<BodyMesh>` | 바디별 메시 |

### BodyMesh

| 프로퍼티 | 타입 | 설명 |
|----------|------|------|
| `Id` | `int` | 바디 ID |
| `NodeCount` | `int` | 노드 수 |
| `ElementCount` | `int` | 요소 수 |
| `Nodes` | `ICollection<MeshNode>` | 메시 노드 목록 |
| `VolumeElements` | `ICollection<VolumeElement>` | 체적 요소 |
| `FaceElements` | `ICollection<FaceElement>` | 면 요소 |
| `EdgeElements` | `ICollection<EdgeElement>` | 엣지 요소 |

### MeshNode

| 프로퍼티 | 타입 | 설명 |
|----------|------|------|
| `Id` | `int` | 노드 ID |
| `Point` | `Point` | 좌표 |

### VolumeElement / FaceElement / EdgeElement

| 프로퍼티 | 타입 | 설명 |
|----------|------|------|
| `Id` | `int` | 요소 ID |
| `Type` | `ElementType` | 요소 타입 |

### BlockMeshAnalysis

| 프로퍼티 | 타입 | 설명 |
|----------|------|------|
| `Parent` | `DesignBody` | 부모 바디 |
| `NodeCount` | `int` | 노드 수 |
| `ElementCount` | `int` | 요소 수 |

---

## 8. Enum 목록

### ElementShape (요소 형상)

```
QuadDominant    // 쿼드 위주
Triangle        // 삼각형
AllQuad         // 전체 쿼드
Tetrahedral     // 사면체
Hexahedral      // 육면체
TriPrime        // 삼각형 프라임
QuadPrime       // 쿼드 프라임
```

### SizeFunction (크기 함수)

```
CurvatureAndProximity   // 곡률 + 근접도
Curvature               // 곡률만
Proximity               // 근접도만
Fixed                   // 고정 크기
```

### MidsideNodes (중간절점)

```
Dropped          // 1차 요소 (중간절점 없음)
Kept             // 2차 요소 (중간절점 유지)
BasedOnPhysics   // 물리 기반 자동 결정
```

### BiasType (바이어스 유형)

```
Right   // 우측으로 조밀
Left    // 좌측으로 조밀
Out     // 바깥으로 조밀
In      // 안쪽으로 조밀
None    // 바이어스 없음
Edge    // 엣지 기반
```

### BiasOption (바이어스 옵션)

```
BiasFactor        // 바이어스 팩터 지정
SmoothTransition  // 부드러운 전이
```

### BlockingDecomposition (블로킹 분해)

```
Automatic    // 자동
Standard     // 표준
Aggressive   // 적극적
Free         // 자유
BoundingBox  // 바운딩 박스
Load         // 로드
None         // 없음
CartSweep    // 카트 스윕
ThinSweep    // 씬 스윕
```

### MeshBasedDefeaturing (메시 기반 디피처링)

```
AutomaticallyDetermined  // 자동 결정
UserDefined              // 사용자 정의
Off                      // 끄기
```

### ProximitySizeFunctionSources (근접도 소스)

```
FacesAndEdges   // 면 + 엣지
Faces           // 면만
Edges           // 엣지만
```

### ProximityDirection (근접도 방향)

```
Automatic   // 자동
Outwards    // 외부
Inwards     // 내부
Both        // 양방향
```

### PatchConformingTriangleSurfaceMesher (면 메셔)

```
AutomaticallyDetermined  // 자동
AdvancingFront           // Advancing Front 알고리즘
```

### DesignMeshStyle (디자인 메시 스타일)

```
Opaque       // 불투명
Transparent  // 투명
Default      // 기본값
```

### MeshEdgeDisplay (메시 엣지 표시)

```
None                       // 표시 안함
MeshJunctions              // 메시 교차점
MeshJunctionsUnlessSmooth  // 부드럽지 않은 교차점만
ExteriorBoundary           // 외부 경계만
Polygons                   // 폴리곤 경계
```

---

## 9. 테셀레이션 (Tessellation)

**클래스**: `SpaceClaim.Api.V252.Modeler.TessellationOptions`
**역할**: CAD 면의 시각화용 삼각형 분할 제어 (FEA 메시 아님)

### 프로퍼티

| 프로퍼티 | 타입 | 기본값 | 설명 |
|----------|------|--------|------|
| `SurfaceDeviation` | `double` | 0.00075 | 표면 최대 편차 (m) |
| `CurveDeviation` | `double` | 0.00075 | 곡선 최대 편차 (m) |
| `AngleDeviation` | `double` | ~0.349 | 노말 최대 편차 (rad, ~20도) |
| `MaximumAspectRatio` | `double` | 0 | 최대 종횡비 (0=무제한) |
| `MaximumEdgeLength` | `double` | 0 | 최대 엣지 길이 (0=무제한) |
| `Watertight` | `bool` | true | 수밀 보장 여부 |

### 사용

```csharp
var opts = new TessellationOptions(0.001, 0.001, 0.349, 0, 0.005, true);
IDictionary<Face, FaceTessellation> tess = body.GetTessellation(faces, opts);
```

### FaceTessellation

| 프로퍼티 | 타입 | 설명 |
|----------|------|------|
| `Vertices` | `IList<Point>` | 정점 좌표 |
| `Facets` | `ICollection<Facet>` | 삼각형 인덱스 |

### Facet (구조체)

| 프로퍼티 | 타입 | 설명 |
|----------|------|------|
| `Vertex0` | `int` | 첫 번째 정점 인덱스 |
| `Vertex1` | `int` | 두 번째 정점 인덱스 |
| `Vertex2` | `int` | 세 번째 정점 인덱스 |

---

## 10. DesignMesh (메시 객체)

**클래스**: `SpaceClaim.Api.V252.DesignMesh`
**역할**: 삼각형 메시 객체 생성/관리 (STL 임포트 등)

### 생성

```csharp
DesignMesh.Create(Part parent, string name, IList<Point> vertices, IList<Facet> facets)
DesignMesh.Create(Part parent, string name, IList<PointF> vertices, IList<Facet> facets)
DesignMesh.Create(Part parent, string name, float[] vertices, int[] facets)
```

### 프로퍼티

| 프로퍼티 | 타입 | 설명 |
|----------|------|------|
| `Name` | `string` | 이름 (get/set) |
| `Shape` | `Mesh` | 메시 토폴로지 |
| `Parent` | `Part` | 부모 파트 |
| `MasterTransform` | `Matrix` | 변환 행렬 |
| `Style` | `DesignMeshStyle` | 스타일 (Opaque/Transparent) |
| `Material` | `DocumentMaterial` | 재질 |
| `SurfaceMaterial` | `SurfaceMaterial` | 표면 재질 |
| `Layer` | `Layer` | 레이어 |
| `CanSuppress` | `bool` | 억제 가능 여부 |
| `IsSuppressed` | `bool` | 억제 상태 (get/set) |

### 메서드

| 메서드 | 설명 |
|--------|------|
| `GetColor(IAppearanceContext)` | 색상 조회 |
| `SetColor(IAppearanceContext, Color?)` | 색상 설정 |
| `GetVisibility(IAppearanceContext)` | 가시성 조회 |
| `SetVisibility(IAppearanceContext, bool?)` | 가시성 설정 |
| `GetFaceColor(MeshFace)` | 면 색상 조회 |
| `SetFaceColor(MeshFace, Color?)` | 면 색상 설정 |
| `SetFaceColor(ICollection<MeshFace>, Color?)` | 복수 면 색상 설정 |
| `GetDesignMeshTopology(MeshTopology)` | 토폴로지 → 디자인 객체 |
| `GetDesignMeshRegion(ICollection<MeshFace>)` | 면 그룹 → 리전 |
| `GetCollision(IDesignMesh, double)` | 충돌 검사 |
| `Transform(Matrix)` | 변환 적용 |
| `Warp(string deformedX, Y, Z, double percentage, double epsilon)` | 변형 |

---

## 11. MeshPrimitive (시각화용)

**클래스**: `SpaceClaim.Api.V252.Display.MeshPrimitive`
**역할**: 렌더링 전용 메시 프리미티브 (Tool의 Display에서 사용)

### 정적 메서드

| 메서드 | 설명 |
|--------|------|
| `CreatePolygons(IList<Point> vertices, ICollection<Polygon> polygons)` | 폴리곤 메시 |
| `CreatePolygons(IList<PositionNormal>, ICollection<Polygon>)` | 노말 포함 |
| `CreatePolygons(IList<PositionColored>, ICollection<PolygonColored>)` | 색상 포함 |
| `CreateFacets(IList<Point>, ICollection<Facet>)` | 삼각형 메시 |
| `CreateFacets(IList<PositionNormal>, ICollection<Facet>)` | 노말 포함 |
| `CreatePlanar(IList<Point>, Direction, ICollection<Facet>)` | 평면 메시 |
| `CreatePlanar(Profile profile)` | 프로파일 → 메시 |
| `CreateShape(ISquare/IRectangle/IRegularPolygon/IStar/IArrow)` | 형상 → 메시 |
| `CreateCylinder(Frame, int nSides, double radius, double height)` | 원통 |
| `CreateCone(Frame, int nSides, double radius, double height)` | 원뿔 |
| `CreateHemisphere(Frame, int nLat, int nLon, double radius)` | 반구 |

---

## 12. Modeler.Mesh (토폴로지)

**클래스**: `SpaceClaim.Api.V252.Modeler.Mesh`
**역할**: 메시 기하학적 토폴로지

### 프로퍼티

| 프로퍼티 | 타입 | 설명 |
|----------|------|------|
| `Faces` | `IList<MeshFace>` | 면 목록 |
| `Edges` | `IList<MeshEdge>` | 엣지 목록 |
| `Vertices` | `IList<MeshVertex>` | 정점 목록 |
| `IsClosed` | `bool` | 닫힌 메시 여부 |
| `IsManifold` | `bool` | 매니폴드 여부 |
| `Volume` | `double` | 체적 |
| `SurfaceArea` | `double` | 표면적 |

### 메서드

| 메서드 | 설명 |
|--------|------|
| `GetCollision(ITrimmedSpace)` | 충돌 검사 |
| `GetClosestSeparation(ITrimmedGeometry)` | 최근접 거리 |
| `GetBoundingBox(Matrix)` | 바운딩 박스 |
| `GetExtremePoint(Direction, Direction, Direction)` | 극점 |
| `ProjectPoint(Point)` | 점 투영 |
| `ContainsPoint(Point)` | 점 포함 여부 |
| `GetConnectedFaces()` | 연결된 면 그룹 |

### MeshFace

| 프로퍼티 | 설명 |
|----------|------|
| `Normal` | 면 법선 |
| `Vertices` | 정점 목록 |
| `Edges` | 엣지 목록 |
| `AdjacentFaces` | 인접 면 목록 |

### MeshEdge

| 프로퍼티 | 설명 |
|----------|------|
| `Direction` | 방향 |
| `Vertices` | 정점 목록 |
| `Faces` | 소속 면 목록 |

### MeshVertex

| 프로퍼티 | 설명 |
|----------|------|
| `Position` | 좌표 |
| `Edges` | 소속 엣지 |
| `Faces` | 소속 면 |

---

## 13. 블로킹 (Blocking)

**클래스**: `SpaceClaim.Api.V252.Blocking`
**역할**: 구조적 메시 블로킹 토폴로지

### 프로퍼티

| 프로퍼티 | 타입 | 설명 |
|----------|------|------|
| `BlockVertices` | `ICollection<IBlockVertex>` | 블록 정점 |
| `BlockEdges` | `ICollection<IBlockEdge>` | 블록 엣지 |
| `BlockFaces` | `ICollection<IBlockFace>` | 블록 면 |
| `BlockVolumes` | `ICollection<IBlockVolume>` | 블록 볼륨 |
| `BlockMaterials` | `ICollection<IBlockMaterial>` | 블록 재질 |
| `Layer` | `Layer` | 레이어 (get/set) |

---

## 14. 사용 예시 코드

### 바디 메시 설정 + 메시 생성

```csharp
using SpaceClaim.Api.V252.Analysis;

// 바디에 메시 설정 생성
var meshSettings = MeshBodySettings.Create(designBody);
meshSettings.ElementSize = 0.002;                            // 2mm
meshSettings.SolidElementShape = ElementShape.Tetrahedral;   // 사면체
meshSettings.MidsideNodes = MidsideNodes.Kept;               // 2차 요소
meshSettings.SizeFunction = SizeFunction.CurvatureAndProximity;
meshSettings.GrowthRate = 1.2;
meshSettings.Apply();

// 메시 생성 실행
var meshMethods = new MeshMethods();
meshMethods.CreateMesh(Window.ActiveWindow, new IDocObject[] { designBody });
```

### 엣지/면 사이징

```csharp
// 특정 엣지에 사이징 적용
var edgeSizing = EdgeSizeControl.Create(designEdge);
edgeSizing.ElementSize = 0.0005;   // 0.5mm
edgeSizing.ElementCount = 20;       // 또는 분할 수 지정
edgeSizing.BiasType = BiasType.In;  // 안쪽으로 조밀

// 특정 면에 사이징 적용
var faceSizing = FaceSizeControl.Create(designFace);
faceSizing.ElementSize = 0.001;     // 1mm
```

### 메시 내보내기

```csharp
var meshMethods = new MeshMethods();

// ANSYS Mechanical (.cdb)
int result = meshMethods.SaveANSYS(@"C:\output\specimen.cdb");

// LS-DYNA (.k)
meshMethods.SaveDYNA(@"C:\output\specimen.k");

// Abaqus (.inp)
meshMethods.SaveAbaqus(@"C:\output\specimen.inp");

// Fluent (.msh)
meshMethods.SaveFluentMesh(@"C:\output\specimen.msh");

// CGNS
meshMethods.SaveCGNS(@"C:\output\specimen.cgns");
```

### 메시 결과 조회

```csharp
var meshMethods = new MeshMethods();

// 어셈블리 전체 메시 정보
var asmMesh = meshMethods.GetAssemblyMesh(document);
Console.WriteLine($"Total nodes: {asmMesh.NodeCount}, elements: {asmMesh.ElementCount}");

foreach (var partMesh in asmMesh.PartMeshes)
{
    foreach (var bodyMesh in partMesh.BodyMeshes)
    {
        Console.WriteLine($"Body {bodyMesh.Id}: {bodyMesh.NodeCount} nodes, {bodyMesh.ElementCount} elements");

        // 노드 좌표 접근
        foreach (var node in bodyMesh.Nodes)
            Console.WriteLine($"  Node {node.Id}: {node.Point}");
    }
}
```

### 헥사 블로킹

```csharp
var meshMethods = new MeshMethods();
var hexaBlock = meshMethods.GetHexaBlocking(designBody);

// 블로킹 명령 실행
string error;
hexaBlock.ProcessCommand("some_blocking_command", out error);

// 메시 생성
hexaBlock.CreateMesh();

// 품질 확인
Console.WriteLine($"Min quality: {hexaBlock.MinElementQuality}");
Console.WriteLine($"Elements: {hexaBlock.ElementCount}");
```

### MeshColorMap (편차 시각화)

```csharp
// 정점별 편차를 색상으로 매핑
var vertexParams = new Dictionary<int, PointUV>();
for (int i = 0; i < vertexCount; i++)
{
    double normalized = deviations[i] / maxDeviation;  // 0~1
    vertexParams[i] = PointUV.Create(normalized, 0);
}

var colorMap = new MeshColorMap(colorRampBitmap, Color.Gray, vertexParams);
```

---

## 참고

### API 문서 위치

- `D:\Program Files\ANSYS Inc\ANSYS Student\v252\scdm\SpaceClaim.Api.V252\API_Class_Library.chm`
- `D:\Program Files\ANSYS Inc\ANSYS Student\v252\scdm\SpaceClaim.Api.V252\SpaceClaim_API.chm`
- `D:\Program Files\ANSYS Inc\ANSYS Student\v252\scdm\SpaceClaim.Api.V252\SpaceClaim.Api.V252.xml`

### 관련 DLL

| DLL | 위치 | 설명 |
|-----|------|------|
| `SpaceClaim.Api.V252.dll` | scdm/SpaceClaim.Api.V252/ | 메인 API (현재 참조) |
| `Mesh.dll` | scdm/ | 코어 메시 엔진 (4.6MB) |
| `AnsysMeshModeler.dll` | scdm/ | ANSYS 메시 모델러 (556KB) |

### 샘플 코드 위치

| 파일 | 내용 |
|------|------|
| `V252/SampleAddIn/Commands/PolygonMesh.cs` | MeshPrimitive 시각화 |
| `V252/SampleAddIn/Commands/DeviationTool.cs` | MeshColorMap 편차 시각화 |
| `V252/RegressionTestAddIn/Commands/CreateMeshFromProfileCommand.cs` | 프로파일 → 메시 |

### 주의사항

- 메시 설정(`MeshBodySettings`)은 **SpaceClaim Mesh 탭 내에서만** 유효
- Workbench 경유 시 Mechanical은 **자체 메시 엔진**을 사용 (SpaceClaim 설정 미전달)
- `SaveANSYS()`로 `.cdb` 내보내기 후 Mechanical에서 **External Model**로 읽는 방법은 가능
- 단위는 SpaceClaim 기본 **미터(m)** 기준
