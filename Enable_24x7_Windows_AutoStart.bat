@echo off
title Enable 24/7 Automatic Startup with Windows
cd /d "%~dp0"

powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "$ws = New-Object -ComObject WScript.Shell; " ^
  "$startup = [Environment]::GetFolderPath('Startup'); " ^
  "$s = $ws.CreateShortcut($startup + '\BookScanner_24x7_AutoStart.lnk'); " ^
  "$s.TargetPath = '%~dp0Start_Silent_Background.vbs'; " ^
  "$s.WorkingDirectory = '%~dp0'; " ^
  "$s.Description = '24/7 Background Book Scanner OCR Service'; " ^
  "$s.Save();"

echo ============================================================================
echo   ENABLED! The 24/7 Book Scanner OCR Service will now start automatically
echo   in the background every time this Windows laptop turns on.
echo ============================================================================
pause
