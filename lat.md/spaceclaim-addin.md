# SpaceClaim Add-In

SpaceClaim 에 `MX Modeller` 리본 탭을 추가하는 C# .NET 4.7.2 AddIn. 시편 모델링, 메쉬, 접촉 검출, Conformal Mesh 등 ANSYS 해석 전처리의 거의 모든 단계를 자동화한다.

진입점은 `[[AddIn.cs#MXAddIn]]`. 모든 리본 버튼은 `commandCapsules` 배열에 등록된 Command 클래스로 매핑되고, 각 Command 는 `BaseCommandCapsule` 을 상속해 클릭 시 다이얼로그를 띄운다.

## 리본 구조

### MX Modeller 탭

| 그룹 | 버튼 | 정의 |
|---|---|---|
| **Specimen** | ASTM/DMA Tensile, DMA 3-pt Bending, Bending Fixture, Compression | [[specimens]] |
| **Advanced** | CAI, Fatigue, Joint | [[specimens]] |
| **Parametric** | Laminate, VoidCut | [[specimens]], [[mesh#Void Cut]] |
| **Mesh** | Material, ApplyMeshSettings, ExportMesh, Simplify, DetectContact, ExportStep, Load, SimulationSetup, BatchPipeline, ConformalMesh | [[mesh]] |
| **DT Mesher** | GmshMesher | [[mesh#Gmsh Mesher]] |

## 아키텍처 패턴

모든 기능이 동일한 4계층 패턴을 따른다:

```
Commands/<Feature>/<Feature>Command.cs   ← 리본 버튼 → 다이얼로그 트리거
Models/<Feature>/<Feature>Parameters.cs  ← 입력 데이터 모델
UI/Dialogs/<Feature>Dialog.cs            ← WinForms 다이얼로그
Services/<Feature>/<Feature>Service.cs   ← 실제 SpaceClaim API 호출 (모델링/메쉬/내보내기)
```

### 공통 베이스

- `[[Core/Commands/BaseCommandCapsule.cs]]` — 모든 Command 의 부모
- `[[Core/UI/IconHelper.cs]]` — 리본 아이콘 로드 (`Resources/Icons/*.png` 에서)
- `[[Core/Geometry/]]` — 공통 기하 유틸
- `[[Core/IO/YamlWriter.cs]]` — 시편 메타데이터 YAML 직렬화 ([[pipeline#Specimen YAML]] 에서 사용)

## 빌드/배포

자세한 빌드 절차는 [[build-deploy#출력 경로]] 참조. 핵심 사실:

- 빌드 산출물: `C:\ProgramData\SpaceClaim\AddIns\MXDigitalTwinModeller\V252\`
- 빌드 전 SpaceClaim 종료 필수 (DLL 잠금)
- 멀티버전 (V251/V252) 지원: 자세한 내용은 [[architecture#멀티버전 지원]]
