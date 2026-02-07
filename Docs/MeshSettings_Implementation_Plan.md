# Mesh Settings 다이얼로그 구현 계획서

> 작성일: 2026-02-04
> API 레퍼런스: [SpaceClaim_Mesh_API_Reference.md](SpaceClaim_Mesh_API_Reference.md)

---

## 1. 개요

SpaceClaim API의 `Analysis.MeshBodySettings`, `Analysis.MeshMethodsStatic` 등을 활용하여,
현재 문서의 모든 DesignBody에 대해 **메쉬 크기를 일괄 설정하고 생성**하는 다이얼로그를 구현한다.

### 핵심 기능
1. **바디 리스트**: 현재 문서의 모든 솔리드 바디를 DataGridView에 표시
2. **바디별 메쉬 크기**: X, Y, Z 방향 크기 + 통합 ElementSize 자동 계산
3. **스마트 기본값**: 각 바디의 BoundingBox 기준 적절한 기본 메쉬 크기 산출
4. **키워드 필터**: 하단 텍스트 입력으로 바디 이름 필터링
5. **일괄 업데이트**: 필터된 바디들에 메쉬 크기 일괄 적용
6. **전체 적용**: 모든 바디에 MeshBodySettings 생성 → CreateMesh 실행

---

## 2. 사용할 SpaceClaim API

### 2.1 바디 탐색

```csharp
// 현재 문서의 모든 DesignBody 가져오기
Part mainPart = Window.ActiveWindow.Document.MainPart;
ICollection<IDesignBody> bodies = mainPart.GetDescendants<IDesignBody>();
```

### 2.2 바운딩 박스 (기본값 계산용)

```csharp
// DesignBody의 Shape(Body)에서 BoundingBox 획득
Box boundingBox = designBody.Shape.GetBoundingBox(Matrix.Identity);
// boundingBox.MinCorner, boundingBox.MaxCorner → X, Y, Z 범위 계산
double xSize = boundingBox.MaxCorner.X - boundingBox.MinCorner.X;  // 미터 단위
double ySize = boundingBox.MaxCorner.Y - boundingBox.MinCorner.Y;
double zSize = boundingBox.MaxCorner.Z - boundingBox.MinCorner.Z;
```

### 2.3 MeshBodySettings (바디별 메쉬 설정)

> 레퍼런스: [SpaceClaim_Mesh_API_Reference.md § 1](SpaceClaim_Mesh_API_Reference.md#1-메시-설정-mesh-body-settings)

```csharp
using SpaceClaim.Api.V252.Analysis;

var settings = MeshBodySettings.Create(designBody);
settings.ElementSize = 0.002;                           // 2mm (m 단위)
settings.SolidElementShape = ElementShape.Tetrahedral;  // 사면체
settings.MidsideNodes = MidsideNodes.Dropped;           // 1차 요소
settings.GrowthRate = 1.2;
settings.SizeFunction = SizeFunction.CurvatureAndProximity;
settings.Apply();
```

### 2.4 EdgeSizeControl (방향별 크기 제어)

> 레퍼런스: [SpaceClaim_Mesh_API_Reference.md § 2](SpaceClaim_Mesh_API_Reference.md#2-엣지-사이징-edge-size-control)

X/Y/Z 방향별 메쉬 크기를 적용하기 위해, 바디의 엣지를 방향별로 분류한 후 EdgeSizeControl 적용:

```csharp
foreach (DesignEdge edge in designBody.Edges)
{
    // 엣지 방향 벡터 계산
    Direction edgeDir = GetEdgeDirection(edge);

    // X/Y/Z 중 가장 근접한 축 판별
    double dotX = Math.Abs(edgeDir.UnitVector.X);
    double dotY = Math.Abs(edgeDir.UnitVector.Y);
    double dotZ = Math.Abs(edgeDir.UnitVector.Z);

    double targetSize;
    if (dotX > dotY && dotX > dotZ)
        targetSize = meshSizeX;  // X방향 엣지 → X 메쉬 크기
    else if (dotY > dotZ)
        targetSize = meshSizeY;  // Y방향 엣지
    else
        targetSize = meshSizeZ;  // Z방향 엣지

    var edgeSizing = EdgeSizeControl.Create(edge);
    edgeSizing.ElementSize = targetSize;
}
```

### 2.5 메쉬 생성

> 레퍼런스: [SpaceClaim_Mesh_API_Reference.md § 5](SpaceClaim_Mesh_API_Reference.md#5-메시-실행내보내기-meshmethods)

```csharp
var meshMethods = new MeshMethods();
meshMethods.CreateMesh(Window.ActiveWindow, new IDocObject[] { designBody1, designBody2, ... });
```

---

## 3. 기본값 계산 알고리즘

각 바디의 BoundingBox에서 X/Y/Z 크기를 구한 후, 적절한 메쉬 크기 산출:

```
defaultMeshSize(dimension) = dimension / TARGET_DIVISIONS
```

| 상수 | 값 | 설명 |
|------|---|------|
| `TARGET_DIVISIONS` | 10 | 각 방향 기본 분할 수 |
| `MIN_ELEMENT_SIZE` | 0.0001 (0.1mm) | 최소 요소 크기 |
| `MAX_ELEMENT_SIZE` | 0.01 (10mm) | 최대 요소 크기 |

예시:
- 바디 X축 길이 = 150mm → defaultX = 150/10 = **15mm** → Clamp → **10mm** (MAX)
- 바디 Y축 길이 = 25mm → defaultY = 25/10 = **2.5mm**
- 바디 Z축 길이 = 4mm → defaultZ = 4/10 = **0.4mm**

**ElementSize** (MeshBodySettings용) = `Min(meshX, meshY, meshZ)` → 가장 작은 방향 기준

---

## 4. UI 설계

### 4.1 다이얼로그 레이아웃

```
┌─────────────────────────────────────────────────────────────────┐
│ Mesh Settings - 격자 설정                                [X]   │
├─────────────────────────────────────────────────────────────────┤
│ ┌─ 바디 메쉬 크기 설정 ──────────────────────────────────────┐ │
│ │ ┌──────────────────────────────────────────────────────────┐│ │
│ │ │ ☑ │ Body Name       │ X(mm) │ Y(mm) │ Z(mm) │ Elem(mm)││ │
│ │ │───┼─────────────────┼───────┼───────┼───────┼─────────│││
│ │ │ ☑ │ Panel           │  10.0 │   2.5 │   0.4 │     0.4 ││ │
│ │ │ ☑ │ Jig_Left        │   5.0 │   2.0 │   1.0 │     1.0 ││ │
│ │ │ ☑ │ Jig_Right       │   5.0 │   2.0 │   1.0 │     1.0 ││ │
│ │ │ ☑ │ Adhesive_Layer  │   2.5 │   2.5 │   0.1 │     0.1 ││ │
│ │ │ ☑ │ Grip_Top        │   3.0 │   2.0 │   1.5 │     1.5 ││ │
│ │ │ ☑ │ Grip_Bottom     │   3.0 │   2.0 │   1.5 │     1.5 ││ │
│ │ └──────────────────────────────────────────────────────────┘│ │
│ └────────────────────────────────────────────────────────────┘ │
│                                                                 │
│ ┌─ 일괄 설정 ───────────────────────────────────────────────┐ │
│ │ 키워드: [____________] 메쉬크기(mm): [___2.0__] [업데이트] │ │
│ │                                                            │ │
│ │ ※ 키워드 입력 시 해당하는 바디만 필터 + 하이라이트        │ │
│ │ ※ "업데이트" 클릭 시 필터된 바디의 X/Y/Z에 일괄 적용     │ │
│ └────────────────────────────────────────────────────────────┘ │
│                                                                 │
│ ┌─ 메쉬 옵션 ───────────────────────────────────────────────┐ │
│ │ 요소 형상: [Tetrahedral ▼]  중간절점: [Dropped ▼]         │ │
│ │ 성장률:    [___1.2__]       크기함수: [CurvatureAndProx ▼]│ │
│ └────────────────────────────────────────────────────────────┘ │
│                                                                 │
│              [전체 적용 (메쉬 생성)]  [닫기]                    │
└─────────────────────────────────────────────────────────────────┘
```

### 4.2 DataGridView 컬럼 정의

| # | Column | Type | Width | Editable | 설명 |
|---|--------|------|-------|----------|------|
| 0 | `chk` | DataGridViewCheckBoxColumn | 30 | Yes | 선택 여부 |
| 1 | `BodyName` | DataGridViewTextBoxColumn | 180 | No | 바디 이름 |
| 2 | `MeshX` | DataGridViewTextBoxColumn | 80 | Yes | X방향 크기 (mm) |
| 3 | `MeshY` | DataGridViewTextBoxColumn | 80 | Yes | Y방향 크기 (mm) |
| 4 | `MeshZ` | DataGridViewTextBoxColumn | 80 | Yes | Z방향 크기 (mm) |
| 5 | `ElemSize` | DataGridViewTextBoxColumn | 80 | No | ElementSize (mm), 자동 = Min(X,Y,Z) |

### 4.3 키워드 필터 동작

1. 사용자가 `txtKeyword` TextBox에 텍스트 입력
2. `TextChanged` 이벤트에서 바디 이름에 키워드 포함 여부 판단
3. 일치하는 행 → 배경색 하이라이트 (LightYellow) + 체크
4. 불일치 행 → 기본 배경 + 체크 해제
5. 키워드 비어있으면 전체 표시, 전체 체크

### 4.4 "업데이트" 버튼 동작

1. `numBatchSize` 값 읽기 (mm)
2. DataGridView에서 체크된(☑) 행만 순회
3. 해당 행의 X, Y, Z, ElemSize 컬럼을 `numBatchSize` 값으로 업데이트
4. (또는 X/Y/Z를 각각 바디 비율에 맞게 스케일링하는 옵션도 고려 가능)

### 4.5 "전체 적용" 버튼 동작

```
1. WriteBlock.Create(document, "Mesh Settings") 트랜잭션 시작
2. 각 바디에 대해:
   a. MeshBodySettings.Create(designBody) 생성
   b. ElementSize = Min(X, Y, Z) / 1000.0  (mm→m 변환)
   c. SolidElementShape, MidsideNodes, GrowthRate, SizeFunction 적용
   d. settings.Apply()
   e. (옵션) 방향별 크기 차이가 큰 경우 EdgeSizeControl 추가 적용
3. MeshMethods.CreateMesh(window, checkedBodies) 실행
4. 트랜잭션 커밋
5. 결과 메시지 표시 (성공/실패, 노드/요소 수)
```

---

## 5. 파일 구조

### 신규 파일 (5개)

| # | 파일 | 내용 |
|---|------|------|
| 1 | `Services\Mesh\MeshSettingsService.cs` | 기본값 계산, MeshBodySettings 적용, CreateMesh 실행 |
| 2 | `Commands\Mesh\ApplyMeshSettingsCommand.cs` | 리본 커맨드 |
| 3 | `UI\Dialogs\MeshSettingsDialog.cs` | 다이얼로그 로직 (이벤트 핸들링, 필터, 업데이트) |
| 4 | `UI\Dialogs\MeshSettingsDialog.Designer.cs` | WinForms 레이아웃 (DataGridView + 일괄설정 패널) |
| 5 | `Resources\Icons\Mesh_32.png` | 아이콘 (32x32 PNG) |

### 수정 파일 (3개)

| 파일 | 변경 내용 |
|------|-----------|
| `AddIn.cs` | Mesh 커맨드 등록 + 리본 XML에 Mesh 버튼 추가 |
| `Core\UI\IconHelper.cs` | `MeshIcon` 프로퍼티 추가 |
| `MXDigitalTwinModeller.csproj` | 신규 파일 Compile + EmbeddedResource 추가 |

---

## 6. 클래스 설계

### 6.1 MeshSettingsService

```csharp
namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.Mesh
{
    public static class MeshSettingsService
    {
        // 상수
        const int TARGET_DIVISIONS = 10;
        const double MIN_SIZE_M = 0.0001;  // 0.1mm
        const double MAX_SIZE_M = 0.01;    // 10mm

        /// <summary>
        /// 바디의 BoundingBox에서 X/Y/Z 기본 메쉬 크기 계산 (mm 반환)
        /// </summary>
        public static void ComputeDefaultSizes(DesignBody body,
            out double xMm, out double yMm, out double zMm)

        /// <summary>
        /// 바디에 MeshBodySettings 적용
        /// </summary>
        public static void ApplyMeshSettings(DesignBody body,
            double elementSizeMm,
            ElementShape shape,
            MidsideNodes midsideNodes,
            double growthRate,
            SizeFunction sizeFunction)

        /// <summary>
        /// 방향별 EdgeSizeControl 적용 (X/Y/Z 크기가 다를 경우)
        /// </summary>
        public static void ApplyDirectionalSizing(DesignBody body,
            double xMm, double yMm, double zMm)

        /// <summary>
        /// 선택된 바디 목록에 대해 메쉬 생성 실행
        /// </summary>
        public static void GenerateMesh(Window window,
            ICollection<IDocObject> bodies)
    }
}
```

### 6.2 MeshSettingsDialog 주요 멤버

```csharp
namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.UI.Dialogs
{
    public partial class MeshSettingsDialog : Form
    {
        private Part _part;
        private List<DesignBody> _allBodies;

        // DataGridView: dgvBodies
        // TextBox: txtKeyword
        // NumericUpDown: numBatchSize
        // Button: btnUpdate, btnApplyAll, btnClose
        // ComboBox: cmbElementShape, cmbMidsideNodes, cmbSizeFunction
        // NumericUpDown: numGrowthRate

        // 초기화: 바디 목록 로드 + 기본값 계산
        private void LoadBodies()

        // 키워드 필터
        private void txtKeyword_TextChanged(...)

        // 일괄 업데이트
        private void btnUpdate_Click(...)

        // 전체 적용 (메쉬 생성)
        private void btnApplyAll_Click(...)
    }
}
```

---

## 7. 리본 배치

Mesh 버튼은 기존 `AdvancedGroup`에 추가하거나, 별도 `MeshGroup` 생성:

```xml
<group id="MXDigitalTwinModeller.MeshGroup" command="MXDigitalTwinModeller.MeshGroup">
    <button id="MXDigitalTwinModeller.ApplyMeshSettings"
            size="large"
            command="MXDigitalTwinModeller.ApplyMeshSettings"/>
</group>
```

→ 기존 `AdvancedGroup` 뒤에 `MeshGroup` 추가

---

## 8. 엣지 방향 판별 알고리즘

바디의 각 엣지를 X/Y/Z 축과 비교하여 가장 근접한 축 판별:

```
1. 엣지 시작점(P0), 끝점(P1) 획득
2. direction = (P1 - P0).Normalize()
3. dotX = |direction · (1,0,0)|
4. dotY = |direction · (0,1,0)|
5. dotZ = |direction · (0,0,1)|
6. 가장 큰 dot product의 축 → 해당 축의 메쉬 크기 적용
```

곡선 엣지의 경우:
- 시작점↔끝점의 직선 방향으로 근사
- 또는 기본 ElementSize 사용 (방향 판별 불가 시)

---

## 9. 예외 처리

| 상황 | 처리 |
|------|------|
| 바디 없음 | "현재 문서에 솔리드 바디가 없습니다" 경고 후 닫기 |
| BoundingBox 실패 | 기본값 2.0mm 사용 |
| MeshBodySettings.Create 실패 | 해당 바디 건너뛰고 경고 로그 |
| CreateMesh 실패 | 에러 메시지 표시, 트랜잭션 롤백 |
| 0 또는 음수 입력 | NumericUpDown의 Minimum으로 clamping |

---

## 10. 구현 순서

| 단계 | 작업 | 의존성 |
|------|------|--------|
| 1 | `Services\Mesh\MeshSettingsService.cs` 작성 | API 레퍼런스 |
| 2 | `UI\Dialogs\MeshSettingsDialog.Designer.cs` 레이아웃 | 없음 |
| 3 | `UI\Dialogs\MeshSettingsDialog.cs` 로직 | Service, Designer |
| 4 | `Commands\Mesh\ApplyMeshSettingsCommand.cs` 커맨드 | Dialog |
| 5 | `Resources\Icons\Mesh_32.png` 아이콘 생성 | 없음 |
| 6 | `IconHelper.cs` + `AddIn.cs` + `csproj` 통합 | 1~5 완료 후 |
| 7 | Build + 검증 | 6 완료 후 |

---

## 11. 검증 항목

- [ ] 빌드 0 errors, 0 warnings
- [ ] 리본에 Mesh 아이콘 표시
- [ ] 다이얼로그 열기 → 바디 목록 정상 표시
- [ ] 기본 메쉬 크기가 바디 크기에 비례하여 합리적
- [ ] 키워드 필터 정상 동작 (대소문자 무시)
- [ ] "업데이트" → 필터된 바디 X/Y/Z 값 일괄 변경
- [ ] "전체 적용" → MeshBodySettings 적용 + 메쉬 생성 실행
- [ ] 메쉬 생성 후 SpaceClaim에서 메쉬 시각화 확인
