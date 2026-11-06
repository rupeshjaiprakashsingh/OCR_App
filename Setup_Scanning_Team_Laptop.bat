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
where node >nul 2>nul
if %ERRORLEVEL% NEQ 0 (
    if exist "C:\Program Files\nodejs\node.exe" (
        set "PATH=C:\Program Files\nodejs;%PATH%"
    )
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
echo [2/5] Installing local PDF processing dependencies...
call npm install --omit=dev --no-audit --no-fund
if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] npm install failed. Please check your internet connection and try again.
    pause
    exit /b 1
)

REM ----------------------------------------------------------------------------
REM STEP 3: Compile Native Windows Multi-Threaded OCR & Blank/Crop Engine
REM ----------------------------------------------------------------------------
echo [3/5] Building native Windows Multi-Threaded OCR ^& Blank/Crop Engine...
if not exist "bin" mkdir "bin"
"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /optimize+ ^
    /out:"bin\WinOcrEngine.exe" ^
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
    "src\native\WinOcrEngine.cs"

if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] Failed to compile WinOcrEngine.exe. Ensure Windows 10/11 is up to date.
    pause
    exit /b 1
)

REM ----------------------------------------------------------------------------
REM STEP 4: Create Scanner Input & Searchable Output Folders + Desktop Shortcuts
REM ----------------------------------------------------------------------------
echo [4/5] Creating Input/Output folders and Desktop Shortcuts...
if not exist "inputScanningFiles" mkdir "inputScanningFiles"
if not exist "outputSearchableFiles" mkdir "outputSearchableFiles"

powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "$ws = New-Object -ComObject WScript.Shell; " ^
  "$desk = [Environment]::GetFolderPath('Desktop'); " ^
  "$s1 = $ws.CreateShortcut($desk + '\Book Scanner QC & OCR.lnk'); " ^
  "$s1.TargetPath = '%~dp0Start_24x7_OCR_Tool.bat'; " ^
  "$s1.WorkingDirectory = '%~dp0'; " ^
  "$s1.IconLocation = 'shell32.dll,23'; " ^
  "$s1.Description = 'Launch 24/7 Book Scanner QC, OCR & Auto-Renamer'; " ^
  "$s1.Save(); " ^
  "$s2 = $ws.CreateShortcut($desk + '\1. Scanner Input Folder.lnk'); " ^
  "$s2.TargetPath = '%~dp0inputScanningFiles'; " ^
  "$s2.Save(); " ^
  "$s3 = $ws.CreateShortcut($desk + '\2. Searchable PDF Output Folder.lnk'); " ^
  "$s3.TargetPath = '%~dp0outputSearchableFiles'; " ^
  "$s3.Save();"

REM ----------------------------------------------------------------------------
REM STEP 5: Launch the 24/7 Application!
REM ----------------------------------------------------------------------------
echo [5/5] Installation Complete!
echo.
echo ============================================================================
echo   INSTALLED SUCCESSFULLY ON THIS LAPTOP!
echo   3 Easy Shortcuts have been added to your Desktop:
echo     1. "Book Scanner QC & OCR"           (Starts 24/7 App ^& Web Dashboard)
echo     2. "1. Scanner Input Folder"         (Drop scanned PDFs here)
echo     3. "2. Searchable PDF Output Folder" (Auto-renamed Searchable PDFs)
echo ============================================================================
echo.
echo Starting the 24/7 Book Scanner QC ^& OCR Suite now...
timeout /t 2 /nobreak >nul
start "" "%~dp0Start_24x7_OCR_Tool.bat"
exit /b 0
