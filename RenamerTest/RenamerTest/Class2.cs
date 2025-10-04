using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.ComponentModel.Design;

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
        public static void Find_difference(string file_name_1, string file_name_2) {
            string[,] parsed1 = ParseFilename(file_name_1, true);
            string[,] parsed2 = ParseFilename(file_name_2, true);
            int len1 = parsed1.GetLength(0);
            int len2 = parsed2.GetLength(0);
            int minLen = Math.Min(len1, len2);
            Console.WriteLine("\nUnterschiede:");
            for (int i = 0; i < minLen; i++)
            {
                if (parsed1[i, 0] != parsed2[i, 0] || parsed1[i, 1] != parsed2[i, 1])
                {
                    Console.WriteLine($"    Position {i}: '{parsed1[i, 0]}' ({parsed1[i, 1]}) != '{parsed2[i, 0]}' ({parsed2[i, 1]})");
                }
            }
            if (len1 != len2)
            {
                Console.WriteLine("Dateinamen haben unterschiedliche Anzahl von Teilen:");
                if (len1 > len2)
                {
                    for (int i = minLen; i < len1; i++)
                    {
                        Console.WriteLine($"    Datei 1 hat zusätzlich: '{parsed1[i, 0]}' ({parsed1[i, 1]})");
                    }
                }
                else
                {
                    for (int i = minLen; i < len2; i++)
                    {
                        Console.WriteLine($"    Datei 2 hat zusätzlich: '{parsed2[i, 0]}' ({parsed2[i, 1]})");
                    }
                }
            }
        }

    }
}
