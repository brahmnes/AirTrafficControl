using Validation;

namespace AirTrafficControl.Interfaces
{
    public class AirplaneStateDto
    {
        // Parameterless constructor for deserialization
        public AirplaneStateDto() { }

        public AirplaneStateDto(AirplaneState airplaneState, FlightPlan flightPlan)
        {
            Requires.NotNull(airplaneState, nameof(airplaneState));
            Requires.NotNull(flightPlan, nameof(flightPlan));

            this.ID = flightPlan.AirplaneID;
            this.StateDescription = airplaneState.ToString();
            this.Location = airplaneState.Location;
            this.Heading = airplaneState.GetHeading(flightPlan);

            var enrouteState = airplaneState as EnrouteState;
            if (enrouteState != null)
            {
                this.EnrouteFrom = enrouteState.From;
                this.EnrouteTo = enrouteState.To;
            }
        }

        public string ID { get; set; }

        public string StateDescription { get; set; }

        public Location Location { get; set; }

        // Heading (in radians), 360 is zero and increases clockwise
        public double Heading { get; set; }


        // EnrouteTo and EnrouteFrom are only used when the airplane is enroute
        // They are needed to animate airplanes on the map
        public Fix EnrouteFrom { get; set; }

        public Fix EnrouteTo { get; set; }
    }
}
