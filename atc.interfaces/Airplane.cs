using JsonSubTypes;
using Newtonsoft.Json;
using Validation;

namespace AirTrafficControl.Interfaces
{
    [JsonObject(MemberSerialization.OptIn)]
    public class Airplane
    {
        // Parameterless constructor for deserialization
        public Airplane() { }

        public Airplane(AirplaneState airplaneState, FlightPlan flightPlan)
        {
            Requires.NotNull(airplaneState, nameof(airplaneState));
            Requires.NotNull(flightPlan, nameof(flightPlan));

            AirplaneState = airplaneState;
            FlightPlan = flightPlan;
        }

        [JsonProperty]
        public AirplaneState AirplaneState { get; set; }

        [JsonProperty]
        public FlightPlan FlightPlan { get; set; }

        [JsonProperty]
        public int DepartureTime { get; set; }

        [JsonProperty]
        public AtcInstruction Instruction { get; set; }

        public string StateDescription => AirplaneState.ToString();


        // Heading (in radians), 360 is zero and increases clockwise
        public double Heading => AirplaneState.GetHeading(FlightPlan);


        // EnrouteTo and EnrouteFrom are only used when the airplane is enroute
        // They are needed to animate airplanes on the map
        public Fix EnrouteFrom => (AirplaneState as EnrouteState)?.From;

        public Fix EnrouteTo => (AirplaneState as EnrouteState)?.To;        
    }
}
