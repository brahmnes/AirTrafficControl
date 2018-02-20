using System.Collections.Generic;

namespace AirTrafficControl.Interfaces
{
    public class FlightStatusModel
    {
        // Parameterless constructor for deserialization
        public FlightStatusModel() { }

        public IEnumerable<AirplaneStateDto> AirplaneStates { get; set; }

        public double EstimatedNextStatusUpdateDelayMsec { get; set; }
    }
}
