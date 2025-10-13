using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Rennamer
{
    internal static class FilenameParser
    {
public static string[,] ParseFilename(string filename, bool view_changes = false)
{
    string extension = Path.GetExtension(filename);
    string nameWithoutExt = Path.GetFileNameWithoutExtension(filename);

    var parts = new List<(string, string)>();
    int i = 0;

    while (i < nameWithoutExt.Length)
    {
        if (char.IsDigit(nameWithoutExt[i]))
        {
            int start = i;
            while (i < nameWithoutExt.Length && char.IsDigit(nameWithoutExt[i])) i++;
            string numberBlock = nameWithoutExt.Substring(start, i - start);

            int zeroCount = 0;
            while (zeroCount < numberBlock.Length && numberBlock[zeroCount] == '0') zeroCount++;

            if (zeroCount > 0) parts.Add((new string('0', zeroCount), "0 vor Nummer"));
            if (zeroCount < numberBlock.Length) parts.Add((numberBlock.Substring(zeroCount), "num"));
        }
        else
        {
            int start = i;
            while (i < nameWithoutExt.Length && !char.IsDigit(nameWithoutExt[i])) i++;
            string textBlock = nameWithoutExt.Substring(start, i - start);
            parts.Add((textBlock, "str"));
        }
    }

    // ✅ only add when extension is present
    if (!string.IsNullOrEmpty(extension))
        parts.Add((extension, "format"));

    var result = new string[parts.Count, 2];
    for (int j = 0; j < parts.Count; j++)
    {
        result[j, 0] = parts[j].Item1;
        result[j, 1] = parts[j].Item2;
    }

    if (view_changes)
    {
        for (int iter = 0; iter < result.GetLength(0); iter++)
            Console.WriteLine($"    {result[iter, 0]} => {result[iter, 1]}");
    }
    return result;
}

        public static (int Code, string SuggestedBaseName, string[] Log) Find_difference(
            string fileBaseName,
            string prefix,
            string prefixReplacement)
        {
            var log = new List<string>();

            // File: parse & normalize to ["str"/"num"/"format"]
            var fileTok = NormalizeTokens(ParseFilename(fileBaseName, false));

            // Pattern: tokenize with '*' support ("any"), numbers as a single "num"
            var pattTok = TokenizePattern(prefix ?? string.Empty);

            // Implicit leading '*' if pattern starts with a number and no format is mentioned
            bool patternMentionsFormat = (prefix ?? string.Empty).Contains(".");
            bool startsWithNumber = pattTok.Count > 0 && pattTok[0].Type == "num";
            if (!patternMentionsFormat && startsWithNumber)
            {
                pattTok.Insert(0, ("*", "any"));
            }

            // ✅ Fast path: plain string prefix (no '*', no extension) acts as "StartsWith"
            bool pattHasStar = pattTok.Any(t => t.Type == "any");
            bool pattHasFormat = pattTok.Any(t => t.Type == "format");
            if (!string.IsNullOrEmpty(prefix) && !pattHasStar && !pattHasFormat)
            {
                if (fileBaseName.StartsWith(prefix, StringComparison.Ordinal))
                {
                    string suggested = PatternAwareReplace(fileBaseName, prefix ?? string.Empty, prefixReplacement ?? string.Empty, log);
                    return (0, suggested, log.ToArray());
                }
            }

            if (pattTok.Count == 0)
            {
                log.Add("Hinweis: Kein Präfix angegeben – keine Einschränkung beim Vergleich.");
                string suggested = DeltaAwareReplace(fileBaseName, prefix ?? string.Empty, prefixReplacement ?? string.Empty, log);
                return (0, suggested, log.ToArray());
            }

            // Wildcard-aware comparison (two-pointer with backtracking on last '*')
            int iFile = 0, iPatt = 0;
            int lastStarPatt = -1, lastStarFile = -1;
            bool anyNumericDiff = false;
            bool anyOtherDiff = false;

            bool MatchToken((string v, string t) F, (string v, string t) P, out bool numericDiff)
            {
                numericDiff = false;
                if (P.t == "any") return true;
                if (F.t != P.t) return false;
                if (F.v == P.v) return true;
                if (P.t == "num") { numericDiff = true; return true; }
                return false;
            }

            while (iFile < fileTok.Count)
            {
                if (iPatt < pattTok.Count && pattTok[iPatt].Type == "any")
                {
                    lastStarPatt = iPatt++;
                    lastStarFile = iFile;
                    continue;
                }

                bool numericDiffThis = false;
                if (iPatt < pattTok.Count && MatchToken(fileTok[iFile], pattTok[iPatt], out numericDiffThis))
                {
                    anyNumericDiff |= numericDiffThis;
                    iFile++; iPatt++;
                    continue;
                }

                if (lastStarPatt != -1)
                {
                    iPatt = lastStarPatt + 1;
                    iFile = ++lastStarFile;
                    continue;
                }

                anyOtherDiff = true;
                break;
            }

            while (iPatt < pattTok.Count && pattTok[iPatt].Type == "any") iPatt++;
            if (iPatt < pattTok.Count) anyOtherDiff = true;

            int code = anyOtherDiff ? 2 : (anyNumericDiff ? 1 : 0);

            // Suggest new base name (delta-aware). If blocked, keep original.
            string suggestedBase = (code == 2)
                ? fileBaseName
                : PatternAwareReplace(fileBaseName, prefix ?? string.Empty, prefixReplacement ?? string.Empty, log);

            log.Add("— Vergleich mit Wildcard —");
            log.Add("Pattern-Tokens: " + string.Join(" | ", pattTok.Select(t => $"{t.Value}:{t.Type}")));
            log.Add("File-Tokens:    " + string.Join(" | ", fileTok.Select(t => $"{t.Value}:{t.Type}")));
            if (code == 2)
                log.Add("Ergebnis: Änderung blockiert (Nicht-numerische/strukturelle Abweichung außerhalb von '*').");
            else if (code == 1)
                log.Add($"Ergebnis: Nur numerische Unterschiede (außerhalb von '*') → Vorschlag: '{fileBaseName}' → '{suggestedBase}'.");
            else
                log.Add($"Ergebnis: Keine Unterschiede (außerhalb von '*') → Vorschlag: '{fileBaseName}' → '{suggestedBase}'.");

            return (code, suggestedBase, log.ToArray());
        }

        // Tokenize a PATTERN (prefix) where '*' is a wildcard token named "any".
        // Numbers are single "num" tokens. Extension-like chunks (e.g., ".png")
        // are tagged as "format" so they match the file-side "format" tokens.
        private static List<(string Value, string Type)> TokenizePattern(string pattern)
        {
            var tokens = new List<(string, string)>();
            if (string.IsNullOrEmpty(pattern))
                return tokens;

            int i = 0;
            while (i < pattern.Length)
            {
                char c = pattern[i];

                if (c == '*')
                {
                    tokens.Add(("*", "any"));
                    i++;
                    continue;
                }

                if (char.IsDigit(c))
                {
                    int start = i;
                    while (i < pattern.Length && char.IsDigit(pattern[i])) i++;
                    string num = pattern.Substring(start, i - start);
                    tokens.Add((num, "num"));
                    continue;
                }

                // collect non-digit, non-asterisk text as one token
                int tStart = i;
                while (i < pattern.Length && pattern[i] != '*' && !char.IsDigit(pattern[i])) i++;
                // ... innerhalb von TokenizePattern, im Teil für "text"
                string text = pattern.Substring(tStart, i - tStart);
                if (text.Length > 0)
                {
                    bool looksLikeExt = text.Length > 1
                                     && text[0] == '.'
                                     && text.Skip(1).All(ch => char.IsLetterOrDigit(ch));

                    if (looksLikeExt)
                    {
                        tokens.Add((text, "format"));
                    }
                    else
                    {
                        // 👇 wie in NormalizeTokens: auf Trenner splitten und Trenner behalten
                        var pieces = Regex.Split(text, "([\\-_ ])");
                        foreach (var piece in pieces)
                        {
                            if (string.IsNullOrEmpty(piece)) continue;
                            tokens.Add((piece, "str"));
                        }
                    }
                }

            }

            return tokens;
        }

        // Pattern-aware replacer:
        // - prefix pattern may contain tokens: "str", "num", and "*" (wildcard -> "any")
        // - replacement pattern may also contain "str", "num", and "*" (in same order semantics)
        // - If both patterns contain at least one "num", we compute Δ from the FIRST numeric tokens
        //   and apply it to the FIRST captured number from the prefix match when building the replacement.
        // - "*" in replacement injects text captured by "*" in prefix (1st star -> 1st star capture, etc.)
        // - Keeps padding for shifted numbers (uses max(captured digits, replacement digits)).
        // Pattern-aware replacer:
        // - Handles '*' wildcard in prefix/replacement
        // - Keeps or shifts numbers intelligently
        // - Adds zero-padding based on replacement pattern
        // - If replacement has NO '*', the rest of filename is dropped
        //   and literal numbers are used from replacement instead of shifted ones
        private static string PatternAwareReplace(string baseName, string prefixPattern, string replacementPattern, List<string> log)
        {
            var preTok = TokenizePattern(prefixPattern);
            var repTok = TokenizePattern(replacementPattern);

            // Implizites führendes '*' falls Pattern mit Zahl startet und keine Extension enthält
            bool preMentionsFormat = preTok.Any(t => t.Type == "format");
            bool preStartsWithNumber = preTok.Count > 0 && preTok[0].Type == "num";
            if (!preMentionsFormat && preStartsWithNumber)
            {
                preTok.Insert(0, ("*", "any"));
            }

            // Kontext behalten, wenn im Replacement kein '*' steht
            bool preHasLeadingAny = preTok.Count > 0 && preTok[0].Type == "any";
            bool repHasAny = repTok.Any(t => t.Type == "any");
            if (preHasLeadingAny && !repHasAny)
            {
                repTok.Insert(0, ("*", "any"));
            }

            // Regex für Matching aufbauen
            var rgx = new StringBuilder("^");
            var starGroupIdx = new List<int>();
            var numGroupIdx = new List<int>();
            int groupCounter = 1;

            for (int k = 0; k < preTok.Count; k++)
            {
                var t = preTok[k];
                var next = (k + 1 < preTok.Count) ? preTok[k + 1] : (default, null);

                switch (t.Type)
                {
                    case "str":
                    case "format":
                        rgx.Append(Regex.Escape(t.Value));
                        break;

                    case "num":
                        rgx.Append("(\\d+)");
                        numGroupIdx.Add(groupCounter++);
                        break;

                    case "any":
                        if (next.Type == "num")
                            rgx.Append("(.*?)(?=\\d)");
                        else if (next.Type == "format")
                            rgx.Append("(.*?)(?=\\.)");
                        else
                            rgx.Append("(.*)");
                        starGroupIdx.Add(groupCounter++);
                        break;
                }
            }

            // Wenn Format enthalten ist → Ende verankern
            bool strictFull = preTok.Any(t => t.Type == "format");
            string pattern = rgx.ToString() + (strictFull ? "$" : "");
            var m = Regex.Match(baseName, pattern);

            // FAST-PATH: nur Extension ändern
            bool isPureExtChange =
                preTok.Count == 2 && preTok[0].Type == "any" && preTok[1].Type == "format" &&
                repTok.Count == 2 && repTok[0].Type == "any" && repTok[1].Type == "format";

            if (isPureExtChange)
            {
                string stem = (m.Groups.Count > 1 ? m.Groups[1].Value : null) ?? string.Empty;
                if (string.IsNullOrEmpty(stem))
                    stem = Path.GetFileNameWithoutExtension(baseName);
                string resultName = stem + repTok[1].Value;
                log?.Add($"Ext switch: '{baseName}' ⇒ '{resultName}'");
                return resultName;
            }

            if (!m.Success)
            {
                log?.Add("Pattern Replace: Präfix-Muster passt nicht am Anfang – keine Änderung.");
                return baseName;
            }

            // Detect star/format in replacement & Tail-Policy
            var repHasStar = repTok.Any(t => t.Type == "any");
            var repHasFormat = repTok.Any(t => t.Type == "format");

            bool dropTail = repHasFormat && !repHasStar;
            bool forceLiteralNumbers = dropTail;

            // ✅ Δ von der **letzten Zahl** berechnen
            long? delta = null;
            int preLastNumIndex = preTok.FindLastIndex(t => t.Type == "num");
            int repLastNumIndex = repTok.FindLastIndex(t => t.Type == "num");
            if (preLastNumIndex >= 0 && repLastNumIndex >= 0)
            {
                if (long.TryParse(preTok[preLastNumIndex].Value, out var preNum) &&
                    long.TryParse(repTok[repLastNumIndex].Value, out var repNum))
                {
                    delta = repNum - preNum;
                }
            }

            // ✅ Letzte Zahl aus Capture nehmen
            string? capturedNumStr = null;
            int capturedNumDigits = 0;
            if (numGroupIdx.Count > 0)
            {
                capturedNumStr = m.Groups[numGroupIdx.Last()].Value;
                capturedNumDigits = capturedNumStr.Length;
            }

            // Threshold-Check (optional)
            bool prefixStartsWithNumber = preTok.Count > 0 && preTok[0].Type == "num";
            bool prefixMentionsFormat2 = preTok.Any(t => t.Type == "format");
            if (!prefixMentionsFormat2 && prefixStartsWithNumber && preLastNumIndex >= 0 && capturedNumStr != null)
            {
                if (long.TryParse(capturedNumStr, out var capNum) &&
                    long.TryParse(preTok[preLastNumIndex].Value, out var patNum))
                {
                    if (capNum < patNum)
                    {
                        log?.Add($"Skip: captured number {capNum} < pattern number {patNum}.");
                        return baseName;
                    }
                }
            }

            // Neues Ergebnis zusammenbauen
            var newHead = new StringBuilder();
            int usedStars = 0;

            foreach (var t in repTok)
            {
                switch (t.Type)
                {
                    case "str":
                        newHead.Append(t.Value);
                        break;

                    case "any":
                        {
                            string cap = usedStars < starGroupIdx.Count ? m.Groups[starGroupIdx[usedStars++]].Value : "";
                            newHead.Append(cap);
                            break;
                        }

                    case "num":
                        {
                            int repDigits = t.Value?.Length ?? 0;
                            bool repHasLeadingZero = repDigits > 0 && t.Value![0] == '0';

                            if (!forceLiteralNumbers && delta.HasValue && capturedNumStr != null && long.TryParse(capturedNumStr, out var capNum))
                            {
                                long newNum = capNum + delta.Value;
                                int pad = repHasLeadingZero ? repDigits : capturedNumDigits;
                                string newNumStr = (newNum >= 0 && pad > 1)
                                    ? newNum.ToString(new string('0', pad))
                                    : newNum.ToString();
                                newHead.Append(newNumStr);
                            }
                            else
                            {
                                newHead.Append(t.Value ?? "");
                            }
                            break;
                        }

                    case "format":
                        newHead.Append(t.Value);
                        break;
                }
            }

            // Head ersetzen + Tail ggf. behalten
            string head = m.Value;
            string tail = baseName.Substring(head.Length);
            string newName = dropTail ? newHead.ToString() : (newHead.ToString() + tail);

            log?.Add($"Pattern Replace: '{prefixPattern}' → '{replacementPattern}' ⇒ '{newName}'" +
                     (dropTail ? " (Tail removed, literal numbers applied)" : ""));
            return newName;
        }


        private static List<(string Value, string Type)> NormalizeTokens(string[,] parsed)
        {
            var list = new List<(string, string)>();
            int n = parsed.GetLength(0);
            int i = 0;

            while (i < n)
            {
                string v = parsed[i, 0];
                string t = parsed[i, 1];

                // Keep "format" as "format" so it matches pattern's "format"
                if (t == "0 vor Nummer")
                {
                    var zeros = new System.Text.StringBuilder();
                    while (i < n && parsed[i, 1] == "0 vor Nummer")
                    {
                        zeros.Append(parsed[i, 0]);
                        i++;
                    }

                    if (i < n && parsed[i, 1] == "num")
                    {
                        string num = parsed[i, 0];
                        list.Add((zeros.ToString() + num, "num"));
                        i++;
                        continue;
                    }

                    list.Add((zeros.ToString(), "str"));
                    continue;
                }

                // normal token (including "str", "num", and "format")
                if (t == "str")
                {
                    // Split on common separators so pattern tokens like "-" may align
                    // without requiring an exact whole-token match.
                    var pieces = Regex.Split(v, "([\\-_ ])"); // keep separators as tokens
                    foreach (var piece in pieces)
                    {
                        if (string.IsNullOrEmpty(piece)) continue;
                        list.Add((piece, "str"));
                    }
                }
                else
                {
                    list.Add((v, t));
                }
                i++;

            }

            return list;
        }




        // ---- Delta-aware replacer (keeps padding) ---------------------------------
        private static string DeltaAwareReplace(string baseName, string prefix, string prefixReplacement, List<string> log)
        {
            string newName;
            var pat = SplitFirstNumber(prefix);
            var repl = SplitFirstNumber(prefixReplacement);

            if (!pat.HasNumber || !repl.HasNumber)
            {
                if (baseName.StartsWith(prefix, StringComparison.Ordinal))
                {
                    newName = (prefixReplacement ?? string.Empty) + baseName.Substring(prefix.Length);
                    log?.Add($"Simple Replace: '{prefix}' → '{prefixReplacement}' ⇒ '{newName}'");
                    return newName;
                }
                log?.Add("Simple Replace: Präfix passt nicht am Anfang – keine Änderung.");
                return baseName;
            }

            var rx = new System.Text.RegularExpressions.Regex("^" + System.Text.RegularExpressions.Regex.Escape(pat.Left) + "(\\d+)" + System.Text.RegularExpressions.Regex.Escape(pat.Right));
            var m = rx.Match(baseName);
            if (!m.Success)
            {
                log?.Add("Delta Replace: Template passt nicht am Anfang – keine Änderung.");
                return baseName;
            }

            long matchedNum = Convert.ToInt64(m.Groups[1].Value);
            long delta = repl.Number - pat.Number;
            long newNum = matchedNum + delta;

            int pad = Math.Max(m.Groups[1].Value.Length, repl.NumberStr.Length);
            string newNumStr = (newNum >= 0 && pad > 1) ? newNum.ToString(new string('0', pad)) : newNum.ToString();

            string newHead = repl.Left + newNumStr + repl.Right;
            newName = newHead + baseName.Substring(m.Length);

            log?.Add($"Delta Replace: Δ = {repl.Number} - {pat.Number} = {delta} ⇒ '{baseName}' → '{newName}'");
            return newName;
        }

        // Split first numeric run into Left + Number + Right
        private static (bool HasNumber, string Left, string NumberStr, long Number, string Right) SplitFirstNumber(string s)
        {
            if (s == null) return (false, "", "", 0, "");
            var m = System.Text.RegularExpressions.Regex.Match(s, "(\\d+)");
            if (!m.Success)
                return (false, s, "", 0, "");
            string left = s.Substring(0, m.Index);
            string numStr = m.Value;
            string right = s.Substring(m.Index + m.Length);
            long num = Convert.ToInt64(numStr);
            return (true, left, numStr, num, right);
        }
    }
}
//c:\\users\edhar\onedrive\зображення\test