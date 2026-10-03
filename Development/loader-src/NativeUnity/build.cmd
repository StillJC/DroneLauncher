@echo off
setlocal
set "DRG_OUT=%~1"
if not defined DRG_OUT set "DRG_OUT=%~dp0..\..\..\artifacts\Runtime\Launcher\Plugins\Unity"
set "DRG_VS="
for /f "usebackq tokens=*" %%i in (`"%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe" -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath`) do set "DRG_VS=%%i"
if not defined DRG_VS (echo Visual Studio C++ x86/x64 tools are required. 1>&2 & exit /b 1)
call "%DRG_VS%\VC\Auxiliary\Build\vcvars64.bat" >nul
if errorlevel 1 exit /b 1
if not exist "%DRG_OUT%" mkdir "%DRG_OUT%"
if errorlevel 1 exit /b 1
pushd "%DRG_OUT%"
cl /nologo /W4 /O2 /MT /EHsc /std:c++17 /LD "%~dp0unity.cpp" /link /Brepro /OUT:"DroneRacingGenesis.Unity.dll"
if errorlevel 1 (popd & exit /b 1)
cl /nologo /W4 /O2 /MT /EHsc /std:c++17 "%~dp0injector.cpp" /link /Brepro /OUT:"DroneRacingGenesis.UnityBootstrap.exe"
if errorlevel 1 (popd & exit /b 1)
popd
exit /b 0
