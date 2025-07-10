using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Eingabe {
    class Program {
        static void Main(string[] args) {
            if (args.Length==0)
                Console.WriteLine("Bitte Parameter eingeben!");
            for (int i = 0; i < args.Length; i++) {
                Console.WriteLine($"Eingabe {i}:{args[i]}");
            }
        }
    }
}
