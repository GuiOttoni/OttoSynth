@echo off
REM Locates vcvars64.bat using vswhere (which ships with VS Installer).
REM Echoes the full path (no trailing newline) for MSBuild to capture.

setlocal enabledelayedexpansion

set "VSWHERE=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"
if not exist "%VSWHERE%" set "VSWHERE=%ProgramFiles%\Microsoft Visual Studio\Installer\vswhere.exe"

if not exist "%VSWHERE%" (
    echo F:\VStudio\VC\Auxiliary\Build\vcvars64.bat
    exit /b 0
)

for /f "usebackq tokens=*" %%i in (`"%VSWHERE%" -latest -property installationPath`) do (
    set "VSPATH=%%i"
)

if not defined VSPATH (
    echo F:\VStudio\VC\Auxiliary\Build\vcvars64.bat
    exit /b 0
)

set "VCVARS=%VSPATH%\VC\Auxiliary\Build\vcvars64.bat"
echo %VCVARS%
exit /b 0
