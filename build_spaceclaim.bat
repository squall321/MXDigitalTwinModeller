@echo off
cd /d "%~dp0"
"C:\Program Files (x86)\Microsoft Visual Studio\2019\BuildTools\MSBuild\Current\Bin\MSBuild.exe" MXDigitalTwinModeller.csproj /p:Configuration=Debug /p:Platform=AnyCPU
