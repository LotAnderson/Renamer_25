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
        public static string[,] ParseFilename(string filename ,bool view_changes = false)
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

            parts.Add((extension, "format"));

            string[,] result = new string[parts.Count, 2];
            for (int j = 0; j < parts.Count; j++)
            {
                result[j, 0] = parts[j].Item1;
                result[j, 1] = parts[j].Item2;
            }
            if (view_changes) {
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

            // Parse & normalize (merge "0 vor Nummer" + "num" => single "num" like "09")
            var fileTok = NormalizeTokens(ParseFilename(fileBaseName, true));
            var prefixTok = NormalizeTokens(ParseFilename(prefix ?? string.Empty, true));

            int fLen = fileTok.Count;
            int pLen = prefixTok.Count;

            if (pLen == 0)
            {
                log.Add("Hinweis: Kein Präfix angegeben – keine Einschränkung beim Vergleich.");
                string suggested = DeltaAwareReplace(fileBaseName, prefix ?? string.Empty, prefixReplacement ?? string.Empty, log);
                return (0, suggested, log.ToArray());
            }

            if (fLen < pLen)
            {
                log.Add("Struktur-Mismatch: Der Dateiname hat weniger Teile als das Präfix-Fenster.");
                return (2, fileBaseName, log.ToArray()); // block
            }

            bool anyNumericDiff = false;
            bool anyOtherDiff = false;

            log.Add("Unterschiede (innerhalb des Präfix-Fensters, mit führenden Nullen normalisiert):");
            for (int i = 0; i < pLen; i++)
            {
                var (fv, ft) = fileTok[i];
                var (pv, pt) = prefixTok[i];

                if (!string.Equals(ft, pt, StringComparison.Ordinal))
                {
                    anyOtherDiff = true;
                    log.Add($"    Position {i}: Typ verschieden: {ft} vs {pt}");
                    continue;
                }

                if (!string.Equals(fv, pv, StringComparison.Ordinal))
                {
                    if (pt == "num")
                    {
                        // Numeric differs → allowed to shift
                        anyNumericDiff = true;
                        log.Add($"    Position {i}: Zahl verschieden: {fv} != {pv}");
                    }
                    else
                    {
                        anyOtherDiff = true;
                        log.Add($"    Position {i}: Wert verschieden: '{fv}' != '{pv}' (Typ {pt})");
                    }
                }
            }

            int code = anyOtherDiff ? 2 : (anyNumericDiff ? 1 : 0);

            // Suggest the new base name using Δ from prefix→replacement (delta-aware, keeps padding)
            string suggestedBase = (code == 2)
                ? fileBaseName
                : DeltaAwareReplace(fileBaseName, prefix ?? string.Empty, prefixReplacement ?? string.Empty, log);

            if (code == 2)
                log.Add("Ergebnis: Änderung blockiert (Non-Numeric/Struktur-Unterschied).");
            else if (code == 1)
                log.Add($"Ergebnis: Nur numerische Unterschiede → Vorschlag: '{fileBaseName}' → '{suggestedBase}'.");
            else
                log.Add($"Ergebnis: Keine Unterschiede → Vorschlag: '{fileBaseName}' → '{suggestedBase}'.");

            return (code, suggestedBase, log.ToArray());
        }

        // --- Normalize: merge leading-zero tokens + number into one numeric token ---
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