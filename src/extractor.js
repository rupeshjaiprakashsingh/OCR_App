const { execFile } = require('child_process');
const path = require('path');
const { ensureWinOcrBinary } = require('./ensureNativeBinary');

const WIN_OCR_EXE = ensureWinOcrBinary();

const ROMAN_NUMERALS = new Set([
  'I', 'II', 'III', 'IV', 'V', 'VI', 'VII', 'VIII', 'IX', 'X',
  'XI', 'XII', 'XIII', 'XIV', 'XV'
]);

const ACRONYMS = new Set([
  'AC', 'DC', 'HVDC', 'HVAC', 'VLSI', 'ULSI', 'PCB', 'IC', 'RF', 'DSP',
  'CAD', 'CAM', 'CAE', 'CNC', 'PLC', 'SCADA', 'IT', 'AI', 'ML', 'IOT',
  'DBMS', 'RDBMS', 'SQL', 'OS', 'OOP', 'OOPS', 'HTML', 'CSS', 'XML',
  'TCP', 'IP', 'HTTP', 'LAN', 'WAN', 'MAN', 'GSM', 'CDMA', 'LTE', 'OFDM',
  'CMOS', 'MOSFET', 'BJT', 'FET', 'IGBT', 'SCR', 'PWM', 'UPS', 'SMPS',
  'LED', 'LCD', 'CRT', 'LASER', 'MASER', 'RADAR', 'SONAR', 'GPS', 'GIS',
  'MSBTE', 'AICTE', 'UGC', 'IEEE', 'ISO', 'BIS', 'DNA', 'RNA', 'HIV', 'AIDS'
]);

const MINOR_WORDS = new Set([
  'a', 'an', 'the', 'and', 'but', 'or', 'nor', 'for', 'so', 'yet',
  'at', 'by', 'from', 'in', 'into', 'of', 'off', 'on', 'onto',
  'out', 'over', 'to', 'up', 'with', 'as'
]);

const NON_TITLE_NOISE_PATTERNS = [
  /https?:\/\//i,
  /www\./i,
  /\.com\b/i,
  /\bprice\b/i,
  /\bisbn\b/i,
  /\bactivation\s+code\b/i,
  /\bsample\s+activation\b/i,
  /\bfree\s+trial\b/i,
  /\bscratch\s+the\s+card\b/i,
  /\binstructions\s+to\s+download\b/i,
  /\bfeatures\s+in\s+e-?books?\b/i,
  /\bentire\s+book\s+in\b/i,
  /\bmulticolour\s+diagrams\b/i,
  /\bsolved\s+.*question\s+papers\b/i,
  /\bwith\s+solution\b/i,
  /\bstrictly\s+as\s+per\b/i,
  /\bw\.?e\.?f\.?\b/i,
  /\bacademic\s+year\b/i,
  /\bscheme\b/i,
  /\b(first|second|third|fourth|fifth|sixth|seventh|eighth|ninth|tenth|final)\s+(year|edition)\b/i,
  /\bsemester\b/i,
  /\bdiploma\b/i,
  /\bengineering\s+group\b/i,
  /\bpolytechnic\s+division\b/i,
  /\binnovation\s+throughout\b/i,
  /\bpublications?\b/i,
  /\bprivate\s+limited\b/i,
  /\bpvt\.?\s*ltd\b/i,
  /\bnew\s+delhi\b/i,
  /\beastern\s+economy\b/i,
  /\bmanufactured\s+in\b/i,
  /\blicensed\s+for\s+distribution\b/i,
  /\bpharmaceutical\s+premium\b/i,
  /\bsale\s+is\s+prohibited\b/i,
  /\bsouth\s+asian\b/i,
  /\bedition\s+for\b/i,
  /\bpharma\b/i,
  /\bdistribution\b/i,
  /\bwolters\s+kluwer\b/i,
  /\bkluwer\b/i,
  /^[0-9A-Z]{12,20}$/
];

const NON_AUTHOR_PATTERNS = [
  /https?:\/\//i,
  /www\./i,
  /\.com\b/i,
  /\b(msbte|aicte|ugc|scheme|revised|strictly|syllabus|academic|year|w\.e\.f)\b/i,
  /\b(first|second|third|fourth|fifth|sixth|seventh|eighth|ninth|tenth|final)\s+(year|edition)\b/i,
  /\b(diploma|semester|sem\b|degree|polytechnic)\b/i,
  /\b(group|branch|department|dept|division|section|faculty|school|center|centre|hospital|clinic)\b/i,
  /\b(institute|college|university|academy|vidyapeeth|bhavan|foundation|society)\b/i,
  /\b(professor|lecturer|investigator|head|principal|dean|director|scholar|scientist|vice-?chair|endowed|chair|chief|emeritus|epidemiologist)\b/i,
  /\b(publications?|publishers?|publishing|press|house|company|books?|series|guide|clinical|textbook|manual|handbook|principles|practice)\b/i,
  /\b(prentice|hall|mcgraw|pearson|wiley|springer|elsevier|oxford|cambridge|tech-?max|nirali|chand|khanna|laxmi|wolters|kluwer|lippincott|jaypee|thieme|saunders|mosby|cengage|arihant|routledge)\b/i,
  /\b(private|limited|pvt|ltd|inc|corp|llp)\b/i,
  /\b(edition|eastern|economy|reprint|volume|vol|part|chapter|unit|module|index|contents|contributors|preface|foreword)\b/i,
  /\b(south|north|east|west|central|asian|indian|american|british|european|international|national|global|world|foreign)\b/i,
  /\b(manufactured|licensed|distribution|pharma|pharmaceutical|premium|prohibited|trade|only|reserved|rights|copyright)\b/i,
  /\b(features|ebook|e-book|e-format|multicolour|diagram|question|paper|solution|solved|sample|activation|code|trial|free|download|app|install|login|signup|register|user|device|windows|android|ios|desktop|library|account)\b/i,
  /\b(price|rs\.?|inr|isbn|issn|code|book\s+code)\b/i,
  /\b(pune|mumbai|delhi|calcutta|kolkata|chennai|madras|bangalore|bengaluru|hyderabad|ahmedabad|nagpur|nashik|aurangabad|howrah|maharashtra|india|oregon|portland|california|davis|sacramento|philadelphia|baltimore|london|sydney|tokyo|pittsburgh)\b/i,
  /\b(innovation|throughout)\b/i,
  /\d{2,}/
];

const CREDENTIAL_PATTERNS = [
  /\b(m\.?e\.?|b\.?e\.?|m\.?tech\.?|b\.?tech\.?|ph\.?d\.?|m\.?sc\.?|b\.?sc\.?|m\.?phil\.?|d\.?sc\.?|m\.?d\.?|m\.?p\.?h\.?|m\.?b\.?b\.?s\.?)\b/i,
  /\b(professor|lecturer|reader|assistant\s+professor|associate\s+professor|chief\s+investigator|head\s+of|hod|dean|principal|former(ly)?|vice-?chair|director\s+of|endowed)\b/i,
  /\b(department\s+of|institute\s+of|college\s+of|university|faculty\s+of|school\s+of|engineering\s+college|polytechnic)\b/i
];

/**
 * Strips trailing academic degrees (e.g. ", MD, MPH", ", PhD", ", M.E.") and leading "By" / "Dr."
 */
function stripDegreesAndTitles(rawText) {
  if (!rawText) return '';
  let s = rawText
    .replace(/\u2019/g, "'")
    .replace(/^by\s+/i, '')
    .trim();

  // Fix common OCR parenthesis-for-C at start of capitalized surname: "(reinin" -> "Creinin"
  s = s.replace(/\(([a-z]{3,})\b/g, (_, rest) => 'C' + rest);

  // Strip trailing degree suffixes after comma or space
  s = s.replace(
    /(\s*[,;]\s*|\s+)(MD|M\.D\.|MI\)|MPH|M\.P\.H\.|PhD|Ph\.D\.|MSc|M\.Sc\.|MSCR|MAS|FACOG|FRCOG|FACS|MBBS|M\.E\.|B\.E\.|M\.Tech\.?|B\.Tech\.?)(\s*[,;]\s*(MD|M\.D\.|MI\)|MPH|M\.P\.H\.|PhD|Ph\.D\.|MSc|M\.Sc\.|MSCR|MAS|FACOG|FRCOG|FACS|MBBS))*$/gi,
    ''
  );

  return s.replace(/[,;:\s]+$/, '').trim();
}

function toSmartTitleCase(rawTitle) {
  if (!rawTitle) return '';
  const cleaned = rawTitle
    .replace(/\s+/g, ' ')
    .replace(/\s*-\s*/g, '-')
    .trim();

  const words = cleaned.split(' ');
  return words
    .map((word, index) => {
      if (word === '&') return '&';
      const parts = word.split('-');
      const formattedParts = parts.map((part, pIdx) => {
        // Preserve possessive 's
        const hasPossessive = /['’]s$/i.test(part);
        const basePart = hasPossessive ? part.replace(/['’]s$/i, '') : part;
        const cleanAlpha = basePart.replace(/[^a-zA-Z0-9]/g, '');
        const upper = cleanAlpha.toUpperCase();

        let formattedBase;
        if (pIdx > 0 && (upper === '11' || upper === 'I1' || upper === '1I')) {
          formattedBase = 'II';
        } else if (ROMAN_NUMERALS.has(upper)) {
          formattedBase = upper;
        } else if (ACRONYMS.has(upper)) {
          formattedBase = upper;
        } else {
          const lower = basePart.toLowerCase();
          if (index > 0 && pIdx === 0 && MINOR_WORDS.has(lower) && !hasPossessive) {
            formattedBase = lower;
          } else {
            formattedBase = lower.charAt(0).toUpperCase() + lower.slice(1);
          }
        }
        return hasPossessive ? `${formattedBase}'s` : formattedBase;
      });
      return formattedParts.join('-');
    })
    .join(' ');
}

function formatPersonName(rawName) {
  const stripped = stripDegreesAndTitles(rawName);
  if (!stripped) return '';
  const cleaned = stripped
    .replace(/,/g, ' ')
    .replace(/\s+/g, ' ')
    .trim();

  return cleaned
    .split(' ')
    .map((token) => {
      if (/^[A-Za-z]\.?$/.test(token)) {
        return token.charAt(0).toUpperCase() + '.';
      }
      if (/^([A-Za-z]\.){2,}$/.test(token)) {
        return token.toUpperCase();
      }
      const lower = token.toLowerCase();
      return lower.charAt(0).toUpperCase() + lower.slice(1);
    })
    .join(' ');
}

/**
 * Splits a single line if it contains two side-by-side person names (e.g. "Jeffrey T. Jensen Mitchell D. Creinin")
 */
function splitSideBySideAuthors(lineText) {
  const cleaned = stripDegreesAndTitles(lineText)
    .replace(/([A-Za-z])\s*,\s*(?=[A-Z])/g, '$1. ')
    .replace(/\s+/g, ' ')
    .trim();

  // Pattern: First [M.] Last   First [M.] Last
  const twoAuthorsWithInitials = /^([A-Z][a-z]+\s+[A-Z]\.?\s+[A-Z][a-z]+)\s+([A-Z][a-z]+\s+[A-Z]\.?\s+[A-Z][a-z]+)$/;
  const match = cleaned.match(twoAuthorsWithInitials);
  if (match) {
    return [match[1], match[2]];
  }
  return [cleaned];
}

function isLikelyPersonName(text) {
  if (!text) return false;
  const stripped = stripDegreesAndTitles(text);
  const cleaned = stripped
    .replace(/[.,]/g, ' ')
    .replace(/\s+/g, ' ')
    .trim();

  if (cleaned.length < 4 || cleaned.length > 48) return false;
  if (/[0-9@#$%^&*()_+=[\]{};:"\\|<>/?~`•]/.test(cleaned)) return false;

  for (const pat of NON_AUTHOR_PATTERNS) {
    if (pat.test(stripped)) return false;
  }

  const tokens = cleaned.split(' ').filter(Boolean);
  if (tokens.length < 2 || tokens.length > 4) return false;

  let fullWords = 0;
  for (const t of tokens) {
    if (!/^[A-Za-z-]+$/.test(t)) return false;
    if (t.length >= 2 && !MINOR_WORDS.has(t.toLowerCase())) {
      fullWords++;
    } else if (t.length > 1 && MINOR_WORDS.has(t.toLowerCase())) {
      return false;
    }
  }
  return fullWords >= 2;
}

function normalizeForComparison(str) {
  return (str || '')
    .toLowerCase()
    .replace(/[^a-z0-9]/g, '');
}

/**
 * Extracts Book Title and Author Name(s) from the OCR metadata of the first pages.
 */
function extractBookAndAuthorFromMeta(meta) {
  if (!meta || !Array.isArray(meta.pages) || meta.pages.length === 0) {
    return {
      bookTitle: 'Unknown Book',
      authorName: 'Unknown Author',
      confidence: 'low'
    };
  }

  const page1 = meta.pages[0];
  const p1Lines = Array.isArray(page1.lines) ? page1.lines : [];

  // ---------------------------------------------------------
  // 1. EXTRACT BOOK TITLE FROM PAGE 1 (FRONT COVER)
  // ---------------------------------------------------------
  const titleCandidatesP1 = p1Lines.filter((line) => {
    const t = (line.text || '').trim();
    if (t.length < 2) return false;
    if (!/[A-Za-z]{3,}/.test(t)) return false;
    if (/^(msbte|free|ebook|e-book|with|solution|question|papers?\.?|eastern|economy|edition|phi|only\.?|kluwer)$/i.test(t)) {
      return false;
    }
    for (const pat of NON_TITLE_NOISE_PATTERNS) {
      if (pat.test(t)) return false;
    }
    return true;
  });

  let maxWordH = 0;
  for (const line of titleCandidatesP1) {
    const h = line.avgWordH || line.h || 0;
    if (h > maxWordH) maxWordH = h;
  }

  const titleThreshold = maxWordH * 0.68;
  let p1TitleLines = titleCandidatesP1
    .filter((line) => (line.avgWordH || line.h || 0) >= titleThreshold)
    .sort((a, b) => (Math.abs(a.y - b.y) > 15 ? a.y - b.y : a.x - b.x));

  // Multi-size Cover Title Expansion:
  // If the largest title line(s) on Page 1 have smaller lead-in lines stacked directly above them
  // (e.g. "Speroff & Darney's" [H=53] -> "Clinical Guide to" [H=77] -> "Contraception" [H=163]),
  // walk upward to include vertically stacked lead-in title lines!
  if (p1TitleLines.length > 0) {
    let addedMore = true;
    while (addedMore) {
      addedMore = false;
      const topTitleLine = p1TitleLines[0];
      for (const cand of titleCandidatesP1) {
        if (p1TitleLines.includes(cand)) continue;
        const candH = cand.avgWordH || cand.h || 0;
        const verticalGap = topTitleLine.y - (cand.y + cand.h);
        const xAligned = Math.abs(cand.x - topTitleLine.x) < 90;
        const endsWithConnector = /\b(to|of|for|in|on|and|&|guide|introduction|principles|textbook|handbook|manual)\s*$/i.test(cand.text) || /['’]s\s*$/i.test(cand.text);

        if (
          candH >= maxWordH * 0.30 &&
          candH >= 32 &&
          verticalGap >= -15 &&
          verticalGap <= 110 &&
          (xAligned || endsWithConnector)
        ) {
          p1TitleLines.unshift(cand);
          addedMore = true;
          break;
        }
      }
    }
  }

  let rawBookTitle = p1TitleLines.map((l) => l.text.trim()).join(' ');
  const titleMinY = p1TitleLines.length > 0 ? Math.min(...p1TitleLines.map((l) => l.y)) : 0;
  const titleMaxY = p1TitleLines.length > 0 ? Math.max(...p1TitleLines.map((l) => l.y + l.h)) : 0;

  // Cross-check title with inner pages for Roman numeral II vs 11
  for (let i = 1; i < meta.pages.length; i++) {
    const pLines = meta.pages[i].lines || [];
    for (const l of pLines) {
      const txt = (l.text || '').trim();
      if (txt.length > 8 && /II\b/.test(txt) && /11\b/.test(rawBookTitle)) {
        const normInner = normalizeForComparison(txt.replace(/II\b/g, '11'));
        const normCover = normalizeForComparison(rawBookTitle);
        if (normInner === normCover) {
          rawBookTitle = txt;
          break;
        }
      }
    }
  }

  const bookTitle = toSmartTitleCase(rawBookTitle) || 'Untitled Book';
  const titleLinesSet = new Set(p1TitleLines);
  const normBookTitle = normalizeForComparison(bookTitle);

  // ---------------------------------------------------------
  // 2. FIND INNER TITLE PAGE AUTHORS (CROSS-VERIFICATION)
  // ---------------------------------------------------------
  const innerConfirmedAuthors = new Set();
  const innerAuthorList = [];

  for (let pIdx = 1; pIdx < meta.pages.length; pIdx++) {
    const lines = (meta.pages[pIdx].lines || []).slice().sort((a, b) => a.y - b.y);
    // Skip "Contributors" or "Contents" pages so we only use actual Title Pages
    const isContributorsOrToc = lines.some((l) =>
      /\b(contributors|table\s+of\s+contents|c\s*o\s*u\s*t\s*o\s*r\s*s|index)\b/i.test(l.text || '')
    );
    if (isContributorsOrToc) continue;

    let foundOnThisTitlePage = false;
    for (let i = 0; i < lines.length; i++) {
      const rawLineText = (lines[i].text || '').trim();
      const parts = splitSideBySideAuthors(rawLineText);

      for (const part of parts) {
        if (!isLikelyPersonName(part)) continue;
        if (normalizeForComparison(part) === normBookTitle) continue;

        // Check if original line had degrees (e.g. ", MD, MPH") OR if any of the next 1-3 lines below have credentials
        let hasCredentialSignal = /\b(MD|MPH|PhD|M\.E\.|B\.E\.|MSc)\b/.test(rawLineText);
        if (!hasCredentialSignal) {
          for (let j = i + 1; j <= Math.min(lines.length - 1, i + 3); j++) {
            const belowText = (lines[j].text || '').trim();
            if (CREDENTIAL_PATTERNS.some((pat) => pat.test(belowText))) {
              hasCredentialSignal = true;
              break;
            }
          }
        }

        if (hasCredentialSignal) {
          const formatted = formatPersonName(part);
          const norm = normalizeForComparison(formatted);
          if (norm && !innerConfirmedAuthors.has(norm)) {
            innerConfirmedAuthors.add(norm);
            innerAuthorList.push(formatted);
            foundOnThisTitlePage = true;
          }
        }
      }
    }
    // Once we found the authors on the primary inner Title Page, stop scanning further pages
    if (foundOnThisTitlePage && innerAuthorList.length > 0) {
      break;
    }
  }

  // ---------------------------------------------------------
  // 3. EXTRACT AUTHOR(S) FROM PAGE 1 (FRONT COVER)
  // ---------------------------------------------------------
  const p1AuthorCandidates = [];
  for (const line of p1Lines) {
    if (titleLinesSet.has(line)) continue;
    const rawText = (line.text || '').trim();
    const subNames = splitSideBySideAuthors(rawText);

    for (const candidateStr of subNames) {
      if (!isLikelyPersonName(candidateStr)) continue;

      const formatted = formatPersonName(candidateStr);
      const norm = normalizeForComparison(formatted);
      if (!norm || norm === normBookTitle || normBookTitle.includes(norm)) continue;

      const wordH = line.avgWordH || line.h || 10;
      let score = wordH * 2.0;

      if (line.y >= titleMinY) score += 30;
      if (line.y >= titleMaxY - 10) score += 25;
      if (/\b[A-Z]\.\s+[A-Z][a-z]+/.test(formatted)) score += 25;
      if (innerConfirmedAuthors.has(norm)) score += 120;

      p1AuthorCandidates.push({
        line,
        text: formatted,
        norm,
        wordH,
        score
      });
    }
  }

  p1AuthorCandidates.sort((a, b) => b.score - a.score);

  let finalAuthors = [];
  if (p1AuthorCandidates.length > 0) {
    const topAuthor = p1AuthorCandidates[0];
    finalAuthors.push(topAuthor);

    for (let i = 1; i < p1AuthorCandidates.length; i++) {
      const cand = p1AuthorCandidates[i];
      const heightRatio = Math.min(cand.wordH, topAuthor.wordH) / Math.max(cand.wordH, topAuthor.wordH);
      const yDist = Math.abs(cand.line.y - topAuthor.line.y);
      const xDist = Math.abs(cand.line.x - topAuthor.line.x);

      if (
        (heightRatio >= 0.78 && yDist < 220 && xDist < 400) ||
        innerConfirmedAuthors.has(cand.norm)
      ) {
        if (!finalAuthors.some((a) => a.norm === cand.norm)) {
          finalAuthors.push(cand);
        }
      }
    }

    finalAuthors.sort((a, b) => (Math.abs(a.line.y - b.line.y) > 15 ? a.line.y - b.line.y : a.line.x - b.line.x));
  }

  let authorName = finalAuthors.map((a) => a.text).join(', ');
  if (!authorName && innerAuthorList.length > 0) {
    authorName = innerAuthorList.join(', ');
  }
  if (!authorName) {
    authorName = 'Unknown Author';
  }

  return {
    bookTitle,
    authorName,
    SuggestedFileName: sanitizeFileName(`${bookTitle} - ${authorName}.pdf`),
    pageCount: meta.pageCount || 0
  };
}

function sanitizeFileName(name) {
  return name
    .replace(/[<>:"/\\|?*\x00-\x1F]/g, ' ')
    .replace(/\s+/g, ' ')
    .trim();
}

function runWinOcrMeta(pdfPath, maxPages = 8) {
  return new Promise((resolve, reject) => {
    execFile(
      WIN_OCR_EXE,
      ['meta', pdfPath, String(maxPages)],
      { maxBuffer: 50 * 1024 * 1024 },
      (err, stdout, stderr) => {
        if (err) {
          return reject(new Error(`WinOcrEngine meta failed: ${stderr || err.message}`));
        }
        try {
          const data = JSON.parse(stdout.trim());
          resolve(data);
        } catch (parseErr) {
          reject(new Error(`Failed to parse WinOcrEngine meta JSON: ${parseErr.message}`));
        }
      }
    );
  });
}

async function extractBookMetadata(pdfPath) {
  const meta = await runWinOcrMeta(pdfPath, 8);
  const extracted = extractBookAndAuthorFromMeta(meta);
  return {
    ...extracted,
    rawPage1Lines: (meta.pages && meta.pages[0] && meta.pages[0].lines) || []
  };
}

module.exports = {
  extractBookMetadata,
  extractBookAndAuthorFromMeta,
  sanitizeFileName,
  toSmartTitleCase,
  formatPersonName
};
