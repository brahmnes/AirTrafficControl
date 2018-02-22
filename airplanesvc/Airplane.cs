using AirTrafficControl.Interfaces;

namespace airplanesvc
{
    public class Airplane
    {
        public AirplaneState State { get; set; }
        public FlightPlan FlightPlan { get; set; }
        public int DepartureTime { get; set; }
        public AtcInstruction Instruction { get; set; }
    }
}
