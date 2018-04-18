using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace atcsvc
{
    public class LoggingEvents
    {
        public const int TableStorageOpFailed = 1;
        public const int AirplaneSvcOpFailed = 2;
        public const int TimePassageHandlingFailed = 3;
        public const int StartingNewFlightFailed = 4;
        public const int StreamingAirplaneInformationFailed = 5;
        public const int AirplaneCountFailed = 6;

        public const int NewFlightCreated = 1000;
        public const int FlightLanded = 1001;
        public const int InstructionIssued = 1002;

        public const string DefaultFailedOperationMessage = "{Operation} failed";
    }
}
