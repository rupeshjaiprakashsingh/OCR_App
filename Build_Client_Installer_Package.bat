@echo off
title Build Secure Client Installer Package (Strips Admin Tools & Source)
color 0B
cd /d "%~dp0"

echo ===============================================================================
echo   BUILDING SECURE CLIENT INSTALLER PACKAGE (FOR CUSTOMER / CLIENT LAPTOPS)
echo ===============================================================================
echo.

REM 1. Ensure native binary is compiled
node -e "const { ensureWinOcrBinary } = require('./src/ensureNativeBinary'); console.log('Native Engine Ready:', ensureWinOcrBinary());"

set "DIST_DIR=%~dp0Client_Distribution_Package"
set "ZIP_FILE=%~dp0Client_Installer_Package.zip"

if exist "%DIST_DIR%" rmdir /s /q "%DIST_DIR%"
if exist "%ZIP_FILE%" del /f /q "%ZIP_FILE%"

mkdir "%DIST_DIR%"
mkdir "%DIST_DIR%\bin"
mkdir "%DIST_DIR%\src"
mkdir "%DIST_DIR%\public"
mkdir "%DIST_DIR%\inputScanningFiles"
mkdir "%DIST_DIR%\outputSearchableFiles"

echo [1/4] Copying pre-compiled native OCR engine (hiding C# source code)...
copy /y "bin\WinOcrEngine.exe" "%DIST_DIR%\bin\" >nul

echo [2/4] Copying application runtime & UI (excluding Admin KeyGen & local license)...
copy /y "src\ensureNativeBinary.js" "%DIST_DIR%\src\" >nul
copy /y "src\extractor.js" "%DIST_DIR%\src\" >nul
copy /y "src\licenseManager.js" "%DIST_DIR%\src\" >nul
copy /y "src\pdfBuilder.js" "%DIST_DIR%\src\" >nul
copy /y "src\processor.js" "%DIST_DIR%\src\" >nul
copy /y "src\server.js" "%DIST_DIR%\src\" >nul
copy /y "src\cli.js" "%DIST_DIR%\src\" >nul
copy /y "public\index.html" "%DIST_DIR%\public\" >nul
copy /y "package.json" "%DIST_DIR%\" >nul
copy /y "package-lock.json" "%DIST_DIR%\" >nul

echo [3/4] Copying 1-Click Client Setup & 24/7 Auto-Start launchers...
copy /y "Setup_Scanning_Team_Laptop.bat" "%DIST_DIR%\" >nul
copy /y "Start_24x7_OCR_Tool.bat" "%DIST_DIR%\" >nul
copy /y "Enable_24x7_Windows_AutoStart.bat" "%DIST_DIR%\" >nul
copy /y "Disable_24x7_Windows_AutoStart.bat" "%DIST_DIR%\" >nul
copy /y "Start_Silent_Background.vbs" "%DIST_DIR%\" >nul

echo [4/4] Creating portable ZIP archive: Client_Installer_Package.zip ...
powershell -NoProfile -Command "Compress-Archive -Path '%DIST_DIR%\*' -DestinationPath '%ZIP_FILE%' -Force"

echo.
echo ===============================================================================
echo   DONE! SECURE CLIENT PACKAGE READY:
echo   1. Folder : %DIST_DIR%
echo   2. ZIP    : %ZIP_FILE%
echo.
echo   HOW TO INSTALL ON CLIENT LAPTOP:
echo   - Copy Client_Installer_Package.zip to the client laptop and extract it.
echo   - Double-click Setup_Scanning_Team_Laptop.bat
echo   - The browser opens to the Hardware Lock Screen showing their Machine ID.
echo   - Unlock using your Admin Password (Rupesh@2026) OR generate a key using
echo     Admin_KeyGenerator.bat on your laptop!
echo ===============================================================================
echo.
pause
