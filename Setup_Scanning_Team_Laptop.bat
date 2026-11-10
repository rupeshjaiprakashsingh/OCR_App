@echo off
setlocal EnableDelayedExpansion
title 1-Click Installer - Bulk Book Scanner QC, OCR ^& Auto-Renamer Suite
cd /d "%~dp0"

echo ============================================================================
echo   1-Click Client Laptop Installer
echo   Bulk Book Scanner QC, Searchable PDF OCR ^& Auto-Renamer Suite
echo ============================================================================
echo.

REM ----------------------------------------------------------------------------
REM STEP 1: Check & Auto-Install Node.js if missing
REM ----------------------------------------------------------------------------
if exist "C:\Program Files\nodejs\node.exe" (
    set "PATH=C:\Program Files\nodejs;%PATH%"
)

where node >nul 2>nul
if %ERRORLEVEL% NEQ 0 (
    echo [1/5] Node.js not found on this laptop. Installing automatically via Windows winget...
    winget install --id OpenJS.NodeJS.LTS -e --accept-package-agreements --accept-source-agreements
    if exist "C:\Program Files\nodejs\node.exe" (
        set "PATH=C:\Program Files\nodejs;%PATH%"
    )
)

where node >nul 2>nul
if %ERRORLEVEL% NEQ 0 (
    echo.
    echo [ERROR] Node.js installation requires restarting this window or installing from https://nodejs.org
    pause
    exit /b 1
)
for /f "tokens=*" %%i in ('node -v') do set NODE_VER=%%i
echo [1/5] Node.js Ready (%NODE_VER%).

REM ----------------------------------------------------------------------------
REM STEP 2: Install NPM Dependencies
REM ----------------------------------------------------------------------------
cd /d "%~dp0"
echo [2/5] Installing local PDF processing dependencies...
call npm install --omit=dev --no-audit --no-fund
if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] npm install failed. Please check your internet connection and try again.
    pause
    exit /b 1
)

REM ----------------------------------------------------------------------------
REM STEP 3: Verify or Build Native Windows Multi-Threaded OCR Engine
REM ----------------------------------------------------------------------------
cd /d "%~dp0"
echo [3/5] Checking native Windows Multi-Threaded OCR ^& Blank/Crop Engine...
if exist "%~dp0bin\WinOcrEngine.exe" (
    echo [3/5] Pre-compiled Native Turbo OCR Engine Ready!
) else (
    if not exist "%~dp0bin" mkdir "%~dp0bin"
    if exist "%~dp0src\native\WinOcrEngine.cs" (
        "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /optimize+ ^
            /out:"%~dp0bin\WinOcrEngine.exe" ^
            /r:"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.Runtime.WindowsRuntime.dll" ^
            /r:"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.Runtime.dll" ^
            /r:"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.Threading.Tasks.dll" ^
            /r:"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.Runtime.InteropServices.WindowsRuntime.dll" ^
            /r:"C:\Windows\System32\WinMetadata\Windows.Foundation.winmd" ^
            /r:"C:\Windows\System32\WinMetadata\Windows.Storage.winmd" ^
            /r:"C:\Windows\System32\WinMetadata\Windows.Data.winmd" ^
            /r:"C:\Windows\System32\WinMetadata\Windows.Graphics.winmd" ^
            /r:"C:\Windows\System32\WinMetadata\Windows.Media.winmd" ^
            /r:"C:\Windows\System32\WinMetadata\Windows.Globalization.winmd" ^
            "%~dp0src\native\WinOcrEngine.cs"
    ) else (
        echo [ERROR] Neither bin\WinOcrEngine.exe nor src\native\WinOcrEngine.cs was found!
        echo Please make sure you extracted the entire ZIP folder before running setup.
        pause
        exit /b 1
    )
)

REM ----------------------------------------------------------------------------
REM STEP 4: Create Scanner Input & Searchable Output Folders + Desktop Shortcuts
REM ----------------------------------------------------------------------------
cd /d "%~dp0"
echo [4/5] Creating Input/Output folders and Desktop Shortcuts...
if not exist "%~dp0inputScanningFiles" mkdir "%~dp0inputScanningFiles"
if not exist "%~dp0outputSearchableFiles" mkdir "%~dp0outputSearchableFiles"

powershell -NoProfile -ExecutionPolicy Bypass -Command "$ws = New-Object -ComObject WScript.Shell; $desk = [Environment]::GetFolderPath('Desktop'); $appDir = '%~dp0'.TrimEnd('\'); $s1 = $ws.CreateShortcut((Join-Path $desk '3_Open_24x7_OCR_Dashboard.lnk')); $s1.TargetPath = (Join-Path $appDir 'Start_24x7_OCR_Tool.bat'); $s1.WorkingDirectory = $appDir; $s1.Save(); $s2 = $ws.CreateShortcut((Join-Path $desk '1_Drop_Scanned_PDFs_Here.lnk')); $s2.TargetPath = (Join-Path $appDir 'inputScanningFiles'); $s2.Save(); $s3 = $ws.CreateShortcut((Join-Path $desk '2_Completed_Searchable_Books.lnk')); $s3.TargetPath = (Join-Path $appDir 'outputSearchableFiles'); $s3.Save();"

REM ----------------------------------------------------------------------------
REM STEP 5: Launch the 24/7 Application!
REM ----------------------------------------------------------------------------
echo [5/5] Installation Complete!
echo.
echo ============================================================================
echo   INSTALLED SUCCESSFULLY ON THIS LAPTOP!
echo   3 Easy Shortcuts have been added to your Desktop:
echo     1. "1_Drop_Scanned_PDFs_Here"       (Drop scanned PDFs here)
echo     2. "2_Completed_Searchable_Books"   (Auto-renamed Searchable PDFs)
echo     3. "3_Open_24x7_OCR_Dashboard"      (Starts 24/7 App ^& Web Dashboard)
echo ============================================================================
echo.
echo Starting the 24/7 Book Scanner QC ^& OCR Suite now...
timeout /t 2 /nobreak >nul
start "" "%~dp0Start_24x7_OCR_Tool.bat"
exit /b 0
