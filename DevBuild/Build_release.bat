@echo off

REM Clean previous builds
if exist "..\bin\Release" rmdir /s /q "..\bin\Release" >nul 2>&1
if exist "..\obj" rmdir /s /q "..\obj" >nul 2>&1

REM Find MSBuild
set MSBUILD_PATH=""

if exist "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" (
    set MSBUILD_PATH="C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe"
    goto :build
)
if exist "C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe" (
    set MSBUILD_PATH="C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe"
    goto :build
)
if exist "C:\Program Files (x86)\Microsoft Visual Studio\2019\Community\MSBuild\Current\Bin\MSBuild.exe" (
    set MSBUILD_PATH="C:\Program Files (x86)\Microsoft Visual Studio\2019\Community\MSBuild\Current\Bin\MSBuild.exe"
    goto :build
)
if exist "C:\Program Files (x86)\Microsoft Visual Studio\2019\BuildTools\MSBuild\Current\Bin\MSBuild.exe" (
    set MSBUILD_PATH="C:\Program Files (x86)\Microsoft Visual Studio\2019\BuildTools\MSBuild\Current\Bin\MSBuild.exe"
    goto :build
)
if exist "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe" (
    set MSBUILD_PATH="C:\Windows\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe"
    goto :build
)

echo MSBuild not found
pause
exit /b 1

:build
echo Building PinLock Release...
%MSBUILD_PATH% ..\PinLock.csproj /p:Configuration=Release /p:Platform="Any CPU" /verbosity:minimal /nologo

if %ERRORLEVEL% neq 0 (
    echo Build failed
    pause
    exit /b 1
)

echo Build complete: ..\bin\Release\PinLock.exe
pause