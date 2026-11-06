@echo off
title 24x7 Book Scanner QC, OCR ^& Auto-Renamer Service
cd /d "%~dp0"

if exist "C:\Program Files\nodejs\node.exe" (
    set "PATH=C:\Program Files\nodejs;%PATH%"
)

REM Check if 24/7 service is already running on port 3456; if so, just open the Dashboard!
powershell -NoProfile -Command "try { $null = Invoke-RestMethod -Uri 'http://localhost:3456/api/status' -TimeoutSec 1; exit 0 } catch { exit 1 }"
if %ERRORLEVEL% EQU 0 (
    echo 24/7 OCR Service is already running! Opening Dashboard...
    start http://localhost:3456
    exit /b 0
)

echo ============================================================================
echo   24/7 Local Searchable PDF OCR, Fast QC ^& Book/Author Auto-Renamer
echo ============================================================================
echo   Input Folder  : %~dp0inputScanningFiles
echo   Output Folder : %~dp0outputSearchableFiles
echo   Web Dashboard : http://localhost:3456
echo ============================================================================
echo   Keep this window minimized to run 24/7 continuously.
echo.

start "" cmd /c "timeout /t 2 /nobreak >nul & start http://localhost:3456"

:loop
echo [%DATE% %TIME%] Starting 24/7 OCR Watcher Service...
node src\server.js
if %ERRORLEVEL% EQU 0 (
    exit /b 0
)
echo [%DATE% %TIME%] Service interrupted. Restarting automatically in 3 seconds...
timeout /t 3 /nobreak >nul
goto loop
