const path = require('path');

let ocrInstancePromise = null;

/**
 * Checks whether @gutenye/ocr-node (RapidOCR / PaddleOCR PP-OCRv4 ONNX Runtime) is installed.
 */
function isRapidOcrAvailable() {
  try {
    require.resolve('@gutenye/ocr-node');
    return true;
  } catch (_) {
    return false;
  }
}

/**
 * Lazily initializes the singleton PaddleOCR PP-OCRv4 ONNX Runtime engine.
 * Uses:
 *  - onnxruntime.dll (native C++ ONNX Runtime engine)
 *  - ch_PP-OCRv4_det_infer.onnx (DBNet text detection)
 *  - ch_PP-OCRv4_rec_infer.onnx (SVTR-LCNet text recognition)
 *  - ch_ppocr_mobile_v2.0_cls_infer.onnx (180-degree text line angle classification)
 */
async function getRapidOcrEngine() {
  if (!ocrInstancePromise) {
    ocrInstancePromise = (async () => {
      const OcrMod = require('@gutenye/ocr-node');
      const Ocr = OcrMod.default || OcrMod;
      return await Ocr.create();
    })();
  }
  return ocrInstancePromise;
}

/**
 * Splits a recognized text line box [x, y, w, h] into proportional word-level bounding boxes
 * so buildSearchablePdf() can align every individual word in the PDF 3 Tr text layer.
 */
function splitLineIntoWordBoxes(lineText, lineX, lineY, lineW, lineH) {
  const rawTokens = String(lineText || '')
    .trim()
    .split(/\s+/)
    .filter(Boolean);
  if (rawTokens.length === 0) return [];

  if (rawTokens.length === 1) {
    return [
      {
        t: rawTokens[0],
        x: Number(lineX.toFixed(1)),
        y: Number(lineY.toFixed(1)),
        w: Number(Math.max(1, lineW).toFixed(1)),
        h: Number(Math.max(4, lineH).toFixed(1))
      }
    ];
  }

  const totalChars = rawTokens.reduce((acc, w) => acc + w.length, 0);
  const totalSpaceUnits = (rawTokens.length - 1) * 0.55;
  const unitWidth = lineW / Math.max(1, totalChars + totalSpaceUnits);

  const words = [];
  let cursorX = lineX;
  for (let i = 0; i < rawTokens.length; i++) {
    const token = rawTokens[i];
    const wordW = Math.max(2, token.length * unitWidth);
    words.push({
      t: token,
      x: Number(cursorX.toFixed(1)),
      y: Number(lineY.toFixed(1)),
      w: Number(wordW.toFixed(1)),
      h: Number(Math.max(4, lineH).toFixed(1))
    });
    cursorX += wordW + 0.55 * unitWidth;
  }
  return words;
}

/**
 * Runs RapidOCR / PaddleOCR (PP-OCRv4 ONNX) on an image path or Buffer and returns
 * normalized OCR lines & word bounding boxes compatible with buildSearchablePdf().
 */
async function recognizeImageWithRapidOcr(imagePathOrBuffer) {
  const ocr = await getRapidOcrEngine();
  const result = await ocr.detect(imagePathOrBuffer);
  const rawLines = Array.isArray(result) ? result : (result && result.texts) || [];

  const lines = [];
  let wordCount = 0;
  let scoreSum = 0;

  for (const item of rawLines) {
    const text = String(item.text || '').trim();
    if (!text) continue;

    const box = item.box || [];
    if (box.length < 4) continue;

    const xs = box.map(pt => pt[0]);
    const ys = box.map(pt => pt[1]);
    const minX = Math.min(...xs);
    const maxX = Math.max(...xs);
    const minY = Math.min(...ys);
    const maxY = Math.max(...ys);
    const w = Math.max(1, maxX - minX);
    const h = Math.max(4, maxY - minY);

    const words = splitLineIntoWordBoxes(text, minX, minY, w, h);
    if (words.length === 0) continue;

    wordCount += words.length;
    scoreSum += typeof item.mean === 'number' ? item.mean * 100 : 98.0;

    lines.push({
      text,
      x: Number(minX.toFixed(1)),
      y: Number(minY.toFixed(1)),
      w: Number(w.toFixed(1)),
      h: Number(h.toFixed(1)),
      avgWordH: Number(h.toFixed(1)),
      words
    });
  }

  const avgConfidence = lines.length > 0 ? Number((scoreSum / lines.length).toFixed(1)) : 100.0;
  return {
    engine: 'RapidOCR / PaddleOCR PP-OCRv4 (ONNX Runtime)',
    imgWidth: result && result.resizedImageWidth ? result.resizedImageWidth : 0,
    imgHeight: result && result.resizedImageHeight ? result.resizedImageHeight : 0,
    confidence: avgConfidence,
    wordCount,
    lines
  };
}

module.exports = {
  isRapidOcrAvailable,
  getRapidOcrEngine,
  recognizeImageWithRapidOcr
};
