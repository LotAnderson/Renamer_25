using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace renamerIdee {
    class Matcher {
        static string VERSION = "V1.0";

        /// <summary>
        /// 
        /// </summary>
        /// <param name="oldName">old pattern</param>
        /// <param name="newName">new pattern</param>
        /// <param name="files">List of filenames</param>
        /// <returns>List of changed filenames</returns>
        public static List<string> matcher(string oldName, string newName, List<string> files) {
            //ToDo: Do the magic here...
            
            return files;
        }
        static string[,] ParseFilename(string filename)
        {
            string extension = Path.GetExtension(filename);                  // z.B. ".jpg"
            string nameWithoutExt = Path.GetFileNameWithoutExtension(filename); // "0Bild_001012_f"

            var parts = new List<(string, string)>();
            int i = 0;

            while (i < nameWithoutExt.Length)
            {
                if (char.IsDigit(nameWithoutExt[i]))
                {
                    // Zahlenblock sammeln
                    int start = i;
                    while (i < nameWithoutExt.Length && char.IsDigit(nameWithoutExt[i]))
                        i++;
                    string numberBlock = nameWithoutExt.Substring(start, i - start);

                    // Ведущие нули отдельно
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
                    // Buchstaben/Symbole sammeln
                    int start = i;
                    while (i < nameWithoutExt.Length && !char.IsDigit(nameWithoutExt[i]))
                        i++;
                    string textBlock = nameWithoutExt.Substring(start, i - start);
                    parts.Add((textBlock, "str"));
                }
            }

            // Erweiterung anhängen
            parts.Add((extension, "format"));

            // В 2D массив
            string[,] result = new string[parts.Count, 2];
            for (int j = 0; j < parts.Count; j++)
            {
                result[j, 0] = parts[j].Item1;
                result[j, 1] = parts[j].Item2;
            }

            return result;
        }
        static void runTests() {

            Console.WriteLine("Run All Matcher Tests");
            string oldP ="", newP= "", res="";
            string[] files1 = {"clipboard01.jpg", "clipboard02.jpg", "clipboard03.jpg",
                               "clipboard01.gif", "img01.jpg", "img-abc.jpg" };

            oldP = "clipboard01.jpg";
            newP = "clipboard01.jpg";
            res = "clipboard01.jpg clipboard02.jpg clipboard03.jpg clipboard01.gif img01.jpg img-abc.jpg";
            test(files1, oldP, newP, res);

            string filename = "0Bild_001012_f.jpg";
            string[,] result = ParseFilename(filename);

            // Ausgabe
            for (int i = 0; i < result.GetLength(0); i++)
                Console.WriteLine($"{result[i, 0]} => {result[i, 1]}");
            Console.BackgroundColor = ConsoleColor.Green;
            Console.WriteLine("All tests succeeded!");
            Console.BackgroundColor = ConsoleColor.Black;
            Console.ReadKey();

        }

        private static void test(string[] files, string oldName, string newName, string testRes = null) {
            Console.WriteLine($"oldName:{oldName} newName: {newName}");
            List<string> res = matcher(oldName, newName, new List<string>(files));
            string resS = string.Join(" ", res);
            Console.WriteLine("Old:" + string.Join(" ", new List<string>(files)));
            Console.WriteLine("New:" + resS);
            Console.WriteLine("--------------------------------------------------");
            if (testRes != null && resS != testRes) {
                throw new Exception("Test failed: expected:" + testRes + " received:" + resS);
            }
        }


        public static void Main(string[] args) {
            int RUN_DEBUG = 1;

            if (RUN_DEBUG == 1) {
                runTests();
                Console.ReadKey();
                return;
            }


            Console.WriteLine("ToDo: current work on matcher...");
            Console.ReadKey();
        }

    }
}
