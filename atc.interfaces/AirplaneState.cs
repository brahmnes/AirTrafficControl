﻿using System.Diagnostics;
using Validation;

namespace AirTrafficControl.Interfaces
{
    // Used for serialization hint--only concrete types have an entry
    public enum AirplaneStateType
    {
        Taxiing = 1, 
        Departing = 2,
        Holding = 3,
        Approach = 4,
        Landed = 5,
        Enroute = 6
    }

    public abstract class AirplaneState
    {
        public abstract Location Location { get; }

        public abstract AirplaneState ComputeNextState(FlightPlan flightPlan, AtcInstruction instruction);
        public abstract double GetHeading(FlightPlan flightPlan);
    }

    public abstract class AirportLocationState : AirplaneState
    {
        public AirportLocationState(Airport airport)
        {
            Requires.NotNull(airport, "airport");
            this.Airport = airport;
        }

        public Airport Airport { get; private set; }

        public override Location Location { get { return Airport.Location; } }
    }

    public abstract class FixLocationState : AirplaneState
    {
        public FixLocationState(Fix fix)
        {
            Requires.NotNull(fix, "fix");
            this.Fix = fix;
        }

        public Fix Fix { get; private set; }

        public override Location Location { get { return Fix.Location; } }
    }

    public class TaxiingState : AirportLocationState
    {
        public TaxiingState(Airport airport) : base(airport) { }

        public override AirplaneState ComputeNextState(FlightPlan flightPlan, AtcInstruction instruction)
        {
            FlightPlan.Validate(flightPlan);

            TakeoffClearance clearance = instruction as TakeoffClearance;            
            if (clearance == null || clearance.LocationOrLimit != this.Airport)
            {
                Debug.Assert(clearance == null && instruction == null, "We have received an unexpected instruction or a takeoff clearance for the wrong airport");
                return this;  // Waiting for takeoff clearance
            }
            else
            {
                var newState = new DepartingState(this.Airport);
                return newState;
            }
        }

        public override string ToString()
        {
            return "Taxiing at " + Airport.DisplayName;
        }

        public override double GetHeading(FlightPlan flightPlan)
        {
            return 0.0; // We do not track heading changes while taxiing.
        }
    }

    public class DepartingState : AirportLocationState
    {
        public DepartingState(Airport airport) : base(airport) { }

        public override AirplaneState ComputeNextState(FlightPlan flightPlan, AtcInstruction instruction)
        {
            FlightPlan.Validate(flightPlan);

            // Hold at the airport if we have received a holding instruction
            HoldInstruction holdInstruction = instruction as HoldInstruction;
            if (holdInstruction != null && holdInstruction.LocationOrLimit == this.Airport)
            {
                return new HoldingState(this.Airport);
            }
            else
            {
                Debug.Assert(flightPlan.FlightPath[0] == this.Airport);
                Fix next = flightPlan.FlightPath[1];
                return new EnrouteState(this.Airport, next);
            }
        }

        public override string ToString()
        {
            return "Departing from " + Airport.DisplayName;
        }

        public override double GetHeading(FlightPlan flightPlan)
        {
            return this.Airport.Location.GetDirectHeadingTo(flightPlan.Destination.Location);
        }
    }

    public class HoldingState : FixLocationState
    {
        public HoldingState(Fix fix) : base(fix) { }

        public override AirplaneState ComputeNextState(FlightPlan flightPlan, AtcInstruction instruction)
        {
            FlightPlan.Validate(flightPlan);

            ApproachClearance approachClearance = instruction as ApproachClearance;
            if (approachClearance != null)
            {
                if (this.Fix == approachClearance.LocationOrLimit)
                {
                    return new ApproachState(approachClearance.LocationOrLimit);
                }
            }

            EnrouteClearance enrouteClearance = instruction as EnrouteClearance;
            if (enrouteClearance != null)
            {
                Fix next = flightPlan.GetNextFix(this.Fix);

                if (enrouteClearance.IsClearedTo(this.Fix, next))
                {
                    return new EnrouteState(this.Fix, next);
                }
            }

            // By default continue holding
            return this;
        }

        public override string ToString()
        {
            return "Holding at " + Fix.DisplayName;
        }

        public override double GetHeading(FlightPlan flightPlan)
        {
            return flightPlan.DeparturePoint.Location.GetDirectHeadingTo(flightPlan.Destination.Location);
        }
    }

    public class ApproachState: AirportLocationState
    {
        public ApproachState(Airport airport) : base(airport) { }

        public override AirplaneState ComputeNextState(FlightPlan flightPlan, Interfaces.AtcInstruction instruction)
        {
            // Approaches are always successful, we never go missed :-)
            return new LandedState(this.Airport);
        }

        public override string ToString()
        {
            return "Flying approach to " + Airport.DisplayName;
        }

        public override double GetHeading(FlightPlan flightPlan)
        {
            return flightPlan.DeparturePoint.Location.GetDirectHeadingTo(flightPlan.Destination.Location);
        }
    }

    public class LandedState: AirportLocationState
    {
        public LandedState(Airport airport) : base(airport) { }

        public override AirplaneState ComputeNextState(FlightPlan flightPlan, Interfaces.AtcInstruction instruction)
        {
            // After landing the airplane just disappears from the world
            return null;
        }

        public override string ToString()
        {
            return "Landed at " + Airport.DisplayName;
        }

        public override double GetHeading(FlightPlan flightPlan)
        {
            return 0.0;
        }
    }

    public class EnrouteState: AirplaneState
    {
        public EnrouteState(Fix from, Fix to)
        {
            Requires.NotNull(from, "from");
            Requires.NotNull(to, "to");
            this.From = from;
            this.To = to;
        }


        public Fix From { get; private set; }

        public Fix To { get; private set; }

        public override Location Location
        {
            get
            {
                // Just an aproximation
                return new Location(
                    (this.To.Location.Latitude + this.From.Location.Latitude) / 2.0,
                    (this.To.Location.Longitude + this.From.Location.Longitude) / 2.0
                );
            }
        }

        public override string ToString()
        {
            return $"Flying from {From.DisplayName} to {To.DisplayName}";
        }

        public override AirplaneState ComputeNextState(FlightPlan flightPlan, AtcInstruction instruction)
        {
            FlightPlan.Validate(flightPlan);

            if (this.To == flightPlan.Destination)
            {
                ApproachClearance clearance = instruction as ApproachClearance;
                if (clearance != null && clearance.LocationOrLimit == flightPlan.Destination)
                {
                    return new ApproachState(flightPlan.Destination);
                }
                else
                {
                    return new HoldingState(flightPlan.Destination);
                }
            }
            else
            {
                Fix next = flightPlan.GetNextFix(this.To); 
                return new EnrouteState(this.To, next);
            }
        }

        public override double GetHeading(FlightPlan flightPlan)
        {
            return From.Location.GetDirectHeadingTo(To.Location);
        }
    }
}
