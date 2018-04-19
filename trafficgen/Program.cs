using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;

using AirTrafficControl.Interfaces;
using Newtonsoft.Json;

namespace trafficgen {
    
    class Program {
        
        static void Main(string[] args) {
            if (args.Length != 2) { Usage(); return; }

            string uriSource = args[0];
            if (!uriSource.EndsWith("/", StringComparison.Ordinal)) {
                uriSource += "/";
            }
            if (!Uri.TryCreate(uriSource, UriKind.Absolute, out Uri atcSvcUri)) { Usage(); return; }

            if (!int.TryParse(args[1], out int aircraftGoal)) { Usage(); return; }

            HttpClient client = new HttpClient();
            client.BaseAddress = atcSvcUri;
            var randomGen = new Random();

            Console.WriteLine("Starting simulation, press any key to end...");

            while (!Console.KeyAvailable) {
                try {
                    string countResponse = client.GetStringAsync("api/flights/count").Result;

                    if (!int.TryParse(countResponse, out int aircraftCount)) {
                        Console.WriteLine($"Cannot parse the response from flight count inquiry. Server returned '{countResponse}'");
                    }
                    else {
                        if (aircraftCount < aircraftGoal) {
                            int flightsToStart = aircraftGoal - aircraftCount;
                            Console.WriteLine($"Number of aircraft in the air below goal, starting {flightsToStart} flights...");

                            for (int i = 0; i < flightsToStart; i++) {
                                StartNewFlight(client, randomGen);
                            }
                        }
                    }
                }
                catch (Exception e) {
                    Console.WriteLine($"Unexpected error encountered: {e.ToString()}");
                }

                Thread.Sleep(TimeSpan.FromSeconds(2));
            }

            Console.ReadKey(intercept: true);
            Console.WriteLine("Traffic simulation ended");
        }

        private static void Usage() {
            Console.WriteLine("Usage: trafficgen <atc-service-url> <number-of-aircraft-to-simulate>");
            Console.WriteLine("");
        }

        private static void StartNewFlight(HttpClient client, Random randomGen) {
            var airports = Universe.Current.Airports;
            Airport departureAirport = airports[randomGen.Next(airports.Count)];
            Airport destinationAirport = null;
            do {
                destinationAirport = airports[randomGen.Next(airports.Count)];
            } while (departureAirport == destinationAirport);

            string callSignsStr = client.GetStringAsync("api/flights/callsigns").Result;
            IEnumerable<string> flyingAirplanes =JsonConvert.DeserializeObject<IEnumerable<string>>(callSignsStr);
            string newAirplaneCallSign = null;
            do {
                newAirplaneCallSign = "N" + randomGen.Next(1, 1000).ToString() + "SIM";
            } while (flyingAirplanes.Contains(newAirplaneCallSign));

            var flightPlan = new FlightPlan();
            flightPlan.CallSign = newAirplaneCallSign;
            flightPlan.DeparturePoint = departureAirport;
            flightPlan.Destination = destinationAirport;
            string serializedFlightPlan = JsonConvert.SerializeObject(flightPlan, Serialization.GetAtcSerializerSettings());
            client.PutAsync("api/flights", new StringContent(serializedFlightPlan, Encoding.UTF8, "application/json")).Wait();
        } 
    }
}
