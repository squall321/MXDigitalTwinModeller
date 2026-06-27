@echo off
REM ANSYS 프로세스 강제 종료 스크립트
echo ========================================
echo ANSYS 프로세스 강제 종료
echo ========================================
echo.

echo Workbench 프로세스 종료...
taskkill /F /IM AnsWBU.exe 2>nul
taskkill /F /IM AnsysMechanical.exe 2>nul
taskkill /F /IM DSApplet.exe 2>nul

echo Framework 프로세스 종료...
taskkill /F /IM AnsysFWW.exe 2>nul
taskkill /F /IM ansyscl.exe 2>nul

echo SpaceClaim 프로세스 종료...
taskkill /F /IM SpaceClaim.exe 2>nul

echo.
echo 모든 ANSYS 프로세스가 종료되었습니다.
echo 이제 ANSYS Workbench를 재시작하세요.
echo.
pause
