using System;

namespace trafficgen {
    class Program {
        
        static void Main(string[] args) {
            if (args.Length != 3) { Usage(); return; }

            if (!Uri.TryCreate(args[1], UriKind.Absolute, out Uri atcSvcUri)) { Usage(); return; }

            if (!int.TryParse(args[2], out int aircraftInAirGoal)) { Usage(); return; }

        }

        static void Usage() {
            Console.WriteLine("Usage: trafficgen <atc-service-url> <number-of-aircraft-to-simulate>");
            Console.WriteLine("");
        }
    }
}
