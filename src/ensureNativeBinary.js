const fs = require('fs');
const path = require('path');
const { execFileSync } = require('child_process');

const ROOT_DIR = path.join(__dirname, '..');
const BIN_DIR = path.join(ROOT_DIR, 'bin');
const EXE_PATH = path.join(BIN_DIR, 'WinOcrEngine.exe');
const CS_SOURCE_PATH = path.join(__dirname, 'native', 'WinOcrEngine.cs');

/**
 * Ensures bin/WinOcrEngine.exe is compiled and ready on any Windows 10/11 laptop.
 * Automatically compiles from src/native/WinOcrEngine.cs using built-in Windows .NET csc.exe if missing.
 */
function ensureWinOcrBinary() {
  if (fs.existsSync(EXE_PATH)) {
    return EXE_PATH;
  }

  if (!fs.existsSync(BIN_DIR)) {
    fs.mkdirSync(BIN_DIR, { recursive: true });
  }

  const fwDir = 'C:\\Windows\\Microsoft.NET\\Framework64\\v4.0.30319';
  const cscPath = path.join(fwDir, 'csc.exe');
  const winmdDir = 'C:\\Windows\\System32\\WinMetadata';

  if (!fs.existsSync(cscPath)) {
    throw new Error(`Windows .NET C# compiler not found at ${cscPath}`);
  }

  console.log('[Setup] Compiling native Windows Multi-Threaded OCR Engine (WinOcrEngine.exe)...');
  const args = [
    '/nologo',
    '/optimize+',
    `/out:${EXE_PATH}`,
    `/r:${path.join(fwDir, 'System.Runtime.WindowsRuntime.dll')}`,
    `/r:${path.join(fwDir, 'System.Runtime.dll')}`,
    `/r:${path.join(fwDir, 'System.Threading.Tasks.dll')}`,
    `/r:${path.join(fwDir, 'System.Runtime.InteropServices.WindowsRuntime.dll')}`,
    `/r:${path.join(winmdDir, 'Windows.Foundation.winmd')}`,
    `/r:${path.join(winmdDir, 'Windows.Storage.winmd')}`,
    `/r:${path.join(winmdDir, 'Windows.Data.winmd')}`,
    `/r:${path.join(winmdDir, 'Windows.Graphics.winmd')}`,
    `/r:${path.join(winmdDir, 'Windows.Media.winmd')}`,
    `/r:${path.join(winmdDir, 'Windows.Globalization.winmd')}`,
    CS_SOURCE_PATH
  ];

  execFileSync(cscPath, args, { stdio: 'inherit', windowsHide: true });
  console.log('[Setup] Native OCR Engine compiled successfully.');
  return EXE_PATH;
}

module.exports = {
  ensureWinOcrBinary,
  EXE_PATH
};
