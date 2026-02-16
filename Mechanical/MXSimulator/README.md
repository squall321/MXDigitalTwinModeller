# MXSimulator - ANSYS Mechanical ACT Extension

ANSYS Mechanical에 MX Digital Twin Simulator 기능을 추가하는 ACT Extension입니다.

## 구조

```
MXSimulator/
├── extension.xml          # ACT 확장 정의 (탭, 패널, 버튼, 콜백)
├── main.py                # IronPython 로직 (WPF 대화상자, 콜백)
├── images/
│   └── cap_vibration.png  # 리본 아이콘
└── README.md              # 이 파일
```

## 기능

### MXSimulator 탭
- **Load 패널**
  - **Cap Vibration**: Cap 진동 하중 정의 대화상자

## 설치

### 수동 설치
1. 이 폴더(`MXSimulator/`)를 통째로 복사:
   ```
   %APPDATA%\ANSYS\v252\ACT\extensions\MXSimulator\
   ```

2. ANSYS Mechanical 재시작

3. 리본에서 `MXSimulator` 탭 확인

### MSI 인스톨러 (향후 계획)
`MXDigitalTwinModeller.msi` 설치 시 SpaceClaim Add-In + Mechanical ACT Extension 동시 배포

## 사용법

1. Mechanical에서 모델 열기
2. `MXSimulator` 탭 → `Load` 패널 → `Cap Vibration` 클릭
3. 대화상자에서 진동 파라미터 입력:
   - Frequency (Hz)
   - Amplitude (mm)
   - Duration (s)
   - Target Named Selection
4. `Apply` 클릭

## 개발 노트

### WPF UI
- `System.Windows.Controls` 기반 WPF 대화상자
- WinForms 대신 WPF 사용 이유: Mechanical ACT는 WPF를 권장

### 향후 확장
- Shared DLL (`MXDigitalTwinModeller.Core.dll`) 연동
  - `SpatialIndex`, `KFilePostProcessor` 재사용
  - `clr.AddReference("MXDigitalTwinModeller.Core")` 로드
- Mechanical DataModel API 연동
  - `ExtAPI.DataModel.Project.Model.AddLoad(...)`
  - Named Selection 자동 생성
  - 하중 커브 자동 설정

## API 참조

- [ANSYS ACT Developer Guide](https://ansyshelp.ansys.com)
- `Ansys.ACT.Interfaces.Mechanical`
- `Ansys.ACT.Automation.Mechanical`
