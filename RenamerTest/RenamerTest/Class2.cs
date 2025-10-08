using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace RenamerTest
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
            var fileTok = NormalizeTokens(ParseFilename(fileBaseName, false)); // no console spam here
                                                                               // Pattern: tokenize with '*' support ("any"), numbers as a single "num"
            var pattTok = TokenizePattern(prefix ?? string.Empty);

            if (pattTok.Count == 0)
            {
                log.Add("Hinweis: Kein Präfix angegeben – keine Einschränkung beim Vergleich.");
                string suggested = DeltaAwareReplace(fileBaseName, prefix ?? string.Empty, prefixReplacement ?? string.Empty, log);
                return (0, suggested, log.ToArray());
            }

            // Wildcard-aware comparison:
            // Greedy but with backtracking on the last '*' (classic two-pointer matcher).
            int iFile = 0, iPatt = 0;
            int lastStarPatt = -1, lastStarFile = -1;
            bool anyNumericDiff = false;
            bool anyOtherDiff = false;

            // Helper: try to match a single concrete pattern token (not '*') against a file token
            bool MatchToken((string v, string t) F, (string v, string t) P)
            {
                if (P.t == "any") return true; // handled outside
                if (F.t != P.t) { anyOtherDiff = true; return false; }
                if (F.v == P.v) return true;

                if (P.t == "num")
                {
                    // value differs but both numeric → numeric-only diff
                    anyNumericDiff = true;
                    return true; // treat as acceptable difference
                }
                else
                {
                    anyOtherDiff = true;
                    return false;
                }
            }

            // Walk through tokens
            while (iFile < fileTok.Count)
            {
                if (iPatt < pattTok.Count && pattTok[iPatt].Type == "any")
                {
                    // Remember star position and advance pattern
                    lastStarPatt = iPatt++;
                    lastStarFile = iFile;
                    continue;
                }

                if (iPatt < pattTok.Count && MatchToken(fileTok[iFile], pattTok[iPatt]))
                {
                    // matched this concrete token (types equal; values equal or numeric-diff accepted)
                    iFile++; iPatt++;
                    continue;
                }

                if (lastStarPatt != -1)
                {
                    // Backtrack: let the last '*' absorb one more file token
                    iPatt = lastStarPatt + 1;
                    iFile = ++lastStarFile;
                    continue;
                }

                // No match and no star to backtrack → concrete mismatch (Other)
                anyOtherDiff = true;
                break;
            }

            // Consume trailing '*' in pattern
            while (iPatt < pattTok.Count && pattTok[iPatt].Type == "any") iPatt++;

            // If pattern still has unmatched concrete tokens → mismatch (Other)
            if (iPatt < pattTok.Count)
                anyOtherDiff = true;

            // Build code
            int code = anyOtherDiff ? 2 : (anyNumericDiff ? 1 : 0);

            // Suggest new base name (delta-aware). If blocked, keep original.
            string suggestedBase = (code == 2)
    ? fileBaseName
    : PatternAwareReplace(fileBaseName, prefix ?? string.Empty, prefixReplacement ?? string.Empty, log);

            // Log what happened
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
        // Digits are split like ParseFilename but we DO NOT create "0 vor Nummer" here;
        // we return numbers as a single "num" token ("09", "10", etc.) to keep matching simple.
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

                // collect non-digit, non-asterisk text as one "str" token
                int tStart = i;
                while (i < pattern.Length && pattern[i] != '*' && !char.IsDigit(pattern[i])) i++;
                string text = pattern.Substring(tStart, i - tStart);
                if (text.Length > 0)
                    tokens.Add((text, "str"));
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

            // Build regex from prefix pattern
            var rgx = new StringBuilder("^");
            var starGroupIdx = new List<int>();
            var numGroupIdx = new List<int>();
            int groupCounter = 1;

            foreach (var t in preTok)
            {
                if (t.Type == "str")
                {
                    rgx.Append(Regex.Escape(t.Value));
                }
                else if (t.Type == "num")
                {
                    rgx.Append("(\\d+)");
                    numGroupIdx.Add(groupCounter++);
                }
                else if (t.Type == "any")
                {
                    rgx.Append("(.*)");
                    starGroupIdx.Add(groupCounter++);
                }
            }

            var m = Regex.Match(baseName, rgx.ToString());
            if (!m.Success)
            {
                log?.Add("Pattern Replace: Präfix-Muster passt nicht am Anfang – keine Änderung.");
                return baseName;
            }

            // Detect if replacement has '*' or not
            var repHasStar = repTok.Any(t => t.Type == "any");
            bool dropTail = !repHasStar && baseName.Length > m.Value.Length; // if no '*' and tail exists
            bool forceLiteralNumbers = dropTail; // use replacement numbers literally when truncating

            // Compute Δ from first numeric tokens in patterns (if present)
            long? delta = null;
            int preFirstNumIndex = preTok.FindIndex(t => t.Type == "num");
            int repFirstNumIndex = repTok.FindIndex(t => t.Type == "num");
            if (preFirstNumIndex >= 0 && repFirstNumIndex >= 0)
            {
                if (long.TryParse(preTok[preFirstNumIndex].Value, out var preNum) &&
                    long.TryParse(repTok[repFirstNumIndex].Value, out var repNum))
                {
                    delta = repNum - preNum;
                }
            }

            // Grab first captured number if there was one
            string? capturedNumStr = null;
            int capturedNumDigits = 0;
            if (numGroupIdx.Count > 0)
            {
                capturedNumStr = m.Groups[numGroupIdx[0]].Value;
                capturedNumDigits = capturedNumStr.Length;
            }

            // Build new head from the replacement pattern
            var newHead = new StringBuilder();
            int usedStars = 0;

            foreach (var t in repTok)
            {
                if (t.Type == "str")
                {
                    newHead.Append(t.Value);
                }
                else if (t.Type == "any")
                {
                    // inject next star capture from prefix
                    string cap = usedStars < starGroupIdx.Count ? m.Groups[starGroupIdx[usedStars++]].Value : "";
                    newHead.Append(cap);
                }
                else if (t.Type == "num")
                {
                    int repDigits = t.Value?.Length ?? 0;

                    if (!forceLiteralNumbers && delta.HasValue && capturedNumStr != null && long.TryParse(capturedNumStr, out var capNum))
                    {
                        // shift captured number by Δ and pad
                        long newNum = capNum + delta.Value;
                        int pad = repDigits > 0 ? repDigits : capturedNumDigits;
                        if (pad < capturedNumDigits) pad = capturedNumDigits;
                        string newNumStr = (newNum >= 0 && pad > 1)
                            ? newNum.ToString(new string('0', pad))
                            : newNum.ToString();
                        newHead.Append(newNumStr);
                    }
                    else
                    {
                        // use the replacement literal number (e.g., "1")
                        newHead.Append(t.Value ?? "");
                    }
                }
            }

            // Replace the matched head with the new head + keep or drop the tail
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

                if (t == "0 vor Nummer")
                {
                    // collect all leading zeros
                    var zeros = new System.Text.StringBuilder();
                    while (i < n && parsed[i, 1] == "0 vor Nummer")
                    {
                        zeros.Append(parsed[i, 0]); // typically "0"
                        i++;
                    }

                    // if followed by a numeric token, merge → one "num" token like "0009"
                    if (i < n && parsed[i, 1] == "num")
                    {
                        string num = parsed[i, 0];
                        list.Add((zeros.ToString() + num, "num"));
                        i++;
                        continue;
                    }

                    // otherwise keep zeros as-is (rare), but still one token
                    list.Add((zeros.ToString(), "str")); // treat as plain text to be conservative
                    continue;
                }

                // normal token
                list.Add((v, t));
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