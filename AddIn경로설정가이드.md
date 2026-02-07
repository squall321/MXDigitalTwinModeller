# AddIn 출력 경로 설정 가이드

## SpaceClaim AddIn 로드 경로

SpaceClaim은 시작 시 다음 경로에서 AddIn을 자동으로 찾아서 로드합니다:

### 1. 시스템 전체 경로 (모든 사용자) - **권장**
```
C:\ProgramData\SpaceClaim\AddIns\
```
- **장점**: 모든 사용자가 사용 가능
- **권장 사용**: 개발 및 배포 모두

### 2. 현재 사용자 경로
```
%APPDATA%\SpaceClaim\AddIns\
= C:\Users\[사용자명]\AppData\Roaming\SpaceClaim\AddIns\
```
- **장점**: 사용자별 설치 가능
- **단점**: 다른 사용자는 사용 불가

### 3. SpaceClaim 설치 폴더 (비권장)
```
[SpaceClaim 설치 경로]\AddIns\
예: D:\Program Files\ANSYS Inc\ANSYS Student\v252\scdm\AddIns\
```
- **단점**: 업데이트 시 삭제될 수 있음
- **비권장**: 개발 시 사용하지 않는 것이 좋음

---

## 현재 프로젝트 설정

### .env 파일 설정
```ini
# AddIn 출력 경로
ADDIN_OUTPUT_PATH_V251=C:\ProgramData\SpaceClaim\AddIns\MXDigitalTwinModeller\V251
ADDIN_OUTPUT_PATH_V252=C:\ProgramData\SpaceClaim\AddIns\MXDigitalTwinModeller\V252
```

### 경로 구조
```
C:\ProgramData\SpaceClaim\AddIns\
└─ MXDigitalTwinModeller\
   ├─ V251\
   │  ├─ MXDigitalTwinModeller.dll
   │  ├─ MXDigitalTwinModeller.pdb
   │  └─ MXDigitalTwinModeller.Manifest.xml
   └─ V252\
      ├─ MXDigitalTwinModeller.dll
      ├─ MXDigitalTwinModeller.pdb
      └─ MXDigitalTwinModeller.Manifest.xml
```

### ✅ 이 설정이 올바른 이유

1. **시스템 전체 경로 사용**
   - `C:\ProgramData\SpaceClaim\AddIns\`는 표준 경로입니다
   - SpaceClaim이 자동으로 검색합니다

2. **버전별 폴더 분리**
   - `V251`, `V252` 폴더로 구분
   - 각 버전의 SpaceClaim이 해당 폴더의 AddIn을 로드합니다

3. **자동 복사**
   - 빌드 시 자동으로 해당 경로에 복사됩니다
   - `.csproj`의 `OutputPath` 설정

---

## 빌드 시 동작 방식

### Debug-V252 빌드 시
```
1. 컴파일
   프로젝트 폴더\bin\... (임시)

2. 출력
   ↓
   C:\ProgramData\SpaceClaim\AddIns\MXDigitalTwinModeller\V252\
   - MXDigitalTwinModeller.dll
   - MXDigitalTwinModeller.pdb
   - MXDigitalTwinModeller.Manifest.xml

3. SpaceClaim V252 실행 시
   자동으로 위 폴더에서 AddIn 로드
```

---

## 경로 변경이 필요한 경우

### 1. 현재 사용자 전용으로 설치하려면

`.env` 파일 수정:
```ini
ADDIN_OUTPUT_PATH_V251=%APPDATA%\SpaceClaim\AddIns\MXDigitalTwinModeller\V251
ADDIN_OUTPUT_PATH_V252=%APPDATA%\SpaceClaim\AddIns\MXDigitalTwinModeller\V252
```

실제 경로:
```
C:\Users\[사용자명]\AppData\Roaming\SpaceClaim\AddIns\MXDigitalTwinModeller\V251
C:\Users\[사용자명]\AppData\Roaming\SpaceClaim\AddIns\MXDigitalTwinModeller\V252
```

### 2. SpaceClaim 설치 폴더에 설치하려면 (비권장)

`.env` 파일 수정:
```ini
ADDIN_OUTPUT_PATH_V251=D:\Program Files\ANSYS Inc\ANSYS Student\v251\scdm\AddIns\MXDigitalTwinModeller
ADDIN_OUTPUT_PATH_V252=D:\Program Files\ANSYS Inc\ANSYS Student\v252\scdm\AddIns\MXDigitalTwinModeller
```

**주의**:
- 관리자 권한 필요
- SpaceClaim 업데이트 시 삭제될 수 있음

---

## 수동 설치 방법

빌드 없이 수동으로 설치하려면:

### 1. 빌드된 파일 확인
```
프로젝트 폴더\bin\Debug-V252\ (또는 Release-V252\)
├─ MXDigitalTwinModeller.dll
├─ MXDigitalTwinModeller.pdb
└─ MXDigitalTwinModeller.Manifest.xml
```

### 2. 복사
```
C:\ProgramData\SpaceClaim\AddIns\MXDigitalTwinModeller\V252\
```
폴더에 위 파일들을 복사

### 3. SpaceClaim 재시작
- SpaceClaim 종료 후 재시작
- AddIn이 자동으로 로드됩니다

---

## AddIn 로드 확인 방법

### SpaceClaim에서 확인

1. **SpaceClaim 실행**

2. **Ribbon 확인**
   - "MX Modeller" 탭이 보이면 로드 성공

3. **AddIn 관리자 확인** (선택사항)
   - 파일 > 옵션 > AddIn (버전에 따라 다를 수 있음)
   - "MX Digital Twin Modeller" 항목 확인

### 로그 확인 (문제 발생 시)

SpaceClaim 로그 파일 위치:
```
%APPDATA%\SpaceClaim\Logs\
```

에러 메시지 확인

---

## 권한 문제 해결

### "액세스 거부" 오류 발생 시

**원인**: `C:\ProgramData\` 폴더에 쓰기 권한 부족

**해결 방법**:

#### 방법 1: Visual Studio를 관리자 권한으로 실행 (권장)
1. Visual Studio 2022 우클릭
2. "관리자 권한으로 실행"
3. 빌드

#### 방법 2: 폴더 생성 및 권한 설정
```batch
# 관리자 권한 명령 프롬프트에서 실행
mkdir "C:\ProgramData\SpaceClaim\AddIns\MXDigitalTwinModeller\V251"
mkdir "C:\ProgramData\SpaceClaim\AddIns\MXDigitalTwinModeller\V252"
icacls "C:\ProgramData\SpaceClaim\AddIns\MXDigitalTwinModeller" /grant Users:(OI)(CI)F /T
```

#### 방법 3: 출력 경로를 사용자 폴더로 변경
`.env` 파일에서:
```ini
ADDIN_OUTPUT_PATH_V251=%APPDATA%\SpaceClaim\AddIns\MXDigitalTwinModeller\V251
ADDIN_OUTPUT_PATH_V252=%APPDATA%\SpaceClaim\AddIns\MXDigitalTwinModeller\V252
```

---

## 배포 시 고려사항

### 개발 중
- 현재 설정 (`C:\ProgramData\...`) 그대로 사용
- 자동 빌드 및 복사로 편리

### 최종 사용자 배포
1. **설치 프로그램 제작** (권장)
   - WiX, Inno Setup 등 사용
   - `C:\ProgramData\SpaceClaim\AddIns\` 에 설치

2. **수동 배포**
   - ZIP 파일로 패키징
   - 사용자가 직접 `C:\ProgramData\SpaceClaim\AddIns\` 에 압축 해제

3. **설치 스크립트**
   ```batch
   @echo off
   set TARGET=C:\ProgramData\SpaceClaim\AddIns\MXDigitalTwinModeller\V252
   mkdir "%TARGET%"
   copy MXDigitalTwinModeller.dll "%TARGET%\"
   copy MXDigitalTwinModeller.Manifest.xml "%TARGET%\"
   echo 설치 완료!
   pause
   ```

---

## 요약

### ✅ 현재 설정 (권장)
```ini
ADDIN_OUTPUT_PATH_V251=C:\ProgramData\SpaceClaim\AddIns\MXDigitalTwinModeller\V251
ADDIN_OUTPUT_PATH_V252=C:\ProgramData\SpaceClaim\AddIns\MXDigitalTwinModeller\V252
```

### 이 설정의 장점
1. ✅ 표준 경로 사용
2. ✅ 모든 사용자가 사용 가능
3. ✅ SpaceClaim이 자동으로 찾음
4. ✅ 버전별 분리로 충돌 없음
5. ✅ 빌드 시 자동 복사

### 필요한 작업
- **없음!** 현재 설정이 이미 최적입니다.
- 빌드 시 자동으로 올바른 위치에 복사됩니다.

### 만약 권한 문제가 발생하면
- Visual Studio를 **관리자 권한으로 실행**하세요.
