using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Collections.Generic;


namespace Rennamer
{
    class Program
    {
        static void Main(string[] args)
        {
            // --info / --help / -h : print info and exit
            if (args != null && args.Length == 1)
            {
                string a0 = (args[0] ?? "").Trim().ToLowerInvariant();
                if (a0 == "--info" || a0 == "--help" || a0 == "-h" || a0 == "/?")
                {
                    PrintInfo();
                    return;
                }
            }

            // detect non-interactive CLI mode (2 or 3 args)
            bool nonInteractive = (args != null && args.Length >= 2);

            // ✅ Only show banner & instructions in interactive mode
            if (!nonInteractive)
            {
                Console.WriteLine("Willkommen beim erweiterten File Renamer!");
                Console.BackgroundColor = ConsoleColor.Red;
                Console.ForegroundColor = ConsoleColor.White;
                Console.Write(" [ Programm Stop ] ");
                Console.ResetColor();
                Console.WriteLine(" Tippen Sie 'stop' (jederzeit) zum Beenden oder 'reset' zum Neustart.");
                Console.WriteLine();
            }

        SelectDirectory:
            string directoryPath;
            string[] files;

            string prefix = "";
            string prefixReplacement = "";
            string? cliDir = null;

            // --- CLI modes ---
            // - 3 args: [dir] [pattern] [replacement]
            // - 2 args: [pattern] [replacement]
            if (nonInteractive)
            {
                if (args.Length >= 3 && Directory.Exists(args[0]))
                {
                    cliDir = args[0];
                    prefix = args[1];
                    prefixReplacement = args[2];
                }
                else
                {
                    prefix = args[0];
                    prefixReplacement = args[1];
                }

                string candidate = cliDir ?? Environment.CurrentDirectory;
                if (!Directory.Exists(candidate))
                {
                    Console.WriteLine("⚠ Startverzeichnis existiert nicht. Abbruch.");
                    return;
                }
                if (PathSafety.IsDangerousPath(candidate))
                {
                    Console.WriteLine("⚠ Startverzeichnis ist zu allgemein/gefährlich. Abbruch.");
                    return;
                }

                directoryPath = Path.GetFullPath(candidate);
                Console.WriteLine($"Starte im nicht-interaktiven Modus: {directoryPath}");

                if (string.IsNullOrWhiteSpace(prefix))
                {
                    Console.WriteLine("⚠ Leeres Pattern übergeben. Abbruch.");
                    return;
                }
            }
            else
            {
                // Interactive directory input
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

                    break;
                }
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
                if (nonInteractive) return;
                goto SelectDirectory;
            }

            Console.WriteLine("Gefundene Bilddateien:");
            foreach (var f in files)
            {
                var fn = Path.GetFileName(f);
                Console.WriteLine(fn);
                FilenameParser.ParseFilename(fn, true);
            }
            Console.WriteLine();

            // Get pattern interactively only if not provided via args
            if (!nonInteractive)
            {
                var pre = PromptFindReplace("Dateiname/Teil des Namens (Wildcards erlaubt)");
                if (pre.restart) goto SelectDirectory;
                prefix = pre.pattern ?? "";
                prefixReplacement = pre.replacement ?? "";
            }

            // Filter by extension if mentioned
            string? extFilter = Path.GetExtension(prefix);
            if (!string.IsNullOrEmpty(extFilter))
            {
                files = files.Where(p => Path.GetExtension(p)
                             .Equals(extFilter, StringComparison.OrdinalIgnoreCase))
                             .ToArray();
                Console.WriteLine($"Dateien werden nach Erweiterung '{extFilter}' gefiltert.");
            }

            // ---- Preview changes ----
            Console.WriteLine();
            Console.WriteLine("Vorschau der Änderungen:");
            int changes = 0;
            var planned = new List<(string oldPath, string newPath)>(files.Length);

            foreach (string file in files)
            {
                string fileName = Path.GetFileName(file);
                string ext = Path.GetExtension(file);
                string baseName = Path.GetFileNameWithoutExtension(file);

                bool matchOnFull = PatternMentionsExtension(prefix) || PatternMentionsExtension(prefixReplacement);
                string subject = matchOnFull ? fileName : baseName;

                var (code, suggested, diffLog) = FilenameParser.Find_difference(subject, prefix, prefixReplacement);
                if (code == 2) continue;

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
                if (nonInteractive) return;
            }

            // ---- Confirm & perform ----
            bool proceed = nonInteractive || ReadYesNo("Wollen Sie wirklich fortfahren? (j/n): ");
            if (proceed && planned.Count > 0)
            {
                int renamed = 0, skipped = 0, failed = 0;
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
                        var tempPath = oldPath + ".renaming_" + Guid.NewGuid().ToString("N");
                        File.Move(oldPath, tempPath);
                        temps.Add((tempPath, newPath));
                    }

                    foreach (var (tempPath, finalPath) in temps)
                    {
                        if (File.Exists(finalPath))
                        {
                            Console.WriteLine($"Übersprungen (Ziel existiert): {Path.GetFileName(finalPath)}");
                            var origPath = tempPath.Substring(0, tempPath.IndexOf(".renaming_", StringComparison.Ordinal));
                            File.Move(tempPath, origPath);
                            skipped++;
                            continue;
                        }

                        File.Move(tempPath, finalPath);
                        renamed++;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Fehler: {ex.Message}. Versuche Rückgängig zu machen…");
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
                        catch { }
                    }
                    failed++;
                }

                Console.WriteLine();
                Console.WriteLine($"Fertig. Umbenannt: {renamed}, Übersprungen: {skipped}, Fehler: {failed}");
                if (nonInteractive) return;

                files = Directory.EnumerateFiles(directoryPath, "*", SearchOption.TopDirectoryOnly)
                                 .Where(p => p.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                                          || p.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
                                          || p.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase))
                                 .ToArray();
            }
            else if (!proceed)
            {
                Console.WriteLine("Vorgang abgebrochen.");
                if (nonInteractive) return;
            }

            // ---- Menu (interactive only) ----
            while (!nonInteractive)
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
        private static void PrintInfo()
        {
            var exe = AppDomain.CurrentDomain.FriendlyName;
            Console.WriteLine("rennamer — Bilddateien intelligent umbenennen");
            Console.WriteLine();
            Console.WriteLine("Verwendung:");
            Console.WriteLine($"  Interaktiv:\n    {exe}");
            Console.WriteLine();
            Console.WriteLine($"  Nicht-interaktiv (aktuelles Verzeichnis):");
            Console.WriteLine($"    {exe} <pattern> <replacement>");
            Console.WriteLine($"    Beispiel: {exe} test new_test");
            Console.WriteLine();
            Console.WriteLine($"  Nicht-interaktiv (explizites Verzeichnis):");
            Console.WriteLine($"    {exe} <directory> <pattern> <replacement>");
            Console.WriteLine($"    Beispiel: {exe} C:\\Bilder test new_test");
            Console.WriteLine();
            Console.WriteLine("Hinweise zu Pattern/Replacement:");
            Console.WriteLine("  • '*' = Wildcard (beliebige Zeichen)");
            Console.WriteLine("  • Zahlen werden als numerische Tokens erkannt (inkl. Null-Padding-Erhalt).");
            Console.WriteLine("  • Enthält das Pattern eine Extension (z.B. '.png'), wird der gesamte Dateiname geprüft.");
            Console.WriteLine("  • Beginnt ein Pattern mit einer Zahl und enthält keine Extension, wirkt es wie '*<Zahl>'.");
            Console.WriteLine("  • Bei Zahl-zu-Zahl-Replacement wird Δ (Differenz) übernommen und Padding beachtet.");
            Console.WriteLine();
            Console.WriteLine("Beispiele:");
            Console.WriteLine($"  {exe} 230 231             → erhöht erste passende Zahl (>=230) um +1");
            Console.WriteLine($"  {exe} test new_test       → ersetzt Präfix 'test' durch 'new_test'");
            Console.WriteLine($"  {exe} *230 *229           → verringert die erste gefundene Zahl 230 → 229");
            Console.WriteLine($"  {exe} *.png *.jpg         → ändert die Extension (nur wenn vollständige Namen gemeint)");
            Console.WriteLine();
            Console.WriteLine("Weitere Optionen:");
            Console.WriteLine($"  {exe} --info | --help | -h | /?   → zeigt diese Hilfe");
        }

    }
}

