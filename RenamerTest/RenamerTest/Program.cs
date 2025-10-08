using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Collections.Generic;


namespace RenamerTest
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Willkommen beim erweiterten File Renamer!");
            Console.BackgroundColor = ConsoleColor.Red;
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write(" [ Programm Stop ] ");
            Console.ResetColor();
            Console.WriteLine(" Tippen Sie 'stop' (jederzeit) zum Beenden oder 'reset' zum Neustart.");
            Console.WriteLine();

        // ---- Get and validate directory + collect files in a single loop ----
        SelectDirectory:
            string directoryPath;
            string[] files;

            while (true)
            {
                directoryPath = ReadInput("Geben Sie den vollständigen Pfad des Verzeichnisses ein: ");
                if (directoryPath == "__reset__") continue;

                if (!Directory.Exists(directoryPath))
                {
                    Console.WriteLine("Das angegebene Verzeichnis existiert nicht.");
                    continue;
                }

                if (PathSafety.IsDangerousPath(directoryPath))
                {
                    Console.WriteLine("⚠ Der gewählte Pfad ist zu allgemein/gefährlich...");
                    continue;
                }

                break; // valid + safe
            }

        StartInSameDirectory:
            files = Directory.EnumerateFiles(directoryPath, "*", SearchOption.TopDirectoryOnly)
                             .Where(p => p.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                                      || p.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
                                      || p.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase))
                             .ToArray();

            if (files.Length == 0)
            {
                Console.WriteLine("Keine .png/.jpg/.jpeg Dateien im Verzeichnis gefunden.");
                goto SelectDirectory;
            }

            // ---- Overview with parsed parts ---------------------------------
            Console.WriteLine("Gefundene Bilddateien:");
            foreach (var f in files)
            {
                var fn = Path.GetFileName(f);
                Console.WriteLine(fn);
                var parsed = FilenameParser.ParseFilename(fn ,true);
            }
            Console.WriteLine();

            // ---- Collect ONLY prefix rule ------------------------------------
            string prefix = "";
            string prefixReplacement = "";
            var pre = PromptFindReplace("Dateiname/Teil des Namens (Wildcards erlaubt)");
            if (pre.restart) goto SelectDirectory;
            prefix = pre.pattern ?? "";
            prefixReplacement = pre.replacement ?? "";
            var prefix_parsed = FilenameParser.ParseFilename(prefix,true);
            var prefix_replacement_parsed = FilenameParser.ParseFilename(prefixReplacement, true);
            // Optional: filter files by extension mentioned in the pattern
            string? extFilter = Path.GetExtension(prefix);
            if (!string.IsNullOrEmpty(extFilter))
            {
                files = files.Where(p => Path.GetExtension(p)
                             .Equals(extFilter, StringComparison.OrdinalIgnoreCase))
                             .ToArray();
                Console.WriteLine($"Dateien werden nach Erweiterung '{extFilter}' gefiltert.");
            }

            // ---- Preview changes ---------------------------------------------
            Console.WriteLine();
            Console.WriteLine("Vorschau der Änderungen:");
            int changes = 0;
            var planned = new List<(string oldPath, string newPath)>(files.Length);

            foreach (string file in files)
            {
                string fileName = Path.GetFileName(file);
                string ext = Path.GetExtension(file);
                string baseName = Path.GetFileNameWithoutExtension(file);

                // If pattern or replacement mentions an extension, match on FULL name
                bool matchOnFull = PatternMentionsExtension(prefix) || PatternMentionsExtension(prefixReplacement);
                string subject = matchOnFull ? fileName : baseName;

                // classify + get suggested name for the chosen subject
                var (code, suggested, diffLog) = FilenameParser.Find_difference(subject, prefix, prefixReplacement);
                if (code == 2) continue; // block only when non-numeric/structural outside wildcards

                // If suggestion already contains extension, use it as-is; otherwise add the original ext
                string targetFileName = SuggestedHasExtension(suggested) ? suggested : suggested + ext;
                string targetPath = Path.Combine(directoryPath, targetFileName);

                if (!string.Equals(file, targetPath, StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine($"{fileName} -> {Path.GetFileName(targetPath)}");
                    changes++;
                    planned.Add((file, targetPath));
                }
            }


            if (changes == 0)
            {
                Console.WriteLine("Es gibt keine Änderungen vorzunehmen.");
            }

            // ---- Confirm and perform -----------------------------------------
            bool proceed = ReadYesNo("Wollen Sie wirklich fortfahren? (j/n): ");
            if (proceed && planned.Count > 0)
            {
                int renamed = 0, skipped = 0, failed = 0;

                // Phase 1: move everything to unique temp names
                var temps = new List<(string tempPath, string finalPath)>(planned.Count);
                try
                {
                    foreach (var (oldPath, newPath) in planned)
                    {
                        if (string.Equals(oldPath, newPath, StringComparison.OrdinalIgnoreCase))
                        {
                            skipped++;
                            continue;
                        }

                        // unique temp beside original file
                        var tempPath = oldPath + ".renaming_" + Guid.NewGuid().ToString("N");
                        File.Move(oldPath, tempPath);
                        temps.Add((tempPath, newPath));
                    }

                    // Phase 2: move temps to their final names
                    foreach (var (tempPath, finalPath) in temps)
                    {
                        // If a non-participating file blocks the final name, skip (or delete to overwrite)
                        if (File.Exists(finalPath))
                        {
                            Console.WriteLine($"Übersprungen (Ziel existiert): {Path.GetFileName(finalPath)}");
                            // Roll back this one temp -> original name next line:
                            var origPath = tempPath.Substring(0, tempPath.IndexOf(".renaming_", StringComparison.Ordinal));
                            File.Move(tempPath, origPath);
                            skipped++;
                            continue;
                        }

                        File.Move(tempPath, finalPath);
                        Console.WriteLine($"Umbenannt: {Path.GetFileName(finalPath)}");
                        renamed++;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Fehler: {ex.Message}. Versuche Rückgängig zu machen…");
                    // best-effort rollback: move any temps back
                    foreach (var (tempPath, _) in temps)
                    {
                        try
                        {
                            if (File.Exists(tempPath))
                            {
                                var origPath = tempPath.Substring(0, tempPath.IndexOf(".renaming_", StringComparison.Ordinal));
                                File.Move(tempPath, origPath);
                            }
                        }
                        catch { /* swallow */ }
                    }
                    failed++;
                }

                Console.WriteLine();
                Console.WriteLine($"Fertig. Umbenannt: {renamed}, Übersprungen: {skipped}, Fehler: {failed}");

                // Refresh file list after action
                files = Directory.EnumerateFiles(directoryPath, "*", SearchOption.TopDirectoryOnly)
                                 .Where(p => p.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                                          || p.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
                                          || p.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase))
                                 .ToArray();
            }
            else if (!proceed)
            {
                Console.WriteLine("Vorgang abgebrochen.");
            }

            // ---- Single, clean menu ------------------------------------------
            while (true)
            {
                Console.WriteLine();
                Console.WriteLine("Was möchten Sie tun?");
                Console.WriteLine("[1] Weitermachen im selben Ordner");
                Console.WriteLine("[2] Reset (neues Verzeichnis wählen)");
                Console.WriteLine("[3] Stop");
                var choice = ReadInput("Auswahl (1/2/3): ");
                if (choice == "__reset__") { Console.WriteLine(); goto SelectDirectory; }
                choice = (choice ?? "").Trim().ToLowerInvariant();

                if (choice == "1")
                {
                    if (files.Length == 0)
                    {
                        Console.WriteLine("In diesem Ordner sind keine passenden Dateien mehr vorhanden.");
                        goto SelectDirectory;
                    }
                    Console.WriteLine();
                    goto StartInSameDirectory;
                }
                else if (choice == "2")
                {
                    Console.WriteLine();
                    goto SelectDirectory;
                }
                else if (choice == "3" || choice == "stop")
                {
                    Console.WriteLine("Programm wurde gestoppt.");
                    Environment.Exit(0);
                }
                else
                {
                    Console.WriteLine("Bitte 1, 2 oder 3 eingeben (oder 'reset'/'stop').");
                    continue;
                }
            }
        }

        // ---------- Helpers ---------------------------------------------------

        // Only prefix modification
        static string ApplyPrefixOnly(string baseName, string prefix, string prefixReplacement)
        {
            if (!string.IsNullOrEmpty(prefix) && baseName.StartsWith(prefix))
                return (prefixReplacement ?? string.Empty) + baseName.Substring(prefix.Length);
            return baseName;
        }

        static string ReadInput(string prompt)
        {
            Console.Write(prompt);
            string raw = Console.ReadLine() ?? "";
            string trimmedLower = raw.Trim().ToLowerInvariant();

            if (trimmedLower == "stop")
            {
                Console.WriteLine("Programm wurde gestoppt.");
                Environment.Exit(0);
            }
            if (trimmedLower == "reset")
            {
                Console.WriteLine("Prozess wird neu gestartet...");
                return "__reset__";
            }
            return raw.Trim();
        }

        static string ReadInputRaw(string prompt)
        {
            Console.Write(prompt);
            string raw = Console.ReadLine() ?? "";
            string trimmedLower = raw.Trim().ToLowerInvariant();

            if (trimmedLower == "stop")
            {
                Console.WriteLine("Programm wurde gestoppt.");
                Environment.Exit(0);
            }
            if (trimmedLower == "reset")
            {
                Console.WriteLine("Prozess wird neu gestartet...");
                return "__reset__";
            }
            return raw; // preserve exact text
        }

        static bool ReadYesNo(string prompt)
        {
            while (true)
            {
                string? ans = ReadInput(prompt);  // <- changed
                if (ans == "__reset__") continue;

                ans = ans?.Trim().ToLowerInvariant();
                if (ans == "j" || ans == "ja" || ans == "y" || ans == "yes") return true;
                if (ans == "n" || ans == "nein" || ans == "no") return false;

                Console.WriteLine("Bitte 'j' oder 'n' eingeben (oder 'reset'/'stop').");
            }
        }

        private static (bool restart, string? pattern, string? replacement) PromptFindReplace(string label)
        {
            string pattern = ReadInputRaw($"{label} zum Entfernen/Ändern (leer lassen = kein {label}): ");
            if (pattern == "__reset__") return (true, null, null);

            string replacement = "";
            if (!string.IsNullOrEmpty(pattern))
            {
                replacement = ReadInputRaw($"Neues {label} (leer lassen = {label} entfernen): ");
                if (replacement == "__reset__") return (true, null, null);
            }
            return (false, pattern, replacement);
        }
        static bool PatternMentionsExtension(string? s) => !string.IsNullOrEmpty(s) && s.IndexOf('.') >= 0;
        static bool SuggestedHasExtension(string? s) => !string.IsNullOrEmpty(s) && Path.GetExtension(s) != "";

    }
}
