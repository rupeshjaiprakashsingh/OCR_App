@echo off
title Disable 24/7 Automatic Startup with Windows

powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "$startup = [Environment]::GetFolderPath('Startup'); " ^
  "$lnk = Join-Path $startup 'BookScanner_24x7_AutoStart.lnk'; " ^
  "if (Test-Path $lnk) { Remove-Item $lnk -Force }"

echo ============================================================================
echo   DISABLED automatic Windows startup for Book Scanner OCR Service.
echo ============================================================================
pause
