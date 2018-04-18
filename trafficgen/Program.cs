using System;
using System.Net.Http;

namespace trafficgen {
    class Program {
        
        static void Main(string[] args) {
            if (args.Length != 3) { Usage(); return; }

            string uriSource = args[1];
            if (!uriSource.EndsWith("/", StringComparison.Ordinal)) {
                uriSource += "/";
            }
            if (!Uri.TryCreate(uriSource, UriKind.Absolute, out Uri atcSvcUri)) { Usage(); return; }

            if (!int.TryParse(args[2], out int aircraftInAirGoal)) { Usage(); return; }

            HttpClient client = new HttpClient();
            client.BaseAddress = atcSvcUri;

            Console.WriteLine("Starting simulation, press any key to end...");
            try {
                while (!Console.KeyAvailable) {
                    string countResponse = client.GetStringAsync("api/flights/count").Result;
                    
                }

                Console.ReadKey(intercept: true);
            }
            catch(Exception e) {
                Console.WriteLine($"Error: {e.ToString()}");
            }

        }

        static void Usage() {
            Console.WriteLine("Usage: trafficgen <atc-service-url> <number-of-aircraft-to-simulate>");
            Console.WriteLine("");
        }
    }
}
