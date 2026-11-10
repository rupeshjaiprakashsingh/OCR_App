$ErrorActionPreference = "Stop"
$InstallDir = "C:\BookScanner_OCR_App"
$RepoZipUrl = "https://github.com/rupeshjaiprakashsingh/OCR_App/archive/refs/heads/main.zip"
$TempZip = Join-Path $env:TEMP "OCR_App_Setup.zip"
$TempExtract = Join-Path $env:TEMP "OCR_App_Extract"

Write-Host "======================================================================" -ForegroundColor Cyan
Write-Host "  Downloading Book Scanner QC, OCR & Auto-Renamer from GitHub..." -ForegroundColor Cyan
Write-Host "======================================================================" -ForegroundColor Cyan

if (Test-Path $TempZip) { Remove-Item $TempZip -Force }
if (Test-Path $TempExtract) { Remove-Item $TempExtract -Recurse -Force }

[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
Invoke-WebRequest -Uri $RepoZipUrl -OutFile $TempZip -UseBasicParsing
Expand-Archive -Path $TempZip -DestinationPath $TempExtract -Force

$ExtractedRoot = Get-ChildItem -Path $TempExtract -Directory | Select-Object -First 1
if (-not (Test-Path $InstallDir)) {
    New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null
}

Get-ChildItem -Path $ExtractedRoot.FullName -Force | ForEach-Object {
    Copy-Item -Path $_.FullName -Destination $InstallDir -Recurse -Force
}

# Remove Admin-only files on Client Laptop
Remove-Item (Join-Path $InstallDir "Admin_KeyGenerator.bat") -Force -ErrorAction SilentlyContinue
Remove-Item (Join-Path $InstallDir "Build_Client_Installer_Package.bat") -Force -ErrorAction SilentlyContinue
Remove-Item (Join-Path $InstallDir "src\keygenCli.js") -Force -ErrorAction SilentlyContinue

Remove-Item $TempZip -Force -ErrorAction SilentlyContinue
Remove-Item $TempExtract -Recurse -Force -ErrorAction SilentlyContinue

Write-Host "Running 1-Click Client Laptop Installer in $InstallDir ..." -ForegroundColor Green
Start-Process -FilePath "cmd.exe" -ArgumentList "/c `"$InstallDir\Setup_Scanning_Team_Laptop.bat`"" -WorkingDirectory $InstallDir -Wait
