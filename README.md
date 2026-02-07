# MX Digital Twin Modeller

ASTM 규격 인장시험 시편 자동 모델링 SpaceClaim AddIn

**멀티버전 지원**: SpaceClaim V251 및 V252 모두 지원

## 기능

- ASTM E8 Standard / SubSize 시편 생성 (금속)
- ASTM D638 Type I / Type II 시편 생성 (플라스틱)
- 직관적인 대화창을 통한 파라미터 입력
- 규격별 기본값 자동 로드
- SpaceClaim V251 및 V252 멀티버전 지원

## 설치 방법

### 1. 환경 설정

`.env.example` 파일을 `.env`로 복사하고 실제 환경에 맞게 경로를 수정하세요.

```ini
# SpaceClaim V251 경로
SPACECLAIM_V251_INSTALL_PATH=C:\Program Files\ANSYS Inc\v251\SpaceClaim
SPACECLAIM_V251_API_PATH=C:\Program Files\ANSYS Inc\v251\SpaceClaim\Api\V251\bin\x64\Debug
SPACECLAIM_V251_EXE=C:\Program Files\ANSYS Inc\v251\SpaceClaim\SpaceClaim.exe

# SpaceClaim V252 경로
SPACECLAIM_V252_INSTALL_PATH=C:\Program Files\ANSYS Inc\v252\SpaceClaim
SPACECLAIM_V252_API_PATH=C:\Program Files\ANSYS Inc\v252\SpaceClaim\Api\V252\bin\x64\Debug
SPACECLAIM_V252_EXE=C:\Program Files\ANSYS Inc\v252\SpaceClaim\SpaceClaim.exe

# AddIn 출력 경로
ADDIN_OUTPUT_PATH_V251=C:\ProgramData\SpaceClaim\AddIns\MXDigitalTwinModeller\V251
ADDIN_OUTPUT_PATH_V252=C:\ProgramData\SpaceClaim\AddIns\MXDigitalTwinModeller\V252
```

### 2. 빌드

Visual Studio 2022에서 솔루션을 열고 빌드 구성을 선택합니다.

#### 빌드 구성:
- **Debug-V251**: V251용 디버그 빌드 → `ADDIN_OUTPUT_PATH_V251`
- **Release-V251**: V251용 릴리즈 빌드 → `ADDIN_OUTPUT_PATH_V251`
- **Debug-V252**: V252용 디버그 빌드 → `ADDIN_OUTPUT_PATH_V252`
- **Release-V252**: V252용 릴리즈 빌드 → `ADDIN_OUTPUT_PATH_V252`

**기본 구성**: Debug-V252

### 3. SpaceClaim에서 확인

해당 버전의 SpaceClaim을 실행하고 Ribbon에서 "MX Modeller" 탭을 확인합니다.

## 개발 환경 설정

### Visual Studio 2022

1. .NET Framework 4.7.2 설치
2. 빌드 구성 선택 (Debug-V251 또는 Debug-V252)
3. 디버그 프로파일 선택:
   - **SpaceClaim V251 Debug**: V251에서 디버깅
   - **SpaceClaim V252 Debug**: V252에서 디버깅

`launchSettings.json`에 두 가지 프로파일이 설정되어 있습니다.

### 디버깅

1. 빌드 구성 선택 (예: Debug-V252)
2. 디버그 프로파일 선택 (예: SpaceClaim V252 Debug)
3. F5를 누르면 해당 버전의 SpaceClaim이 실행되고 AddIn이 로드됩니다
4. 중단점을 설정하여 디버깅할 수 있습니다

## 프로젝트 구조

```
MXDigitalTwinModeller/
├─ Core/                    # 공통 모듈
│  ├─ Geometry/            # 기하학 유틸리티
│  ├─ Commands/            # 커맨드 기본 클래스
│  └─ UI/                  # UI 헬퍼
├─ Commands/               # 커맨드 구현
│  └─ TensileTest/
├─ UI/Dialogs/            # 대화창
├─ Models/                 # 데이터 모델
│  └─ TensileTest/
├─ Services/              # 비즈니스 로직
│  └─ TensileTest/
└─ Resources/             # 리소스
```

## 사용 방법

1. SpaceClaim 실행
2. "MX Modeller" 탭 클릭
3. "Tensile Test" 그룹에서 "ASTM 인장시편" 버튼 클릭
4. 대화창에서 규격 선택 및 치수 입력
5. "생성" 버튼 클릭

## 지원 규격

| 규격 | 타입 | 게이지 길이 | 게이지 폭 |
|------|------|------------|----------|
| ASTM E8 | Standard | 50 mm | 12.5 mm |
| ASTM E8 | SubSize | 25 mm | 6 mm |
| ASTM D638 | Type I | 50 mm | 13 mm |
| ASTM D638 | Type II | 57 mm | 6 mm |

## 라이선스

Copyright © 2026 MX

## 버전

v1.0.0
