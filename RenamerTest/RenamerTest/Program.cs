using System.Text.RegularExpressions;

using System;
using System.IO;


namespace RenamerTest
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Willkommen beim erweiterten File Renamer!");

            // Eingabe des Verzeichnisses
            Console.Write("Geben Sie den vollständigen Pfad des Verzeichnisses ein: ");
            string directoryPath = Console.ReadLine();

            // Überprüfen, ob der Pfad existiert
            if (!Directory.Exists(directoryPath))
            {
                Console.WriteLine("Das angegebene Verzeichnis existiert nicht.");
                return;
            }

            // Eingabe des Präfixes
            Console.Write("Geben Sie das Präfix ein, das geändert oder entfernt werden soll (Leer lassen, um nichts zu ändern): ");
            string prefix = Console.ReadLine();

            // Eingabe des Suffixes
            Console.Write("Geben Sie das Suffix ein, das geändert oder entfernt werden soll (Leer lassen, um nichts zu ändern): ");
            string suffix = Console.ReadLine();

            // Eingabe der Option für führende Nullen (addieren oder entfernen)
            Console.Write("Möchten Sie führende Nullen hinzufügen oder entfernen? (add/remove): ");
            string leadingZerosOption = Console.ReadLine().ToLower();

            if (leadingZerosOption != "add" && leadingZerosOption != "remove")
            {
                Console.WriteLine("Ungültige Option! Bitte 'add' oder 'remove' wählen.");
                return;
            }

            string[] files = Directory.GetFiles(directoryPath);
            foreach (string file in files)
            {
                string fileName = Path.GetFileName(file);
                string fileExtension = Path.GetExtension(file);
                string baseName = Path.GetFileNameWithoutExtension(file);

                // Falls Präfix entfernt oder geändert werden soll
                if (!string.IsNullOrEmpty(prefix) && baseName.StartsWith(prefix))
                {
                    baseName = baseName.Substring(prefix.Length); // Präfix entfernen
                }

                // Falls Suffix entfernt oder geändert werden soll
                if (!string.IsNullOrEmpty(suffix) && baseName.EndsWith(suffix))
                {
                    baseName = baseName.Substring(0, baseName.Length - suffix.Length); // Suffix entfernen
                }

                // Führende Nullen hinzufügen oder entfernen
                if (leadingZerosOption == "add")
                {
                    baseName = AddLeadingZeros(baseName);
                }
                else if (leadingZerosOption == "remove")
                {
                    baseName = RemoveLeadingZeros(baseName);
                }

                // Neuer Name mit der ursprünglichen Dateiendung
                string newFileName = Path.Combine(directoryPath, baseName + fileExtension);

                // Umbenennen
                if (file != newFileName)
                {
                    File.Move(file, newFileName);
                    Console.WriteLine($"Datei umbenannt: {fileName} -> {baseName + fileExtension}");
                }
            }

            Console.WriteLine("Vorgang abgeschlossen.");
        }

        // Funktion, um führende Nullen hinzuzufügen
        static string AddLeadingZeros(string name)
        {
            return name.PadLeft(4, '0'); // Hier wird auf 4 Ziffern erweitert (Anpassbar)
        }

        // Funktion, um führende Nullen zu entfernen
        static string RemoveLeadingZeros(string name)
        {
            return Regex.Replace(name, @"^0+", "");
        }
    }
}
