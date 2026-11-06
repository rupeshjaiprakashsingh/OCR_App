# Bulk Book Scanner Production QC, Searchable PDF OCR & Auto-Renamer Suite

A fast, 100% local Windows 10/11 desktop & web application built for **Bulk Document & Book Scanning Teams**. Drop scanned PDF books into the input folder and the software runs **24/7** to automatically remove blank pages, crop black scanner borders, auto-rotate sideways pages, perform high-accuracy **Searchable PDF OCR**, validate data, and **rename every file with the Book Name & Author's Name extracted from Page 1**.

---

## Easy Installation on Any Client / Customer Windows Laptop

Requires **Windows 10 or Windows 11 (64-bit)**. No paid API keys or third-party OCR installs required—uses built-in Windows native OCR + `.NET`.

### Method 1: Easiest Download ZIP + 1-Click Installer (No Git Needed on Client PC)

1. Open **https://github.com/rupeshjaiprakashsingh/OCR_App** on the client laptop.
2. Click the green **`<> Code`** button $\rightarrow$ **`Download ZIP`**.
3. Extract the ZIP folder to `C:\OCR_App` (or any folder on the laptop).
4. Double-click **`Setup_Scanning_Team_Laptop.bat`**.
   - Automatically installs **Node.js LTS** (if not already installed on the laptop).
   - Installs PDF libraries (`pdf-lib`).
   - Compiles the native Windows multi-threaded OCR & Blank/Crop binary (`bin\WinOcrEngine.exe`).
   - Creates **3 ready-to-use Shortcuts right on the Customer's Desktop**:
     1. **`Book Scanner QC & OCR`** — Launches the 24/7 Watcher & opens **http://localhost:3456**
     2. **`1. Scanner Input Folder`** — Configure the scanner to save PDFs directly here
     3. **`2. Searchable PDF Output Folder`** — Auto-renamed Searchable PDFs appear here

---

### Method 2: One-Command PowerShell Install (If Repo is Public)

Open **PowerShell** on the client Windows 10/11 laptop and run:
```powershell
irm https://raw.githubusercontent.com/rupeshjaiprakashsingh/OCR_App/main/Install_From_GitHub.ps1 | iex
```

---

### Method 3: Install via Git Clone

```powershell
git clone https://github.com/rupeshjaiprakashsingh/OCR_App.git C:\OCR_App
cd C:\OCR_App
.\Setup_Scanning_Team_Laptop.bat
```

---

### Optional: Run Automatically in Background When Windows Starts (24/7 Mode)

If the client laptop is a dedicated scanning station that should start the 24/7 OCR service silently every time Windows turns on:
- Double-click **`Enable_24x7_Windows_AutoStart.bat`** once.
- (To turn off auto-startup later, double-click **`Disable_24x7_Windows_AutoStart.bat`**.)

---

## Core Production Features

1. **Page 1 Book Name & Author Extraction (`src/extractor.js`)**:
   - Analyzes front cover typography (font-height hierarchy, multi-line titles, Roman numerals) and cross-verifies with inner title pages to automatically save files as `<Book Name> - <Author Name>.pdf`.
2. **Automatic Blank Page Detection & Deletion**:
   - Pixel-level ink-density scanning (`inkRatio`) + OCR word verification removes blank separator pages automatically.
3. **Smart Scanner Border & Margin Cropping**:
   - Detects black scanner lid borders and content bounds, applying lossless PDF `CropBox` trimming (plus per-page or batch uniform crop controls in the Visual QC Studio).
4. **Auto-Orientation & Fast Page Rotation (`90° / 180° / 270°`)**:
   - Automatically detects sideways/upside-down text pages during OCR, with 1-click and hotkey rotation in QC.
5. **High-Accuracy Searchable PDF Layer + Live Page Text Editor**:
   - Embeds an invisible, aligned text layer (`3 Tr` PDF rendering mode) over every scanned page for `Ctrl+F` search and copy/paste extraction, plus an in-app per-page OCR text editor.
6. **Ultra-Fast Visual QC & Data Validation Studio (`http://localhost:3456`)**:
   - Full-book contact-sheet thumbnail grid, 6-point automated validation checklist, operator approval workflow, and **2-second PDF rebuilds**.

---

## Operator Keyboard Shortcuts (Inside Fast Visual QC Studio)

| Shortcut | Action |
| :--- | :--- |
| `←` / `→` | Previous / Next Page |
| `Delete` | Delete or Restore Selected Page |
| `R` / `L` | Rotate Selected Page `90° CW` / `90° CCW` |
| `C` | Toggle Crop on Selected Page |
| `Ctrl + Enter` | **Approve QC & Export Final Searchable PDF** |
