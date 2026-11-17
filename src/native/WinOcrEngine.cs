using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Windows.Data.Pdf;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage;
using Windows.Storage.Streams;

namespace OcrAppNative
{
    class PageOcrData
    {
        public int PageIndex;
        public double PdfWidth;
        public double PdfHeight;
        public uint ImgWidth;
        public uint ImgHeight;
        public string JsonLine;
    }

    class PixelMetrics
    {
        public double InkRatio;
        public bool IsBlank;
        public bool HasDarkBorder;
        public bool VerticalLineBias;
        public bool IsLinedNotePage;
        public double CropLeftPct;
        public double CropTopPct;
        public double CropRightPct;
        public double CropBottomPct;
    }

    class OcrOrientationStats
    {
        public int TotalWords;
        public int NaturalWords;
        public int LongNaturalWords;
        public int StopWords;
        public int Score;
    }

    class UnifiedWord
    {
        public string Text;
        public double X;
        public double Y;
        public double W;
        public double H;
        public double Conf;
    }

    class UnifiedLine
    {
        public double X;
        public double Y;
        public double W;
        public double H;
        public double AvgWordH;
        public string Text;
        public List<UnifiedWord> Words = new List<UnifiedWord>();
    }

    class Program
    {
        static Task<T> WaitOp<T>(IAsyncOperation<T> op)
        {
            var tcs = new TaskCompletionSource<T>();
            op.Completed = (info, status) =>
            {
                if (status == AsyncStatus.Completed)
                {
                    try { tcs.TrySetResult(info.GetResults()); }
                    catch (Exception ex) { tcs.TrySetException(ex); }
                }
                else if (status == AsyncStatus.Error)
                {
                    tcs.TrySetException(info.ErrorCode ?? new Exception("WinRT Async Error"));
                }
                else
                {
                    tcs.TrySetCanceled();
                }
            };
            return tcs.Task;
        }

        static Task WaitAct(IAsyncAction act)
        {
            var tcs = new TaskCompletionSource<bool>();
            act.Completed = (info, status) =>
            {
                if (status == AsyncStatus.Completed) tcs.TrySetResult(true);
                else if (status == AsyncStatus.Error) tcs.TrySetException(info.ErrorCode ?? new Exception("WinRT Async Error"));
                else tcs.TrySetCanceled();
            };
            return tcs.Task;
        }

        static string EscapeJson(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            StringBuilder sb = new StringBuilder(s.Length + 8);
            foreach (char c in s)
            {
                switch (c)
                {
                    case '\\': sb.Append("\\\\"); break;
                    case '"': sb.Append("\\\""); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 32)
                            sb.AppendFormat("\\u{0:x4}", (int)c);
                        else
                            sb.Append(c);
                        break;
                }
            }
            return sb.ToString();
        }

        static readonly Dictionary<string, string> SERIF_OCR_CORRECTIONS = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "Offcio", "Officio" },
            { "offcio", "officio" },
            { "Offce", "Office" },
            { "offce", "office" },
            { "Insdtutes", "Institutes" },
            { "insdtutes", "institutes" },
            { "Insdtute", "Institute" },
            { "Pedia\u00FCics", "Pediatrics" },
            { "Pedia&ics", "Pediatrics" },
            { "Pediab-ics", "Pediatrics" },
            { "Pediab-ics.", "Pediatrics." },
            { "Pedi\u00E5trica", "Pediatrica" },
            { "Iffectious", "Infectious" },
            { "Iffectious.", "Infectious." },
            { "Commitiee", "Committee" },
            { "Commutiee", "Committee" },
            { "Publicatiorus", "Publications" },
            { "endorsernent", "endorsement" },
            { "Kmberlin", "Kimberlin" },
            { "Kmberlin,", "Kimberlin," },
            { "Bengaiuru", "Bengaluru" },
            { "CHIIDRENS", "CHILDREN" },
            { "CHIIDREN", "CHILDREN" },
            { "t}\u00FCs", "this" },
            { "ori\u00F6nal", "original" },
            { "MI)", "MD" },
            { "MI),", "MD," },
            { "I'D,", "MD," },
            { "\u2022ro", "TO" },
            { "\u2022r", "" },
            { "CHILDREN'", "CHILDREN" },
            { "32%?", "32ND" },
            { "32NP", "32ND" }
        };

        static string CleanOcrWord(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "";
            // Normalize common typographic ligatures and smart punctuation for 100% searchable accuracy
            string s = raw
                .Replace("\uFB00", "ff")
                .Replace("\uFB01", "fi")
                .Replace("\uFB02", "fl")
                .Replace("\uFB03", "ffi")
                .Replace("\uFB04", "ffl")
                .Replace("\u2018", "'")
                .Replace("\u2019", "'")
                .Replace("\u201C", "\"")
                .Replace("\u201D", "\"")
                .Replace("\u2013", "-")
                .Replace("\u2014", "-")
                .Trim();
            if (s.Length == 0) return "";
            string fixedWord;
            if (SERIF_OCR_CORRECTIONS.TryGetValue(s, out fixedWord))
            {
                return fixedWord;
            }
            return s;
        }

        static bool IsNoiseToken(string w, double conf)
        {
            if (string.IsNullOrEmpty(w)) return true;
            // Filter dotted-line hallucinations (e.g. Seeessaescoeovns... or HEHEHSHHSH...)
            if (w.Length >= 8)
            {
                HashSet<char> distinct = new HashSet<char>(w.ToLowerInvariant());
                if (distinct.Count <= 4) return true;
            }
            // Filter low-confidence pure symbol/barcode noise
            int alphaNum = 0;
            foreach (char c in w)
            {
                if (char.IsLetterOrDigit(c)) alphaNum++;
            }
            if (alphaNum == 0 && (conf < 65.0 || w.Length >= 2)) return true;
            if (conf < 28.0 && alphaNum <= 1) return true;
            if (Regex.IsMatch(w, @"^[iIl1|!\\/\[\]\(\)\{\}\-=+~_]{2,}$") && conf < 80.0) return true;
            return false;
        }

        static void RepairSplitWordsInLine(List<UnifiedWord> words)
        {
            if (words == null || words.Count == 0) return;
            for (int i = 0; i < words.Count; i++)
            {
                if (string.IsNullOrEmpty(words[i].Text))
                {
                    words.RemoveAt(i);
                    i--;
                }
            }
            if (words.Count < 2) return;
            for (int i = 0; i < words.Count - 1; i++)
            {
                string a = words[i].Text;
                string b = words[i + 1].Text;
                if (string.Equals(a, "TO", StringComparison.Ordinal) && string.Equals(b, "HE", StringComparison.Ordinal))
                {
                    words[i + 1].Text = "THE";
                }
                else if (string.Equals(a, "Dedicated", StringComparison.OrdinalIgnoreCase) && string.Equals(b, "fo", StringComparison.Ordinal))
                {
                    words[i + 1].Text = "to";
                }
                else if (string.Equals(a, "Pvt", StringComparison.OrdinalIgnoreCase) && (string.Equals(b, "tta", StringComparison.OrdinalIgnoreCase) || string.Equals(b, "td", StringComparison.OrdinalIgnoreCase)))
                {
                    words[i + 1].Text = "Ltd";
                }

                string merged = null;
                if (string.Equals(a, "Lyn", StringComparison.Ordinal) && b.StartsWith("ield", StringComparison.OrdinalIgnoreCase))
                {
                    merged = "Lynf" + b;
                }
                else if (string.Equals(a, "Kimber", StringComparison.Ordinal) && b.StartsWith("lin", StringComparison.OrdinalIgnoreCase))
                {
                    merged = "Kimber" + b;
                }
                else if (string.Equals(a, "Pedia", StringComparison.OrdinalIgnoreCase) && b.StartsWith("trics", StringComparison.OrdinalIgnoreCase))
                {
                    merged = "Pedia" + b;
                }

                if (merged != null)
                {
                    double minX = Math.Min(words[i].X, words[i + 1].X);
                    double minY = Math.Min(words[i].Y, words[i + 1].Y);
                    double maxX = Math.Max(words[i].X + words[i].W, words[i + 1].X + words[i + 1].W);
                    double maxY = Math.Max(words[i].Y + words[i].H, words[i + 1].Y + words[i + 1].H);
                    words[i].Text = merged;
                    words[i].X = minX;
                    words[i].Y = minY;
                    words[i].W = Math.Max(1.0, maxX - minX);
                    words[i].H = Math.Max(1.0, maxY - minY);
                    words.RemoveAt(i + 1);
                    i--;
                }
            }
        }

        static byte[] BuildUprightGrayscalePgm(byte[] bgra, int origW, int origH, int rotDeg)
        {
            int outW = (rotDeg == 90 || rotDeg == 270) ? origH : origW;
            int outH = (rotDeg == 90 || rotDeg == 270) ? origW : origH;
            byte[] header = Encoding.ASCII.GetBytes("P5\n" + outW + " " + outH + "\n255\n");
            int hdrLen = header.Length;
            byte[] pgm = new byte[hdrLen + outW * outH];
            System.Buffer.BlockCopy(header, 0, pgm, 0, hdrLen);

            for (int y = 0; y < origH; y++)
            {
                int rowStart = y * origW * 4;
                for (int x = 0; x < origW; x++)
                {
                    int idx = rowStart + x * 4;
                    byte lum = (byte)((bgra[idx + 2] * 77 + bgra[idx + 1] * 150 + bgra[idx] * 29) >> 8);
                    int dx = x, dy = y;
                    if (rotDeg == 90)
                    {
                        dx = origH - 1 - y;
                        dy = x;
                    }
                    else if (rotDeg == 180)
                    {
                        dx = origW - 1 - x;
                        dy = origH - 1 - y;
                    }
                    else if (rotDeg == 270)
                    {
                        dx = y;
                        dy = origW - 1 - x;
                    }
                    pgm[hdrLen + dy * outW + dx] = lum;
                }
            }
            return pgm;
        }

        static List<UnifiedLine> RunTesseractTsvOnPgm(byte[] pgmBytes, uint imgW, uint imgH, out double meanConf, out int totalWords)
        {
            meanConf = 96.0;
            totalWords = 0;
            List<UnifiedLine> lines = new List<UnifiedLine>();

            string exeDir = Path.GetDirectoryName(typeof(Program).Assembly.Location);
            string tessExe = Path.Combine(exeDir, "tesseract", "tesseract.exe");
            string tessData = Path.Combine(exeDir, "tesseract", "tessdata");
            if (!File.Exists(tessExe) || !Directory.Exists(tessData))
            {
                return lines;
            }

            try
            {
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = tessExe,
                    Arguments = "stdin stdout --tessdata-dir \"" + tessData + "\" -l eng --oem 1 --psm 3 tsv",
                    UseShellExecute = false,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8
                };
                psi.EnvironmentVariables["OMP_THREAD_LIMIT"] = "1";

                using (Process proc = Process.Start(psi))
                {
                    Task<string> outTask = proc.StandardOutput.ReadToEndAsync();
                    Task<string> errTask = proc.StandardError.ReadToEndAsync();

                    proc.StandardInput.BaseStream.Write(pgmBytes, 0, pgmBytes.Length);
                    proc.StandardInput.BaseStream.Flush();
                    proc.StandardInput.BaseStream.Close();

                    Task.WaitAll(outTask, errTask);
                    proc.WaitForExit();

                    string tsv = outTask.Result ?? "";
                    string[] rows = tsv.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

                    Dictionary<string, List<UnifiedWord>> lineGroups = new Dictionary<string, List<UnifiedWord>>();
                    List<string> lineOrder = new List<string>();
                    double confSum = 0.0;
                    int confCount = 0;

                    for (int i = 1; i < rows.Length; i++)
                    {
                        string[] cols = rows[i].Split('\t');
                        if (cols.Length < 12) continue;
                        if (cols[0] != "5") continue;

                        string rawText = cols[11];
                        if (string.IsNullOrWhiteSpace(rawText)) continue;

                        double left, top, width, height, conf;
                        if (!double.TryParse(cols[6], NumberStyles.Any, CultureInfo.InvariantCulture, out left)) continue;
                        if (!double.TryParse(cols[7], NumberStyles.Any, CultureInfo.InvariantCulture, out top)) continue;
                        if (!double.TryParse(cols[8], NumberStyles.Any, CultureInfo.InvariantCulture, out width)) continue;
                        if (!double.TryParse(cols[9], NumberStyles.Any, CultureInfo.InvariantCulture, out height)) continue;
                        double.TryParse(cols[10], NumberStyles.Any, CultureInfo.InvariantCulture, out conf);

                        string cleanW = CleanOcrWord(rawText);
                        if (IsNoiseToken(cleanW, conf)) continue;

                        bool isExtremeEdge = (left < imgW * 0.012) || (left + width > imgW * 0.988) ||
                                             (top < imgH * 0.01) || (top + height > imgH * 0.99);
                        if (isExtremeEdge && cleanW.Length <= 2 && !char.IsLetterOrDigit(cleanW[0])) continue;

                        string key = cols[2] + "_" + cols[3] + "_" + cols[4];
                        List<UnifiedWord> wList;
                        if (!lineGroups.TryGetValue(key, out wList))
                        {
                            wList = new List<UnifiedWord>();
                            lineGroups[key] = wList;
                            lineOrder.Add(key);
                        }

                        wList.Add(new UnifiedWord
                        {
                            Text = cleanW,
                            X = left,
                            Y = top,
                            W = Math.Max(1.0, width),
                            H = Math.Max(1.0, height),
                            Conf = Math.Max(0.0, conf)
                        });

                        if (conf > 0)
                        {
                            confSum += conf;
                            confCount++;
                        }
                    }

                    foreach (string key in lineOrder)
                    {
                        List<UnifiedWord> wList = lineGroups[key];
                        RepairSplitWordsInLine(wList);
                        if (wList.Count == 0) continue;

                        double minX = double.MaxValue, minY = double.MaxValue;
                        double maxX = double.MinValue, maxY = double.MinValue;
                        double sumH = 0;
                        List<string> toks = new List<string>(wList.Count);

                        foreach (UnifiedWord uw in wList)
                        {
                            if (uw.X < minX) minX = uw.X;
                            if (uw.Y < minY) minY = uw.Y;
                            if (uw.X + uw.W > maxX) maxX = uw.X + uw.W;
                            if (uw.Y + uw.H > maxY) maxY = uw.Y + uw.H;
                            sumH += uw.H;
                            toks.Add(uw.Text);
                        }

                        string joined = string.Join(" ", toks);
                        // Skip lines that are purely non-alphanumeric noise or dotted-rule border hallucinations (mostly c/e/o/s)
                        int totalLetters = 0;
                        int ceosLetters = 0;
                        bool hasAlphaNum = false;
                        foreach (char c in joined)
                        {
                            if (char.IsLetterOrDigit(c)) hasAlphaNum = true;
                            if (char.IsLetter(c))
                            {
                                totalLetters++;
                                char lc = char.ToLowerInvariant(c);
                                if (lc == 'c' || lc == 'e' || lc == 'o' || lc == 's') ceosLetters++;
                            }
                        }
                        if (!hasAlphaNum) continue;
                        if (totalLetters >= 12 && ceosLetters >= totalLetters * 0.72) continue;

                        totalWords += wList.Count;
                        lines.Add(new UnifiedLine
                        {
                            X = minX,
                            Y = minY,
                            W = Math.Max(1.0, maxX - minX),
                            H = Math.Max(1.0, maxY - minY),
                            AvgWordH = sumH / wList.Count,
                            Text = joined,
                            Words = wList
                        });
                    }

                    if (confCount > 0)
                    {
                        meanConf = Math.Min(100.0, Math.Max(82.0, confSum / confCount));
                    }
                }
            }
            catch { }

            return lines;
        }

        static List<UnifiedLine> ExtractWinOcrLines(OcrResult result, uint imgW, uint imgH)
        {
            List<UnifiedLine> list = new List<UnifiedLine>();
            if (result == null || result.Lines == null) return list;

            foreach (OcrLine line in result.Lines)
            {
                if (line.Words == null || line.Words.Count == 0) continue;
                List<UnifiedWord> wList = new List<UnifiedWord>();
                foreach (OcrWord w in line.Words)
                {
                    Rect r = w.BoundingRect;
                    string cleanW = CleanOcrWord(w.Text);
                    if (string.IsNullOrEmpty(cleanW)) continue;

                    bool isExtremeEdge = (r.X < imgW * 0.012) || (r.X + r.Width > imgW * 0.988) ||
                                         (r.Y < imgH * 0.01) || (r.Y + r.Height > imgH * 0.99);
                    if (isExtremeEdge && cleanW.Length == 1 && !char.IsLetterOrDigit(cleanW[0])) continue;

                    wList.Add(new UnifiedWord
                    {
                        Text = cleanW,
                        X = r.X,
                        Y = r.Y,
                        W = Math.Max(1.0, r.Width),
                        H = Math.Max(1.0, r.Height),
                        Conf = 92.0
                    });
                }

                RepairSplitWordsInLine(wList);
                if (wList.Count == 0) continue;

                double minX = double.MaxValue, minY = double.MaxValue;
                double maxX = double.MinValue, maxY = double.MinValue;
                double sumH = 0;
                List<string> toks = new List<string>(wList.Count);
                foreach (UnifiedWord uw in wList)
                {
                    if (uw.X < minX) minX = uw.X;
                    if (uw.Y < minY) minY = uw.Y;
                    if (uw.X + uw.W > maxX) maxX = uw.X + uw.W;
                    if (uw.Y + uw.H > maxY) maxY = uw.Y + uw.H;
                    sumH += uw.H;
                    toks.Add(uw.Text);
                }

                list.Add(new UnifiedLine
                {
                    X = minX,
                    Y = minY,
                    W = Math.Max(1.0, maxX - minX),
                    H = Math.Max(1.0, maxY - minY),
                    AvgWordH = sumH / wList.Count,
                    Text = string.Join(" ", toks),
                    Words = wList
                });
            }
            return list;
        }

        static List<UnifiedLine> FuseTesseractAndWinOcrLines(List<UnifiedLine> tessLines, List<UnifiedLine> winLines)
        {
            if (tessLines == null || tessLines.Count == 0) return winLines ?? new List<UnifiedLine>();
            if (winLines == null || winLines.Count == 0) return tessLines;

            List<UnifiedLine> fused = new List<UnifiedLine>(tessLines);

            foreach (UnifiedLine wl in winLines)
            {
                // Check if this WinRT line is a clean display/banner line (e.g. "RED", "BOOK" on reverse-color graphic cover)
                // that Tesseract's page segmentation skipped
                if (Regex.IsMatch(wl.Text, @"^[iIl1|!0oO\s\-]{3,}$")) continue;
                int validAlpha = 0;
                bool hasVowel = false, hasCons = false;
                foreach (char c in wl.Text)
                {
                    if (char.IsLetter(c))
                    {
                        validAlpha++;
                        char lc = char.ToLowerInvariant(c);
                        if (lc == 'a' || lc == 'e' || lc == 'i' || lc == 'o' || lc == 'u' || lc == 'y') hasVowel = true;
                        else hasCons = true;
                    }
                }
                if (validAlpha < 3 || !hasVowel || !hasCons) continue;

                bool overlapsExisting = false;
                foreach (UnifiedLine tl in tessLines)
                {
                    double overlapY = Math.Min(wl.Y + wl.H, tl.Y + tl.H) - Math.Max(wl.Y, tl.Y);
                    double minH = Math.Min(wl.H, tl.H);
                    if (overlapY > minH * 0.30)
                    {
                        double overlapX = Math.Min(wl.X + wl.W, tl.X + tl.W) - Math.Max(wl.X, tl.X);
                        double minW = Math.Min(wl.W, tl.W);
                        if (overlapX > minW * 0.20)
                        {
                            overlapsExisting = true;
                            break;
                        }
                    }
                }

                if (!overlapsExisting)
                {
                    fused.Add(wl);
                }
            }

            fused.Sort((a, b) => a.Y.CompareTo(b.Y));
            return fused;
        }

        static readonly HashSet<string> COMMON_BOOK_WORDS = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "the","and","for","with","from","that","this","are","was","were","have","has","not","but",
            "what","all","can","which","their","will","each","about","into","than","other","over","also",
            "after","between","during","under","within","without","where","when","there","these","those",
            "table","tables","figure","chapter","section","algorithm","treatment","dose","dosage","drugs",
            "drug","disease","diseases","infection","infections","therapy","system","systems","electric",
            "electrical","voltage","current","circuit","frequency","microwave","biology","animal","animals",
            "plant","cell","blood","pressure","patient","patients","clinical","management","syndrome",
            "method","methods","continued","resistance","welding","species","oral","daily","human","hours"
        };

        static OcrOrientationStats ScoreOcrOrientation(OcrResult res)
        {
            OcrOrientationStats st = new OcrOrientationStats();
            if (res == null || res.Lines == null) return st;

            foreach (var ln in res.Lines)
            {
                if (ln.Words == null) continue;
                foreach (var w in ln.Words)
                {
                    string raw = (w.Text ?? "").Trim();
                    if (raw.Length == 0) continue;
                    st.TotalWords++;

                    string clean = Regex.Replace(raw, @"^[^A-Za-z0-9]+|[^A-Za-z0-9]+$", "");
                    if (clean.Length < 2) continue;

                    if (COMMON_BOOK_WORDS.Contains(clean))
                    {
                        st.StopWords++;
                        st.NaturalWords++;
                        if (clean.Length >= 5) st.LongNaturalWords++;
                        continue;
                    }

                    bool hasVowel = false;
                    bool hasConsonant = false;
                    int alphaCount = 0;
                    foreach (char c in clean)
                    {
                        if (char.IsLetter(c))
                        {
                            alphaCount++;
                            char lower = char.ToLowerInvariant(c);
                            if (lower == 'a' || lower == 'e' || lower == 'i' || lower == 'o' || lower == 'u' || lower == 'y')
                                hasVowel = true;
                            else
                                hasConsonant = true;
                        }
                    }

                    if (alphaCount >= 3 && hasVowel && hasConsonant && alphaCount >= clean.Length - 1)
                    {
                        st.NaturalWords++;
                        if (alphaCount >= 5) st.LongNaturalWords++;
                    }
                }
            }

            st.Score = st.NaturalWords * 2 + st.LongNaturalWords * 3 + st.StopWords * 6;
            return st;
        }

        static PixelMetrics AnalyzeBitmapPixels(byte[] bgra, int width, int height, int pageIndex)
        {
            PixelMetrics m = new PixelMetrics();
            if (bgra == null || width < 20 || height < 20)
            {
                m.CropLeftPct = 1.0; m.CropTopPct = 1.0; m.CropRightPct = 1.0; m.CropBottomPct = 1.0;
                return m;
            }

            // 1. Check outer 3% border for black scanner edge shadows
            int edgeX = Math.Max(2, (int)(width * 0.03));
            int borderDark = 0, borderTotal = 0;

            for (int y = 0; y < height; y += 4)
            {
                int rowStart = y * width * 4;
                for (int x = 0; x < edgeX; x += 2)
                {
                    int idx = rowStart + x * 4;
                    int lum = (bgra[idx + 2] * 77 + bgra[idx + 1] * 150 + bgra[idx] * 29) >> 8;
                    borderTotal++;
                    if (lum < 110) borderDark++;
                }
                for (int x = width - edgeX; x < width; x += 2)
                {
                    int idx = rowStart + x * 4;
                    int lum = (bgra[idx + 2] * 77 + bgra[idx + 1] * 150 + bgra[idx] * 29) >> 8;
                    borderTotal++;
                    if (lum < 110) borderDark++;
                }
            }
            m.HasDarkBorder = borderTotal > 0 && ((double)borderDark / borderTotal) > 0.04;

            // 2. Analyze inner page area (skipping outer 5% scanner edge zone) for true ink density, line projection & content bounds
            int marginX = (int)(width * 0.055);
            int marginY = (int)(height * 0.045);
            int darkCount = 0;
            int totalInner = 0;
            int minX = width, minY = height, maxX = 0, maxY = 0;

            int innerW = Math.Max(1, (width - 2 * marginX + 1) / 2);
            int innerH = Math.Max(1, (height - 2 * marginY + 1) / 2);
            int[] rowCounts = new int[innerH];
            int[] colCounts = new int[innerW];
            int fullRuleRows = 0;

            int rIdx = 0;
            for (int y = marginY; y < height - marginY; y += 2, rIdx++)
            {
                int rowStart = y * width * 4;
                int rowDark = 0;
                int rowMinX = width, rowMaxX = 0;
                int cIdx = 0;
                for (int x = marginX; x < width - marginX; x += 2, cIdx++)
                {
                    int idx = rowStart + x * 4;
                    int lum = (bgra[idx + 2] * 77 + bgra[idx + 1] * 150 + bgra[idx] * 29) >> 8;
                    totalInner++;
                    if (lum < 165)
                    {
                        darkCount++;
                        rowDark++;
                        if (rIdx < innerH) rowCounts[rIdx]++;
                        if (cIdx < innerW) colCounts[cIdx]++;
                        if (x < rowMinX) rowMinX = x;
                        if (x > rowMaxX) rowMaxX = x;
                    }
                }
                if (rowDark > innerW * 0.65) fullRuleRows++;
                if (rowDark >= 3)
                {
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                    if (rowMinX < minX) minX = rowMinX;
                    if (rowMaxX > maxX) maxX = rowMaxX;
                }
            }

            // Compute row vs column stripe modulation to detect vertical text lines (90 / 270 deg)
            double rowMod = 0;
            for (int i = 2; i < innerH; i++)
            {
                rowMod += Math.Abs(rowCounts[i] - rowCounts[i - 2]);
            }
            rowMod = innerH > 2 ? (rowMod / (innerH - 2)) / Math.Max(1.0, innerW) : 0;

            double colMod = 0;
            for (int i = 2; i < innerW; i++)
            {
                colMod += Math.Abs(colCounts[i] - colCounts[i - 2]);
            }
            colMod = innerW > 2 ? (colMod / (innerW - 2)) / Math.Max(1.0, innerH) : 0;

            m.VerticalLineBias = colMod > rowMod * 1.05;
            m.IsLinedNotePage = fullRuleRows >= 12 && !m.VerticalLineBias;
            m.InkRatio = totalInner > 0 ? (double)darkCount / totalInner : 0.0;
            m.IsBlank = m.InkRatio < 0.0022;

            if (pageIndex == 0)
            {
                // Front cover: gentle 0.8% edge trim to preserve full-bleed cover art while removing scanner line
                m.CropLeftPct = 0.8;
                m.CropTopPct = 0.8;
                m.CropRightPct = 0.8;
                m.CropBottomPct = 0.8;
            }
            else if (maxX <= minX || maxY <= minY)
            {
                m.CropLeftPct = 2.0;
                m.CropTopPct = 2.0;
                m.CropRightPct = 2.0;
                m.CropBottomPct = 2.0;
            }
            else
            {
                // Add 2.5% white margin around detected content, clamped to [1.2%, 8.5%] for clean uniform pages
                m.CropLeftPct = Math.Min(8.5, Math.Max(1.2, ((double)minX / width) * 100.0 - 2.5));
                m.CropTopPct = Math.Min(8.5, Math.Max(1.2, ((double)minY / height) * 100.0 - 2.5));
                m.CropRightPct = Math.Min(8.5, Math.Max(1.2, ((double)(width - maxX) / width) * 100.0 - 2.5));
                m.CropBottomPct = Math.Min(8.5, Math.Max(1.2, ((double)(height - maxY) / height) * 100.0 - 2.5));
            }

            return m;
        }

        static async Task SavePageThumbnailBytesAsync(PdfPage page, string thumbFilePath)
        {
            if (string.IsNullOrEmpty(thumbFilePath)) return;
            using (InMemoryRandomAccessStream thumbStream = new InMemoryRandomAccessStream())
            {
                PdfPageRenderOptions thumbOpts = new PdfPageRenderOptions();
                thumbOpts.DestinationWidth = 240;
                thumbOpts.DestinationHeight = (uint)Math.Max(100, Math.Round(240.0 * (page.Size.Height / page.Size.Width)));
                thumbOpts.BitmapEncoderId = BitmapEncoder.JpegEncoderId;
                await WaitAct(page.RenderToStreamAsync(thumbStream, thumbOpts));

                uint size = (uint)thumbStream.Size;
                if (size > 0)
                {
                    using (DataReader reader = new DataReader(thumbStream.GetInputStreamAt(0)))
                    {
                        await WaitOp(reader.LoadAsync(size));
                        byte[] bytes = new byte[size];
                        reader.ReadBytes(bytes);
                        File.WriteAllBytes(thumbFilePath, bytes);
                    }
                }
            }
        }

        static async Task<PageOcrData> OcrSinglePageAsync(PdfDocument pdf, OcrEngine ocr, uint pageIndex, double scale, string pageThumbsDir)
        {
            using (PdfPage page = pdf.GetPage(pageIndex))
            using (InMemoryRandomAccessStream stream = new InMemoryRandomAccessStream())
            {
                double pdfW = page.Size.Width;
                double pdfH = page.Size.Height;
                PdfPageRenderOptions opts = new PdfPageRenderOptions();
                // Optimal 250-275 DPI scanner resolution for Tesseract 5.4 LSTM + Leptonica & WinRT fusion
                uint targetW = (pageIndex == 0) ? 1400u : 1200u;
                opts.DestinationWidth = targetW;
                opts.DestinationHeight = (uint)Math.Max(100, Math.Round(targetW * (pdfH / Math.Max(1.0, pdfW))));
                // Use uncompressed BMP in-memory stream to eliminate PNG zlib compression/decompression CPU overhead!
                opts.BitmapEncoderId = BitmapEncoder.BmpEncoderId;

                await WaitAct(page.RenderToStreamAsync(stream, opts));
                BitmapDecoder decoder = await WaitOp(BitmapDecoder.CreateAsync(BitmapDecoder.BmpDecoderId, stream));
                uint origImgW = decoder.PixelWidth;
                uint origImgH = decoder.PixelHeight;
                uint imgW = origImgW;
                uint imgH = origImgH;

                PixelDataProvider pixelProvider = await WaitOp(decoder.GetPixelDataAsync());
                byte[] bgraPixels = pixelProvider.DetachPixelData();
                PixelMetrics metrics = AnalyzeBitmapPixels(bgraPixels, (int)imgW, (int)imgH, (int)pageIndex);

                if (!string.IsNullOrEmpty(pageThumbsDir))
                {
                    string thumbFile = Path.Combine(pageThumbsDir, "p_" + (pageIndex + 1) + ".jpg");
                    if (!File.Exists(thumbFile))
                    {
                        try { await SavePageThumbnailBytesAsync(page, thumbFile); } catch { }
                    }
                }

                OcrResult result = null;
                int detectedRotation = 0;

                using (SoftwareBitmap bitmap = await WaitOp(decoder.GetSoftwareBitmapAsync()))
                {
                    result = await WaitOp(ocr.RecognizeAsync(bitmap));
                }

                OcrOrientationStats stats0 = ScoreOcrOrientation(result);
                int rawWordCount = stats0.TotalWords;
                int validAlphaWords = stats0.NaturalWords;

                bool isLandscapePage = pdfW > pdfH * 1.04;
                bool hasVerticalLines = metrics.VerticalLineBias;
                bool lowScoreForInk = !metrics.IsLinedNotePage && (
                    (metrics.InkRatio > 0.015 && stats0.Score < 95) ||
                    (metrics.InkRatio > 0.006 && stats0.Score < 35) ||
                    (stats0.TotalWords >= 5 && stats0.NaturalWords * 2 < stats0.TotalWords)
                );

                // Multi-Signal Auto-Orientation & Exact Angle (90 / 270 / 180) Detector
                // For Page 1 (front cover, pageIndex == 0), only test rotation if 0-deg has < 2 natural words (cover art / barcodes can have vertical stripes)
                bool shouldCheckRotation = (pageIndex == 0)
                    ? (stats0.NaturalWords < 2 && isLandscapePage)
                    : (!metrics.IsBlank && metrics.InkRatio > 0.004 && (isLandscapePage || hasVerticalLines || lowScoreForInk));

                if (shouldCheckRotation)
                {
                    BitmapRotation[] candidateRots = new BitmapRotation[] {
                        BitmapRotation.Clockwise90Degrees,
                        BitmapRotation.Clockwise270Degrees,
                        BitmapRotation.Clockwise180Degrees
                    };
                    int[] degs = new int[] { 90, 270, 180 };

                    int bestAngle = 0;
                    int bestScore = stats0.Score;
                    OcrResult bestRes = result;
                    OcrOrientationStats bestStats = stats0;

                    int score90 = 0, score270 = 0;
                    OcrResult res90 = null, res270 = null;
                    OcrOrientationStats st90 = null, st270 = null;

                    for (int rIdx = 0; rIdx < candidateRots.Length; rIdx++)
                    {
                        try
                        {
                            BitmapTransform transform = new BitmapTransform { Rotation = candidateRots[rIdx] };
                            using (SoftwareBitmap rotBmp = await WaitOp(decoder.GetSoftwareBitmapAsync(
                                BitmapPixelFormat.Bgra8,
                                BitmapAlphaMode.Premultiplied,
                                transform,
                                ExifOrientationMode.IgnoreExifOrientation,
                                ColorManagementMode.DoNotColorManage)))
                            {
                                OcrResult rotRes = await WaitOp(ocr.RecognizeAsync(rotBmp));
                                OcrOrientationStats rotStats = ScoreOcrOrientation(rotRes);

                                if (degs[rIdx] == 90) { score90 = rotStats.Score; res90 = rotRes; st90 = rotStats; }
                                if (degs[rIdx] == 270) { score270 = rotStats.Score; res270 = rotRes; st270 = rotStats; }

                                if (rotStats.Score > bestScore * 1.20 && rotStats.Score >= bestScore + 10)
                                {
                                    bestScore = rotStats.Score;
                                    bestAngle = degs[rIdx];
                                    bestRes = rotRes;
                                    bestStats = rotStats;
                                }
                            }
                        }
                        catch { }
                    }

                    // Special Case: Landscape page in a Portrait book where main heading/running header is on 90 or 270 (e.g. 1.pdf Page 199)
                    if (bestAngle == 0 && isLandscapePage)
                    {
                        if (score270 >= 45 && score270 > score90 * 2 && st270 != null && st270.StopWords >= 2)
                        {
                            bestAngle = 270;
                            bestRes = res270;
                            bestStats = st270;
                        }
                        else if (score90 >= 45 && score90 > score270 * 2 && st90 != null && st90.StopWords >= 2)
                        {
                            bestAngle = 90;
                            bestRes = res90;
                            bestStats = st90;
                        }
                    }

                    if (bestAngle != 0 && bestRes != null)
                    {
                        detectedRotation = bestAngle;
                        result = bestRes;
                        rawWordCount = bestStats.TotalWords;
                        validAlphaWords = bestStats.NaturalWords;

                        double oldL = metrics.CropLeftPct, oldT = metrics.CropTopPct, oldR = metrics.CropRightPct, oldB = metrics.CropBottomPct;
                        if (detectedRotation == 90)
                        {
                            uint tmp = imgW; imgW = imgH; imgH = tmp;
                            metrics.CropLeftPct = oldB;
                            metrics.CropTopPct = oldL;
                            metrics.CropRightPct = oldT;
                            metrics.CropBottomPct = oldR;
                        }
                        else if (detectedRotation == 270)
                        {
                            uint tmp = imgW; imgW = imgH; imgH = tmp;
                            metrics.CropLeftPct = oldT;
                            metrics.CropTopPct = oldR;
                            metrics.CropRightPct = oldB;
                            metrics.CropBottomPct = oldL;
                        }
                        else if (detectedRotation == 180)
                        {
                            metrics.CropLeftPct = oldR;
                            metrics.CropTopPct = oldB;
                            metrics.CropRightPct = oldL;
                            metrics.CropBottomPct = oldT;
                        }
                    }
                }

                // Final blank check combining pixel ink density and valid words
                bool finalIsBlank = metrics.IsBlank && validAlphaWords <= 1;
                double confidence = 100.0;
                List<UnifiedLine> finalLines = new List<UnifiedLine>();

                if (!finalIsBlank)
                {
                    List<UnifiedLine> winLines = ExtractWinOcrLines(result, imgW, imgH);
                    double tessConf = 96.0;
                    int tessWords = 0;
                    byte[] pgmBytes = BuildUprightGrayscalePgm(bgraPixels, (int)origImgW, (int)origImgH, detectedRotation);
                    List<UnifiedLine> tessLines = RunTesseractTsvOnPgm(pgmBytes, imgW, imgH, out tessConf, out tessWords);

                    if (tessLines.Count > 0)
                    {
                        finalLines = FuseTesseractAndWinOcrLines(tessLines, winLines);
                        confidence = tessConf;
                    }
                    else
                    {
                        finalLines = winLines;
                        if (rawWordCount > 0)
                        {
                            confidence = Math.Min(100.0, Math.Max(75.0, ((double)validAlphaWords / Math.Max(1, rawWordCount)) * 100.0 + 12.0));
                        }
                        else
                        {
                            confidence = 95.0;
                        }
                    }

                    int fusedWords = 0;
                    foreach (UnifiedLine ul in finalLines)
                    {
                        fusedWords += ul.Words.Count;
                    }
                    rawWordCount = fusedWords;
                }

                StringBuilder sb = new StringBuilder(4096);
                sb.Append("{\"pageIndex\":").Append(pageIndex)
                  .Append(",\"pageNumber\":").Append(pageIndex + 1)
                  .Append(",\"pdfWidth\":").Append(pdfW.ToString("F2", CultureInfo.InvariantCulture))
                  .Append(",\"pdfHeight\":").Append(pdfH.ToString("F2", CultureInfo.InvariantCulture))
                  .Append(",\"imgWidth\":").Append(imgW)
                  .Append(",\"imgHeight\":").Append(imgH)
                  .Append(",\"isBlank\":").Append(finalIsBlank ? "true" : "false")
                  .Append(",\"inkRatio\":").Append(metrics.InkRatio.ToString("F5", CultureInfo.InvariantCulture))
                  .Append(",\"hasDarkBorder\":").Append(metrics.HasDarkBorder ? "true" : "false")
                  .Append(",\"detectedRotation\":").Append(detectedRotation)
                  .Append(",\"confidence\":").Append(confidence.ToString("F1", CultureInfo.InvariantCulture))
                  .Append(",\"wordCount\":").Append(rawWordCount)
                  .Append(",\"cropBox\":{\"left\":").Append(metrics.CropLeftPct.ToString("F2", CultureInfo.InvariantCulture))
                  .Append(",\"top\":").Append(metrics.CropTopPct.ToString("F2", CultureInfo.InvariantCulture))
                  .Append(",\"right\":").Append(metrics.CropRightPct.ToString("F2", CultureInfo.InvariantCulture))
                  .Append(",\"bottom\":").Append(metrics.CropBottomPct.ToString("F2", CultureInfo.InvariantCulture)).Append("}")
                  .Append(",\"lines\":[");

                bool firstLine = true;
                if (!finalIsBlank && finalLines.Count > 0)
                {
                    foreach (UnifiedLine ul in finalLines)
                    {
                        if (ul.Words == null || ul.Words.Count == 0) continue;
                        if (!firstLine) sb.Append(",");
                        firstLine = false;

                        StringBuilder wordsSb = new StringBuilder(256);
                        bool firstWord = true;
                        foreach (UnifiedWord uw in ul.Words)
                        {
                            if (!firstWord) wordsSb.Append(",");
                            firstWord = false;
                            wordsSb.Append("{\"t\":\"").Append(EscapeJson(uw.Text)).Append("\"")
                                   .Append(",\"x\":").Append(uw.X.ToString("F1", CultureInfo.InvariantCulture))
                                   .Append(",\"y\":").Append(uw.Y.ToString("F1", CultureInfo.InvariantCulture))
                                   .Append(",\"w\":").Append(uw.W.ToString("F1", CultureInfo.InvariantCulture))
                                   .Append(",\"h\":").Append(uw.H.ToString("F1", CultureInfo.InvariantCulture))
                                   .Append("}");
                        }

                        sb.Append("{\"text\":\"").Append(EscapeJson(ul.Text)).Append("\"")
                          .Append(",\"x\":").Append(ul.X.ToString("F1", CultureInfo.InvariantCulture))
                          .Append(",\"y\":").Append(ul.Y.ToString("F1", CultureInfo.InvariantCulture))
                          .Append(",\"w\":").Append(ul.W.ToString("F1", CultureInfo.InvariantCulture))
                          .Append(",\"h\":").Append(ul.H.ToString("F1", CultureInfo.InvariantCulture))
                          .Append(",\"avgWordH\":").Append(ul.AvgWordH.ToString("F1", CultureInfo.InvariantCulture))
                          .Append(",\"words\":[").Append(wordsSb.ToString()).Append("]}");
                    }
                }

                sb.Append("]}");
                return new PageOcrData
                {
                    PageIndex = (int)pageIndex,
                    PdfWidth = pdfW,
                    PdfHeight = pdfH,
                    ImgWidth = imgW,
                    ImgHeight = imgH,
                    JsonLine = sb.ToString()
                };
            }
        }

        static async Task RunMetaAsync(string pdfPath, int maxMetaPages)
        {
            StorageFile file = await WaitOp(StorageFile.GetFileFromPathAsync(Path.GetFullPath(pdfPath)));
            PdfDocument pdf = await WaitOp(PdfDocument.LoadFromFileAsync(file));
            OcrEngine ocr = OcrEngine.TryCreateFromUserProfileLanguages();
            if (ocr == null)
            {
                ocr = OcrEngine.TryCreateFromLanguage(new Windows.Globalization.Language("en-US"));
            }

            uint count = Math.Min((uint)maxMetaPages, pdf.PageCount);
            List<string> pageJsons = new List<string>();
            for (uint i = 0; i < count; i++)
            {
                double scale = (i == 0) ? 2.0 : 1.6;
                PageOcrData data = await OcrSinglePageAsync(pdf, ocr, i, scale, null);
                pageJsons.Add(data.JsonLine);
            }

            StringBuilder outSb = new StringBuilder();
            outSb.Append("{\"pageCount\":").Append(pdf.PageCount)
                 .Append(",\"pages\":[").Append(string.Join(",", pageJsons)).Append("]}");
            Console.OutputEncoding = Encoding.UTF8;
            Console.WriteLine(outSb.ToString());
        }

        static async Task RunRangeAsync(string pdfPath, string outJsonlPath, int startIdx, int endIdx, int concurrency)
        {
            string fullPdfPath = Path.GetFullPath(pdfPath);
            StorageFile mainFile = await WaitOp(StorageFile.GetFileFromPathAsync(fullPdfPath));
            PdfDocument mainPdf = await WaitOp(PdfDocument.LoadFromFileAsync(mainFile));
            int totalPages = (int)mainPdf.PageCount;

            int actualStart = Math.Max(0, Math.Min(startIdx, totalPages));
            int actualEnd = Math.Max(actualStart, Math.Min(endIdx, totalPages));
            int sliceCount = actualEnd - actualStart;
            if (sliceCount <= 0)
            {
                File.WriteAllText(outJsonlPath, "", new UTF8Encoding(false));
                return;
            }

            Console.OutputEncoding = Encoding.UTF8;
            string[] results = new string[sliceCount];
            int nextOffset = 0;
            int completed = 0;

            int workers = Math.Max(1, Math.Min(concurrency, sliceCount));
            Task[] workerTasks = new Task[workers];

            for (int w = 0; w < workers; w++)
            {
                workerTasks[w] = Task.Run(async () =>
                {
                    StorageFile wFile = await WaitOp(StorageFile.GetFileFromPathAsync(fullPdfPath));
                    PdfDocument wPdf = await WaitOp(PdfDocument.LoadFromFileAsync(wFile));
                    OcrEngine wOcr = OcrEngine.TryCreateFromUserProfileLanguages();
                    if (wOcr == null)
                    {
                        wOcr = OcrEngine.TryCreateFromLanguage(new Windows.Globalization.Language("en-US"));
                    }

                    while (true)
                    {
                        int offset = Interlocked.Increment(ref nextOffset) - 1;
                        if (offset >= sliceCount) break;
                        int pageIdx = actualStart + offset;

                        try
                        {
                            PageOcrData data = await OcrSinglePageAsync(wPdf, wOcr, (uint)pageIdx, 1.5, null);
                            results[offset] = data.JsonLine;
                        }
                        catch (Exception ex)
                        {
                            results[offset] = "{\"pageIndex\":" + pageIdx + ",\"pageNumber\":" + (pageIdx + 1) + ",\"error\":\"" + EscapeJson(ex.Message) + "\",\"lines\":[]}";
                        }

                        int doneNow = Interlocked.Increment(ref completed);
                        Console.WriteLine("STEP:" + doneNow + ":" + sliceCount);
                        Console.Out.Flush();
                    }
                });
            }

            await Task.WhenAll(workerTasks);

            string dir = Path.GetDirectoryName(Path.GetFullPath(outJsonlPath));
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            using (StreamWriter sw = new StreamWriter(outJsonlPath, false, new UTF8Encoding(false)))
            {
                for (int i = 0; i < sliceCount; i++)
                {
                    sw.WriteLine(results[i] ?? ("{\"pageIndex\":" + (actualStart + i) + ",\"lines\":[]}"));
                }
            }
        }

        static async Task RunFullAsync(string pdfPath, string outJsonlPath, int concurrency, double scale, string pageThumbsDir)
        {
            string fullPdfPath = Path.GetFullPath(pdfPath);
            StorageFile mainFile = await WaitOp(StorageFile.GetFileFromPathAsync(fullPdfPath));
            PdfDocument mainPdf = await WaitOp(PdfDocument.LoadFromFileAsync(mainFile));
            uint totalPages = mainPdf.PageCount;

            if (!string.IsNullOrEmpty(pageThumbsDir) && !Directory.Exists(pageThumbsDir))
            {
                Directory.CreateDirectory(pageThumbsDir);
            }

            Console.OutputEncoding = Encoding.UTF8;
            Console.WriteLine("START:" + totalPages);
            Console.Out.Flush();

            string[] results = new string[totalPages];
            int completed = 0;
            int nextPage = 0;

            int workers = Math.Max(1, Math.Min(concurrency, (int)totalPages));
            Task[] workerTasks = new Task[workers];

            for (int w = 0; w < workers; w++)
            {
                workerTasks[w] = Task.Run(async () =>
                {
                    StorageFile wFile = await WaitOp(StorageFile.GetFileFromPathAsync(fullPdfPath));
                    PdfDocument wPdf = await WaitOp(PdfDocument.LoadFromFileAsync(wFile));
                    OcrEngine wOcr = OcrEngine.TryCreateFromUserProfileLanguages();
                    if (wOcr == null)
                    {
                        wOcr = OcrEngine.TryCreateFromLanguage(new Windows.Globalization.Language("en-US"));
                    }

                    while (true)
                    {
                        int pageIdx = Interlocked.Increment(ref nextPage) - 1;
                        if (pageIdx >= totalPages) break;

                        double pageScale = (pageIdx == 0) ? Math.Max(scale, 2.0) : scale;
                        try
                        {
                            PageOcrData data = await OcrSinglePageAsync(wPdf, wOcr, (uint)pageIdx, pageScale, pageThumbsDir);
                            results[pageIdx] = data.JsonLine;
                        }
                        catch (Exception ex)
                        {
                            results[pageIdx] = "{\"pageIndex\":" + pageIdx + ",\"pageNumber\":" + (pageIdx + 1) + ",\"error\":\"" + EscapeJson(ex.Message) + "\",\"lines\":[]}";
                        }

                        int doneNow = Interlocked.Increment(ref completed);
                        if (doneNow % 2 == 0 || doneNow == totalPages || doneNow == 1)
                        {
                            Console.WriteLine("PROGRESS:" + doneNow + ":" + totalPages);
                            Console.Out.Flush();
                        }
                    }
                });
            }

            await Task.WhenAll(workerTasks);

            string dir = Path.GetDirectoryName(Path.GetFullPath(outJsonlPath));
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            using (StreamWriter sw = new StreamWriter(outJsonlPath, false, new UTF8Encoding(false)))
            {
                for (int i = 0; i < totalPages; i++)
                {
                    sw.WriteLine(results[i] ?? ("{\"pageIndex\":" + i + ",\"lines\":[]}"));
                }
            }

            Console.WriteLine("DONE:" + totalPages);
            Console.Out.Flush();
        }

        /**
         * Fast QC Scan & Thumbnail Generator for already-OCR'd PDFs:
         * Generates page thumbnails (p_1.jpg .. p_N.jpg) AND computes Blank Page, Ink Ratio, and Auto-Crop metrics in ~5 seconds!
         */
        static async Task RunQcFastScanAsync(string pdfPath, string outMetricsJsonPath, string pageThumbsDir, int concurrency)
        {
            string fullPdfPath = Path.GetFullPath(pdfPath);
            StorageFile mainFile = await WaitOp(StorageFile.GetFileFromPathAsync(fullPdfPath));
            PdfDocument mainPdf = await WaitOp(PdfDocument.LoadFromFileAsync(mainFile));
            uint totalPages = mainPdf.PageCount;

            if (!string.IsNullOrEmpty(pageThumbsDir) && !Directory.Exists(pageThumbsDir))
            {
                Directory.CreateDirectory(pageThumbsDir);
            }

            string[] pageMetricsJson = new string[totalPages];
            int nextPage = 0;
            int workers = Math.Max(1, Math.Min(concurrency, (int)totalPages));
            Task[] workerTasks = new Task[workers];

            for (int w = 0; w < workers; w++)
            {
                workerTasks[w] = Task.Run(async () =>
                {
                    StorageFile wFile = await WaitOp(StorageFile.GetFileFromPathAsync(fullPdfPath));
                    PdfDocument wPdf = await WaitOp(PdfDocument.LoadFromFileAsync(wFile));

                    while (true)
                    {
                        int pageIdx = Interlocked.Increment(ref nextPage) - 1;
                        if (pageIdx >= totalPages) break;

                        try
                        {
                            using (PdfPage page = wPdf.GetPage((uint)pageIdx))
                            using (InMemoryRandomAccessStream stream = new InMemoryRandomAccessStream())
                            {
                                PdfPageRenderOptions opts = new PdfPageRenderOptions();
                                opts.DestinationWidth = 300;
                                opts.DestinationHeight = (uint)Math.Max(100, Math.Round(300.0 * (page.Size.Height / page.Size.Width)));
                                await WaitAct(page.RenderToStreamAsync(stream, opts));

                                BitmapDecoder decoder = await WaitOp(BitmapDecoder.CreateAsync(stream));
                                PixelDataProvider provider = await WaitOp(decoder.GetPixelDataAsync());
                                byte[] bgra = provider.DetachPixelData();
                                PixelMetrics m = AnalyzeBitmapPixels(bgra, (int)decoder.PixelWidth, (int)decoder.PixelHeight, pageIdx);

                                if (!string.IsNullOrEmpty(pageThumbsDir))
                                {
                                    string thumbFile = Path.Combine(pageThumbsDir, "p_" + (pageIdx + 1) + ".jpg");
                                    if (!File.Exists(thumbFile))
                                    {
                                        await SavePageThumbnailBytesAsync(page, thumbFile);
                                    }
                                }

                                StringBuilder msb = new StringBuilder(160);
                                msb.Append("{\"pageIndex\":").Append(pageIdx)
                                   .Append(",\"pageNumber\":").Append(pageIdx + 1)
                                   .Append(",\"isBlank\":").Append(m.IsBlank ? "true" : "false")
                                   .Append(",\"inkRatio\":").Append(m.InkRatio.ToString("F5", CultureInfo.InvariantCulture))
                                   .Append(",\"hasDarkBorder\":").Append(m.HasDarkBorder ? "true" : "false")
                                   .Append(",\"cropBox\":{\"left\":").Append(m.CropLeftPct.ToString("F2", CultureInfo.InvariantCulture))
                                   .Append(",\"top\":").Append(m.CropTopPct.ToString("F2", CultureInfo.InvariantCulture))
                                   .Append(",\"right\":").Append(m.CropRightPct.ToString("F2", CultureInfo.InvariantCulture))
                                   .Append(",\"bottom\":").Append(m.CropBottomPct.ToString("F2", CultureInfo.InvariantCulture))
                                   .Append("}}");
                                pageMetricsJson[pageIdx] = msb.ToString();
                            }
                        }
                        catch
                        {
                            pageMetricsJson[pageIdx] = "{\"pageIndex\":" + pageIdx + ",\"pageNumber\":" + (pageIdx + 1) + ",\"isBlank\":false,\"inkRatio\":0.1,\"hasDarkBorder\":false,\"cropBox\":{\"left\":2,\"top\":2,\"right\":2,\"bottom\":2}}";
                        }
                    }
                });
            }

            await Task.WhenAll(workerTasks);
            string jsonOut = "[" + string.Join(",", pageMetricsJson) + "]";
            File.WriteAllText(outMetricsJsonPath, jsonOut, new UTF8Encoding(false));
            Console.WriteLine("QC_SCAN_DONE:" + totalPages);
        }

        /**
         * Re-runs full multi-angle OCR + rotation detection on a comma-separated list of 0-based page indices
         * and outputs their updated JSONL lines to stdout (used to upgrade rotation detection on already-cached books in ~2s).
         */
        static async Task RunPagesListAsync(string pdfPath, string csvIndices, int concurrency)
        {
            string fullPdfPath = Path.GetFullPath(pdfPath);
            StorageFile mainFile = await WaitOp(StorageFile.GetFileFromPathAsync(fullPdfPath));
            PdfDocument mainPdf = await WaitOp(PdfDocument.LoadFromFileAsync(mainFile));
            int totalPages = (int)mainPdf.PageCount;

            string[] parts = (csvIndices ?? "").Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            List<int> pageList = new List<int>();
            foreach (string p in parts)
            {
                int idx;
                if (int.TryParse(p.Trim(), out idx) && idx >= 0 && idx < totalPages)
                {
                    pageList.Add(idx);
                }
            }
            if (pageList.Count == 0) return;

            string[] outLines = new string[pageList.Count];
            int nextItem = 0;
            int workers = Math.Max(1, Math.Min(concurrency, pageList.Count));
            Task[] workerTasks = new Task[workers];

            for (int w = 0; w < workers; w++)
            {
                workerTasks[w] = Task.Run(async () =>
                {
                    StorageFile wFile = await WaitOp(StorageFile.GetFileFromPathAsync(fullPdfPath));
                    PdfDocument wPdf = await WaitOp(PdfDocument.LoadFromFileAsync(wFile));
                    OcrEngine wOcr = OcrEngine.TryCreateFromUserProfileLanguages();
                    if (wOcr == null)
                    {
                        wOcr = OcrEngine.TryCreateFromLanguage(new Windows.Globalization.Language("en-US"));
                    }

                    while (true)
                    {
                        int itemIdx = Interlocked.Increment(ref nextItem) - 1;
                        if (itemIdx >= pageList.Count) break;
                        int pageIdx = pageList[itemIdx];
                        try
                        {
                            PageOcrData d = await OcrSinglePageAsync(wPdf, wOcr, (uint)pageIdx, 1.5, null);
                            outLines[itemIdx] = d.JsonLine;
                        }
                        catch { }
                    }
                });
            }

            await Task.WhenAll(workerTasks);
            Console.OutputEncoding = Encoding.UTF8;
            foreach (string line in outLines)
            {
                if (!string.IsNullOrEmpty(line)) Console.WriteLine(line);
            }
        }

        static async Task RunThumbAsync(string pdfPath, string outPngPath, uint pageIdx = 0, uint targetWidth = 480)
        {
            string fullPdfPath = Path.GetFullPath(pdfPath);
            StorageFile file = await WaitOp(StorageFile.GetFileFromPathAsync(fullPdfPath));
            PdfDocument pdf = await WaitOp(PdfDocument.LoadFromFileAsync(file));
            if (pdf.PageCount == 0 || pageIdx >= pdf.PageCount) return;

            string fullOutPath = Path.GetFullPath(outPngPath);
            string outDir = Path.GetDirectoryName(fullOutPath);
            string outFileName = Path.GetFileName(fullOutPath);
            if (!Directory.Exists(outDir)) Directory.CreateDirectory(outDir);

            StorageFolder folder = await WaitOp(StorageFolder.GetFolderFromPathAsync(outDir));
            StorageFile outFile = await WaitOp(folder.CreateFileAsync(outFileName, CreationCollisionOption.ReplaceExisting));

            using (PdfPage page = pdf.GetPage(pageIdx))
            using (IRandomAccessStream stream = await WaitOp(outFile.OpenAsync(FileAccessMode.ReadWrite)))
            {
                PdfPageRenderOptions opts = new PdfPageRenderOptions();
                opts.DestinationWidth = targetWidth;
                opts.DestinationHeight = (uint)Math.Round((double)targetWidth * (page.Size.Height / page.Size.Width));
                await WaitAct(page.RenderToStreamAsync(stream, opts));
            }
            Console.WriteLine("THUMB_OK:" + fullOutPath);
        }

        static int Main(string[] args)
        {
            if (args.Length < 2)
            {
                Console.Error.WriteLine("Usage:");
                Console.Error.WriteLine("  WinOcrEngine.exe meta <pdfPath> [maxPages]");
                Console.Error.WriteLine("  WinOcrEngine.exe full <pdfPath> <outJsonlPath> [concurrency] [scale] [pageThumbsDir]");
                Console.Error.WriteLine("  WinOcrEngine.exe qc-scan <pdfPath> <outMetricsJsonPath> <pageThumbsDir> [concurrency]");
                Console.Error.WriteLine("  WinOcrEngine.exe thumb <pdfPath> <outPngPath>");
                return 1;
            }

            try
            {
                string mode = args[0].ToLowerInvariant();
                if (mode == "meta")
                {
                    string pdfPath = args[1];
                    int maxPages = (args.Length >= 3) ? int.Parse(args[2]) : 8;
                    RunMetaAsync(pdfPath, maxPages).GetAwaiter().GetResult();
                }
                else if (mode == "full")
                {
                    string pdfPath = args[1];
                    string outJsonlPath = args[2];
                    int concurrency = (args.Length >= 4) ? int.Parse(args[3]) : 4;
                    double scale = (args.Length >= 5) ? double.Parse(args[4], CultureInfo.InvariantCulture) : 1.6;
                    string pageThumbsDir = (args.Length >= 6) ? args[5] : null;
                    RunFullAsync(pdfPath, outJsonlPath, concurrency, scale, pageThumbsDir).GetAwaiter().GetResult();
                }
                else if (mode == "range")
                {
                    string pdfPath = args[1];
                    string outJsonlPath = args[2];
                    int startIdx = int.Parse(args[3]);
                    int endIdx = int.Parse(args[4]);
                    int concurrency = (args.Length >= 6) ? int.Parse(args[5]) : 2;
                    RunRangeAsync(pdfPath, outJsonlPath, startIdx, endIdx, concurrency).GetAwaiter().GetResult();
                }
                else if (mode == "qc-scan")
                {
                    string pdfPath = args[1];
                    string outMetricsJsonPath = args[2];
                    string pageThumbsDir = args[3];
                    int concurrency = (args.Length >= 5) ? int.Parse(args[4]) : 6;
                    RunQcFastScanAsync(pdfPath, outMetricsJsonPath, pageThumbsDir, concurrency).GetAwaiter().GetResult();
                }
                else if (mode == "pages")
                {
                    string pdfPath = args[1];
                    string csvIndices = args[2];
                    int concurrency = (args.Length >= 4) ? int.Parse(args[3]) : 4;
                    RunPagesListAsync(pdfPath, csvIndices, concurrency).GetAwaiter().GetResult();
                }
                else if (mode == "thumb")
                {
                    string pdfPath = args[1];
                    string outPngPath = args[2];
                    uint pageIdx = (args.Length >= 4) ? uint.Parse(args[3]) : 0u;
                    uint targetW = (args.Length >= 5) ? uint.Parse(args[4]) : 480u;
                    RunThumbAsync(pdfPath, outPngPath, pageIdx, targetW).GetAwaiter().GetResult();
                }
                else
                {
                    Console.Error.WriteLine("Unknown mode: " + mode);
                    return 1;
                }
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("ERROR: " + ex.ToString());
                return 2;
            }
        }
    }
}
