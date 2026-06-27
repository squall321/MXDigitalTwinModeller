이 디렉토리는 **MX Digital Twin Modeller** 프로젝트의 도메인 개념, 설계 결정, 기능 명세를 [lat.md](https://www.npmjs.com/package/lat.md) 형식의 지식 그래프로 정리한 곳이다. SpaceClaim Add-In 과 Mechanical ACT Extension 두 사이드의 모든 기능과 그 출처가 되는 소스 파일을 연결한다.

이 프로젝트는 **두 개의 독립적인 배포 단위**로 구성된다:

- **SpaceClaim Add-In** (C# .NET 4.7.2): 시편 모델링, 메쉬, 접촉 검출, Conformal Mesh 등 — `MX Modeller` 리본 탭으로 제공
- **Mechanical ACT Extension** (IronPython + WPF): STEP 임포트, Face Pair NS, Tied Contact Check, Material Calibrator, Modal/Scenario, Post-process — `MXSimulator` 탭으로 제공

두 사이드는 공용 DLL `MXDigitalTwinModeller.Core.dll` (SpatialIndex, BodyBounds, GeometryUtils) 을 공유한다.

## 인덱스

### 전체

- [[architecture]] — 듀얼 프로젝트 구조, 공용 Core DLL, 멀티버전 (V251/V252) 지원 + **CAD-Modification 커널 지뢰(Boolean poison/OffsetFaces 대체)·곡면 배치·SC cold-launch 운영**
- [[api-learnings]] — ANSYS ACT/SpaceClaim API 함정 노트 (검증 완료) + **Mod 검증: kernel-truth fingerprint·PIN 오분류 발견·IronPython 바인딩 함정(CircleProfile/Unite/DesignBody/Cylinder.Radius)**
- [[build-deploy]] — MSBuild, 빌드 구성, ACT Extension 자동 배포, WiX 인스톨러

### SpaceClaim Add-In

- [[spaceclaim-addin]] — Add-In 개요 + 리본 구조
- [[specimens]] — 시편 모델링 (인장/굽힘/CAI/피로/Joint/Laminate)
- [[mesh]] — 메쉬/접촉/Simplify/Material/Load/Export/Conformal/Gmsh/VoidCut
- [[pipeline]] — BatchPipeline + IronPython 16개 스크립트

### Mechanical ACT Extension (MXSimulator)

- [[mechanical-act]] — Extension 개요 + 탭 구조
- [[face-analysis]] — STEP 임포트 + 면 법선 분석 + 방향별 Named Selection
- [[scenarios]] — Modal Analysis + Cap Vibration Scenario
- [[face-pair-ns]] — Face Pair Named Selection (merge mode)
- [[tied-check]] — Tied Contact Check + RBM 검출
- [[material-calibrator]] — Elastic/Plastic 재료 calibration (PyInstaller EXE)
- [[postprocess]] — 결과 분석 + KFile 내보내기
