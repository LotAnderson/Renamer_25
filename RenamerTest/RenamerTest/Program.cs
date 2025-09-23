using System.Text.RegularExpressions;
using System.Linq; // Add at the top if not present

using System;
using System.IO;


namespace RenamerTest
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Willkommen beim erweiterten File Renamer!");

            // Simulated button with colored background
            Console.BackgroundColor = ConsoleColor.Red;
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write(" [ Programm Stop ] ");
            Console.ResetColor();
            Console.WriteLine(" Tippen Sie 'stop' und drücken Sie Enter, um das Programm zu beenden.");
            Console.WriteLine();
            // 1) Get current directory
            string currentDirectory = Directory.GetCurrentDirectory();

            // 2) Get all .png and .jpg files
            string[] imageFiles = Directory.GetFiles(currentDirectory, "*.png")
                .Concat(Directory.GetFiles(currentDirectory, "*.jpg"))
                .ToArray();

            // 3) Output to console
            Console.WriteLine("Alle .png und .jpg Dateien im aktuellen Verzeichnis:");
            foreach (string file in imageFiles)
            {
                string fileName = Path.GetFileName(file);
                Console.WriteLine(fileName);

                // Output parsed version
                string[,] result = ParseFilename(fileName);
                for (int i = 0; i < result.GetLength(0); i++)
                    Console.WriteLine($"    {result[i, 0]} => {result[i, 1]}");
            }

            string directoryPath = "";
            while (true)
            {
                directoryPath = ReadInputOrResetOrStop("Geben Sie den vollständigen Pfad des Verzeichnisses ein ('reset' zum Neustart): ");

                // Reset process if user types 'reset'
                if (directoryPath == "__reset__")
                    continue;

                // Validate directory
                while (!Directory.Exists(directoryPath))
                {
                    Console.WriteLine("Das angegebene Verzeichnis existiert nicht.");
                    directoryPath = ReadInputOrResetOrStop("Bitte geben Sie einen gültigen Verzeichnispfad ein ('reset' zum Neustart): ");
                    if (directoryPath == "__reset__")
                        break;
                }
                if (directoryPath == "__reset__")
                    continue;

                // ... rest of your process (prefix, suffix, file renaming, etc.)

                break; // Exit loop after successful process
            }

            //TODO : Hier Dateiüberblick

            // 2) Get all .png and .jpg files
            imageFiles = Directory.GetFiles(directoryPath, "*.png")
                .Concat(Directory.GetFiles(directoryPath, "*.jpg"))
                .ToArray();

            // 3) Output to console
            Console.WriteLine("Alle .png und .jpg Dateien im aktuellen Verzeichnis:");
            foreach (string file in imageFiles)
            {
                string fileName = Path.GetFileName(file);
                Console.WriteLine(fileName);

                // Output parsed version
                string[,] result = ParseFilename(fileName);
                for (int i = 0; i < result.GetLength(0); i++)
                    Console.WriteLine($"    {result[i, 0]} => {result[i, 1]}");
            }
            // Eingabe des Präfixes
            Console.Write("Geben Sie das Präfix ein, das geändert oder entfernt werden soll (Leer lassen, um nichts zu ändern): ");
            string prefix = Console.ReadLine();

            // Eingabe des Suffixes
            Console.Write("Geben Sie das Suffix ein, das geändert oder entfernt werden soll (Leer lassen, um nichts zu ändern): ");
            string suffix = Console.ReadLine();

            //Mittelstufe
            // Eingabe der Option für führende Nullen (addieren oder entfernen)
            //Console.Write("Möchten Sie führende Nullen hinzufügen oder entfernen? (add/remove): ");
            //string leadingZerosOption = Console.ReadLine().ToLower();

            //if (leadingZerosOption != "add" && leadingZerosOption != "remove")
            //{
            //    Console.WriteLine("Ungültige Option! Bitte 'add' oder 'remove' wählen.");
            //    return;
            //}

            string[] files = Directory.GetFiles(directoryPath);

            //foreach (string file in files)
            //{
            //    string fileName = Path.GetFileName(file);

            //    string[,] result = ParseFilename(fileName);

            //    // Ausgabe
            //    for (int i = 0; i < result.GetLength(0); i++)
            //        Console.WriteLine($"{result[i, 0]} => {result[i, 1]}");
            //    string fileExtension = Path.GetExtension(file);
            //    string baseName = Path.GetFileNameWithoutExtension(file);

            //    // Falls Präfix entfernt oder geändert werden soll
            //    if (!string.IsNullOrEmpty(prefix) && baseName.StartsWith(prefix))
            //    {
            //        baseName = baseName.Substring(prefix.Length); // Präfix entfernen
            //    }

            //    // Falls Suffix entfernt oder geändert werden soll
            //    if (!string.IsNullOrEmpty(suffix) && baseName.EndsWith(suffix))
            //    {
            //        baseName = baseName.Substring(0, baseName.Length - suffix.Length); // Suffix entfernen
            //    }

            //    //Microsoft           
            //    //// Führende Nullen hinzufügen oder entfernen
            //    //if (leadingZerosOption == "add")
            //    //{
            //    //    baseName = AddLeadingZeros(baseName);
            //    //}
            //    //else if (leadingZerosOption == "remove")
            //    //{
            //    //    baseName = RemoveLeadingZeros(baseName);
            //    //}

            //    // Neuer Name mit der ursprünglichen Dateiendung
            //    string newFileName = Path.Combine(directoryPath, baseName + fileExtension);

            //    // Umbenennen
            //    if (file != newFileName)
            //    {
            //        File.Move(file, newFileName);
            //        Console.WriteLine($"Datei umbenannt: {fileName} -> {baseName + fileExtension}");
            //    }
            //}
            Console.WriteLine("Folgende Änderungen werden vorgenommen:");
            foreach (string file in files)
            {
                string fileName = Path.GetFileName(file);
                string fileExtension = Path.GetExtension(file);
                string baseName = Path.GetFileNameWithoutExtension(file);

                if (!string.IsNullOrEmpty(prefix) && baseName.StartsWith(prefix))
                    baseName = baseName.Substring(prefix.Length);

                if (!string.IsNullOrEmpty(suffix) && baseName.EndsWith(suffix))
                    baseName = baseName.Substring(0, baseName.Length - suffix.Length);

                string newFileName = baseName + fileExtension;

                if (fileName != newFileName)
                    Console.WriteLine($"{fileName} -> {newFileName}");
            }

            // Bestätigungsabfrage
            Console.Write("Wollen Sie wirklich fortfahren? (j/n): ");
            string confirm = Console.ReadLine()?.Trim().ToLower();
            if (confirm != "j")
            {
                Console.WriteLine("Vorgang abgebrochen.");
                return;
            }

            Console.WriteLine("Vorgang abgeschlossen.");
        }

        //Mittelstufe
        // Funktion, um führende Nullen hinzuzufügen
        //static string AddLeadingZeros(string name)
        //{
        //    return name.PadLeft(4, '0'); // Hier wird auf 4 Ziffern erweitert (Anpassbar)
        //}

        // Funktion, um führende Nullen zu entfernen
        static string RemoveLeadingZeros(string name)
        {
            return Regex.Replace(name, @"^0+", "");
        }
        static string[,] ParseFilename(string filename)
        {
            string extension = Path.GetExtension(filename);                  // e.g., ".jpg"
            string nameWithoutExt = Path.GetFileNameWithoutExtension(filename); // e.g., "0Bild_001012_f"

            var parts = new List<(string, string)>();
            int i = 0;

            while (i < nameWithoutExt.Length)
            {
                if (char.IsDigit(nameWithoutExt[i]))
                {
                    // Collect digit block
                    int start = i;
                    while (i < nameWithoutExt.Length && char.IsDigit(nameWithoutExt[i]))
                        i++;
                    string numberBlock = nameWithoutExt.Substring(start, i - start);

                    // Leading zeros separately
                    int zeroCount = 0;
                    while (zeroCount < numberBlock.Length && numberBlock[zeroCount] == '0')
                        zeroCount++;

                    if (zeroCount > 0)
                        parts.Add((new string('0', zeroCount), "0 vor Nummer"));
                    if (zeroCount < numberBlock.Length)
                        parts.Add((numberBlock.Substring(zeroCount), "num"));
                }
                else
                {
                    // Collect letters/symbols
                    int start = i;
                    while (i < nameWithoutExt.Length && !char.IsDigit(nameWithoutExt[i]))
                        i++;
                    string textBlock = nameWithoutExt.Substring(start, i - start);
                    parts.Add((textBlock, "str"));
                }
            }

            // Add extension
            parts.Add((extension, "format"));

            // Convert to 2D array
            string[,] result = new string[parts.Count, 2];
            for (int j = 0; j < parts.Count; j++)
            {
                result[j, 0] = parts[j].Item1;
                result[j, 1] = parts[j].Item2;
            }

            return result;
        }
        static string ReadInputOrStop(string prompt)
        {
            Console.Write(prompt);
            string input = Console.ReadLine();
            if (input?.Trim().ToLower() == "stop")
            {
                Console.WriteLine("Programm wurde gestoppt.");
                Environment.Exit(0);
            }
            return input;
        }
        static string ReadInputOrResetOrStop(string prompt)
        {
            Console.Write(prompt);
            string input = Console.ReadLine();
            if (input?.Trim().ToLower() == "stop")
            {
                Console.WriteLine("Programm wurde gestoppt.");
                Environment.Exit(0);
            }
            if (input?.Trim().ToLower() == "reset")
            {
                Console.WriteLine("Prozess wird neu gestartet...");
                return "__reset__";
            }
            return input;
        }
    }

}
