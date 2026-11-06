# Bulk Book Scanner Production QC, OCR & Smart Auto-Renamer Suite

A fast, 100% local Windows application designed for **Bulk Book Scanning Teams**. After scanning documents into the input folder, this software automatically performs **Blank Page Deletion**, **Smart Scanner Border Cropping**, **Auto-Rotation**, **High-Accuracy Searchable PDF OCR**, **Book Name & Author Auto-Renaming from Page 1**, and provides a **High-Speed Visual QC & Data Validation Studio**.

---

## Core Production Features

1. **Smart Book Name & Author Extraction from Page 1 (`src/extractor.js`)**:
   - Analyzes Page 1 (front cover) typography geometry (multi-size stacked titles, Roman numerals, possessives) and cross-verifies with inner Title Pages (academic degrees & affiliations).
   - Automatically renames every output PDF to `<Book Name> - <Author Name>.pdf`.

2. **Automatic Blank Page Detection & Removal**:
   - Pixel-level inner-page ink-density analysis (`inkRatio`) combined with OCR word filtering automatically detects blank separator pages or empty inside covers (such as Page 2 of `1.pdf`) and removes them cleanly from the final PDF.

3. **Smart Scanner Border & Margin Cropping**:
   - Detects black scanner lid shadows and content bounding boxes on every page and applies lossless PDF `CropBox` trimming without degrading image resolution.
   - Supports per-page or 1-click batch uniform cropping in the Visual QC Studio.

4. **Auto-Orientation & Fast Page Rotation (`90° / 180° / 270°`)**:
   - Automatically detects sideways or upside-down text pages during OCR and rotates them upright, with instant 1-click or `R` / `L` hotkey rotation in QC.

5. **High-Accuracy Searchable PDF Layer + Live Page Text Editor**:
   - Embeds an invisible, coordinate-aligned text layer (`3 Tr` PDF rendering mode) over every scanned page so users can search (`Ctrl+F`) and copy/extract text across the entire book.
   - Includes ligature normalization, border-speck filtering, and a live per-page OCR text verifier/editor inside the QC Studio for 100% verified text accuracy.

6. **Ultra-Fast Visual QC & Data Validation Studio (`http://localhost:3456`)**:
   - **Contact-Sheet Thumbnail Grid** with instant filters (`All Pages`, `Blanks / Deleted`, `Rotated`, `Diagrams / Low Text`).
   - **Automated 6-Point Data Validation Checklist** verifying Book Title, Author Name, Blank Removal, Cropping, Rotation, and Searchable Word Coverage.
   - **2-Second PDF Rebuild**: Any QC adjustments (deleting/restoring pages, rotating, cropping, editing text, or renaming) rebuild the final Searchable PDF in seconds using cached OCR data.

---

## Operator Keyboard Shortcuts (Inside Fast QC Studio)

| Shortcut | Action |
| :--- | :--- |
| `←` / `→` | Previous / Next Page |
| `Delete` | Delete or Restore Selected Page |
| `R` / `L` | Rotate Selected Page `90° CW` / `90° CCW` |
| `C` | Toggle Crop on Selected Page |
| `Ctrl + Enter` | **Approve QC & Export Final Searchable PDF** |

---

## Deploying to Scanning Team Laptops

1. Copy the `OCR_App` folder to any Windows 10/11 laptop.
2. Double-click **[`Setup_Scanning_Team_Laptop.bat`](file:///C:/rupesh/OCR_App/Setup_Scanning_Team_Laptop.bat)** once (verifies Node.js, compiles the native Windows multi-threaded OCR engine, creates folders, and places a **"Book Scanner QC & OCR"** shortcut on the Desktop).
3. Double-click **[`Start_24x7_OCR_Tool.bat`](file:///C:/rupesh/OCR_App/Start_24x7_OCR_Tool.bat)** (or the Desktop shortcut) to run 24/7 and open the Workbench at **http://localhost:3456**.
