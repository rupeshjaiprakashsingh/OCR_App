@echo off
title 24x7 Book Scanner OCR ^& Smart Auto-Renamer Tool
cd /d "%~dp0"

echo ============================================================================
echo   24/7 Local Searchable PDF OCR ^& Book/Author Auto-Renamer Tool
echo ============================================================================
echo   Input Folder  : %~dp0inputScanningFiles
echo   Output Folder : %~dp0outputSearchableFiles
echo   Web Dashboard : http://localhost:3456
echo ============================================================================
echo.

REM Open the Web Dashboard after 2 seconds
start "" cmd /c "timeout /t 2 /nobreak >nul & start http://localhost:3456"

:loop
echo [%DATE% %TIME%] Starting 24/7 OCR Watcher Service...
node src\server.js
echo [%DATE% %TIME%] Service stopped or restarted. Restarting in 3 seconds...
timeout /t 3 /nobreak >nul
goto loop
