---
lat:
  require-code-mention: true
---

# Specimens

표준 물성 측정 시험 시편을 자동으로 생성한다. 사용자는 다이얼로그에서 규격(ASTM/ISO)과 치수를 선택하면 SpaceClaim 의 Body 가 자동으로 모델링된다. 인장, 굽힘, CAI, 피로, Joint, 적층 등 6대 카테고리.

각 시편은 `Commands/<카테고리>/Create*Command.cs` → `UI/Dialogs/<카테고리>SpecimenDialog.cs` → `Services/<카테고리>/<카테고리>SpecimenService.cs` 흐름. 자세한 4계층 패턴은 [[spaceclaim-addin#아키텍처 패턴]] 참조.

## ASTM/DMA 인장시편

ASTM E8 (금속), ASTM D638 (플라스틱), DMA Tensile (ASTM D4065) 을 단일 다이얼로그에서 선택할 수 있다. 게이지 길이/폭과 dog-bone 곡률을 자동 적용.

지원 규격:
- ASTM E8 Standard: GL 50mm, GW 12.5mm, Total 200mm
- ASTM E8 SubSize: GL 25mm, GW 6mm, Total 100mm
- ASTM D638 Type I: GL 50mm, GW 13mm, Total 165mm
- ASTM D638 Type II: GL 57mm, GW 6mm, Total 183mm
- DMA Tensile (ASTM D4065 / ISO 6721)

리본 진입점: `[[Commands/TensileTest/CreateASTMTensileSpecimenCommand.cs#CreateASTMTensileSpecimenCommand]]`. 다이얼로그: `[[UI/Dialogs/TensileSpecimenDialog.cs]]`. 모델링 로직: `[[Services/TensileTest/SpecimenModelingService.cs#SpecimenModelingService]]`. 메타데이터 YAML 출력: `[[Services/TensileTest/SpecimenMetadataService.cs#SpecimenMetadataService]]` ([[pipeline#Specimen YAML]] 참조).

## DMA 3점 굽힘 시편

ASTM D790 / ISO 178 표준. 직사각형 바 + 하부 지지점 2개 + 상부 로딩 노즈 1개의 지지 구조를 함께 모델링한다. 스팬/두께 비율 16:1 기본.

리본 진입점: `[[Commands/DMA/CreateDMA3PointBendingCommand.cs#CreateDMA3PointBendingCommand]]`. 다이얼로그: `[[UI/Dialogs/DMA3PointBendingDialog.cs]]`. 서비스: `[[Services/DMA/DMA3PointBendingService.cs#DMA3PointBendingService]]`.

## DMA 4점 굽힘 시편

ASTM C1161 / ASTM D6272 표준. 하부 외부 지지점 2개 + 상부 내부 로딩 노즈 2개. 외부/내부 스팬 비율 2:1 기본.

리본 진입점: `[[Commands/DMA/CreateDMA4PointBendingCommand.cs#CreateDMA4PointBendingCommand]]`. 다이얼로그: `[[UI/Dialogs/DMA4PointBendingDialog.cs]]`. 서비스: `[[Services/DMA/DMA4PointBendingService.cs#DMA4PointBendingService]]`.

## Bending Fixture 적용

기존 시편 모델에 지지 구조 (지지점 + 로딩 노즈) 만 따로 적용한다. 시편을 외부에서 받아왔거나, 다른 명령으로 만든 후에 지그만 추가하고 싶을 때 사용.

리본 진입점: `[[Commands/BendingFixture/ApplyBendingFixtureCommand.cs#ApplyBendingFixtureCommand]]`. 다이얼로그: `[[UI/Dialogs/ApplyBendingFixtureDialog.cs]]`. 서비스: `[[Services/BendingFixture/BendingFixtureService.cs#BendingFixtureService]]`.

## 압축 시편

ASTM D3410 / D695 호환. 게이지 길이가 짧고 단면이 사각인 압축 전용 형상.

리본 진입점: `[[Commands/Compression/CreateCompressionSpecimenCommand.cs#CreateCompressionSpecimenCommand]]`. 다이얼로그: `[[UI/Dialogs/CompressionSpecimenDialog.cs]]`. 서비스: `[[Services/Compression/CompressionSpecimenService.cs#CompressionSpecimenService]]`.

## CAI 시편

Compression After Impact 시편. ASTM D7136/D7137 호환. 직사각형 패널에 충격 위치 마킹 가능.

리본 진입점: `[[Commands/CAI/CreateCAISpecimenCommand.cs#CreateCAISpecimenCommand]]`. 다이얼로그: `[[UI/Dialogs/CAISpecimenDialog.cs]]`. 서비스: `[[Services/CAI/CAISpecimenService.cs#CAISpecimenService]]`.

## 피로 시편

ASTM E466 / ISO 1099 호환. Notched / Unnotched 변형 가능. 노치 반경, 응력집중계수 자동 산출.

리본 진입점: `[[Commands/Fatigue/CreateFatigueSpecimenCommand.cs#CreateFatigueSpecimenCommand]]`. 다이얼로그: `[[UI/Dialogs/FatigueSpecimenDialog.cs]]`. 서비스: `[[Services/Fatigue/FatigueSpecimenService.cs#FatigueSpecimenService]]`.

## Joint 시편

복합재 접합부 시편 (Single Lap Shear, Double Lap Shear 등). ASTM D5868 / D3163. 접합 길이와 어댄드 두께를 파라미터화.

리본 진입점: `[[Commands/Joint/CreateJointSpecimenCommand.cs#CreateJointSpecimenCommand]]`. 다이얼로그: `[[UI/Dialogs/JointSpecimenDialog.cs]]`. 서비스: `[[Services/Joint/JointSpecimenService.cs#JointSpecimenService]]`.

## Laminate

복합재 적층판 시편. Surface (쉘) 또는 Solid (3D body, 라미나당 분리 바디) 모드 선택. 라미나 두께, 섬유 각도, 적층 순서 입력. Solid 모드는 라미나 간 본드 면을 자동 검출해 Share Topology 준비까지.

리본 진입점: `[[Commands/Laminate/CreateLaminateCommand.cs#CreateLaminateCommand]]`. 다이얼로그: `[[UI/Dialogs/LaminateDialog.cs]]`. 서비스: Solid 모드 `[[Services/Laminate/SolidLaminateService.cs#SolidLaminateService]]`, Surface 모드 `[[Services/Laminate/SurfaceLaminateService.cs#SurfaceLaminateService]]`.
