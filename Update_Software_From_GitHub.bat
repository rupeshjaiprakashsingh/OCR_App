@echo off
title Update Book Scanner QC, OCR ^& Auto-Renamer Suite
cd /d "%~dp0"

echo ============================================================================
echo   Updating Book Scanner QC, OCR ^& Auto-Renamer from GitHub...
echo ============================================================================

where git >nul 2>nul
if %ERRORLEVEL% EQU 0 (
    git pull origin main
) else (
    echo Git is not installed; downloading latest release via PowerShell...
    powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Install_From_GitHub.ps1"
    exit /b 0
)

if exist "bin\WinOcrEngine.exe" del /f /q "bin\WinOcrEngine.exe"
call "%~dp0Setup_Scanning_Team_Laptop.bat"
