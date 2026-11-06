@echo off
title Setup - Bulk Book Scanner QC, OCR ^& Auto-Renamer Suite
cd /d "%~dp0"

echo ============================================================================
echo   Scanning Team Laptop Setup - Book Scanner QC, OCR ^& Auto-Renamer
echo ============================================================================
echo.

REM 1. Check Node.js
where node >nul 2>nul
if %ERRORLEVEL% NEQ 0 (
    echo [1/4] Node.js not found. Attempting automatic install via winget...
    winget install OpenJS.NodeJS.LTS --accept-package-agreements --accept-source-agreements
    echo Please close and re-run this script after Node.js installation finishes.
    pause
    exit /b 1
) else (
    echo [1/4] Node.js detected.
)

REM 2. Install npm packages if needed
if not exist "node_modules\pdf-lib" (
    echo [2/4] Installing PDF processing libraries...
    call npm install
) else (
    echo [2/4] PDF libraries ready.
)

REM 3. Compile native Windows OCR & Pixel QC Engine if needed
if not exist "bin" mkdir "bin"
echo [3/4] Compiling native Windows Multi-Threaded OCR ^& Blank/Crop Engine...
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

if not exist "inputScanningFiles" mkdir "inputScanningFiles"
if not exist "outputSearchableFiles" mkdir "outputSearchableFiles"

REM 4. Create Desktop Shortcut for Easy Operator Access
echo [4/4] Creating Desktop Shortcut for Scanning Team Operator...
powershell -NoProfile -Command "$ws = New-Object -ComObject WScript.Shell; $s = $ws.CreateShortcut([Environment]::GetFolderPath('Desktop') + '\Book Scanner QC & OCR.lnk'); $s.TargetPath = '%~dp0Start_24x7_OCR_Tool.bat'; $s.WorkingDirectory = '%~dp0'; $s.IconLocation = 'shell32.dll,23'; $s.Description = 'Start 24/7 Book Scanner QC, OCR & Auto-Renamer'; $s.Save()"

echo.
echo ============================================================================
echo   SETUP COMPLETE!
echo   A shortcut "Book Scanner QC & OCR" has been placed on the Desktop.
echo   Double-click Start_24x7_OCR_Tool.bat (or the Desktop icon) to launch!
echo ============================================================================
pause
