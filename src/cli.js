const fs = require('fs');
const path = require('path');
const { processScannedPdf } = require('./processor');

const ROOT_DIR = path.join(__dirname, '..');
const DEFAULT_INPUT = path.join(ROOT_DIR, 'inputScanningFiles');
const DEFAULT_OUTPUT = path.join(ROOT_DIR, 'outputSearchableFiles');
const TEMP_DIR = path.join(ROOT_DIR, 'data', 'temp');

async function runCli() {
  const args = process.argv.slice(2);
  const inputTarget = args[0] && !args[0].startsWith('--') ? path.resolve(args[0]) : DEFAULT_INPUT;
  const outputDir = args[1] && !args[1].startsWith('--') ? path.resolve(args[1]) : DEFAULT_OUTPUT;

  let files = [];
  if (fs.existsSync(inputTarget) && fs.statSync(inputTarget).isFile()) {
    files = [inputTarget];
  } else if (fs.existsSync(inputTarget) && fs.statSync(inputTarget).isDirectory()) {
    files = fs
      .readdirSync(inputTarget)
      .filter((f) => f.toLowerCase().endsWith('.pdf'))
      .sort((a, b) => a.localeCompare(b, undefined, { numeric: true }))
      .map((f) => path.join(inputTarget, f));
  } else {
    console.error(`Input path not found: ${inputTarget}`);
    process.exit(1);
  }

  console.log(`Found ${files.length} PDF file(s) to OCR and auto-rename.`);
  for (let i = 0; i < files.length; i++) {
    const pdfPath = files[i];
    console.log(`\n[${i + 1}/${files.length}] Processing: ${path.basename(pdfPath)}`);
    let lastLoggedPct = -1;

    const result = await processScannedPdf({
      inputPdfPath: pdfPath,
      outputDir,
      tempDir: TEMP_DIR,
      concurrency: 6,
      scale: 1.5,
      onMetaExtracted: (meta) => {
        console.log(`  -> Extracted Book Title : ${meta.bookTitle}`);
        console.log(`  -> Extracted Author     : ${meta.authorName}`);
        console.log(`  -> Target Output File   : ${meta.outputFileName} (${meta.pageCount} pages)`);
      },
      onProgress: (done, total, stage) => {
        if (stage === 'ocr' && total > 0) {
          const pct = Math.floor((done / total) * 10) * 10;
          if (pct > lastLoggedPct && (pct % 20 === 0 || done === total)) {
            lastLoggedPct = pct;
            console.log(`  -> OCR Progress: ${done}/${total} pages (${ Math.round((done / total) * 100)}%)`);
          }
        } else if (stage === 'building_pdf') {
          console.log(`  -> Building Searchable PDF with embedded invisible text layer...`);
        }
      }
    });

    console.log(`  -> DONE in ${result.elapsedSeconds}s!`);
    console.log(`  -> Saved Searchable PDF : ${result.outputPdfPath}`);
    console.log(`  -> Saved Extracted Text : ${result.textOutPath}`);
    console.log(`  -> Searchable Words     : ${result.totalWordsEmbedded.toLocaleString()} words across ${result.pagesWithText}/${result.pageCount} pages`);
  }
}

runCli().catch((err) => {
  console.error('CLI Error:', err);
  process.exit(1);
});
