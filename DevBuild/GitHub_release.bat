@echo off

if not exist "..\bin\Release\PinLock.exe" (
    echo Build not found. Run Build_release.bat first.
    pause
    exit /b 1
)

if exist "PinLock_Release" rmdir /s /q "PinLock_Release" >nul 2>&1
mkdir "PinLock_Release"

echo Packaging release files...
copy "..\bin\Release\PinLock.exe" "PinLock_Release\"
copy "..\bin\Release\*.dll" "PinLock_Release\" >nul 2>&1
copy "..\bin\Release\*.nlp" "PinLock_Release\" >nul 2>&1
copy "..\README.md" "PinLock_Release\" >nul 2>&1
copy "..\LICENSE" "PinLock_Release\" >nul 2>&1

echo Creating ZIP file...
powershell -Command "Compress-Archive -Path 'PinLock_Release\*' -DestinationPath 'PinLock-v1.2.0.zip' -Force"

echo Release package ready: PinLock-v1.2.0.zip
pause