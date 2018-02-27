using System.Collections.Generic;
using Validation;

namespace AirTrafficControl.Interfaces
{
    // Used for serialization hint--only concrete types have an entry
    public enum AtcInstructionType
    {
        TakeoffClearance = 1,
        Hold = 2,
        EnrouteClearance = 3,
        ApproachClearance = 4
    }

    public abstract class AtcInstruction
    {
        // Serialization constructor
        public AtcInstruction() { }

        public AtcInstruction(Fix locationOrLimit)
        {
            Requires.NotNull(locationOrLimit, "locationOrLimit");
            this.LocationOrLimit = locationOrLimit;
        }

        public Fix LocationOrLimit { get; set; }

        public virtual void AddUniverseInfo()
        {
            LocationOrLimit.AddUniverseInfo();
        }
    }

    public abstract class AirportFixAtcInstruction: AtcInstruction
    {
        public AirportFixAtcInstruction() { }

        public AirportFixAtcInstruction(Airport airport) : base(airport) { this.LocationOrLimit = airport; }

        public new Airport LocationOrLimit { get; set; }

        public override void AddUniverseInfo()
        {
            LocationOrLimit.AddUniverseInfo();
        }
    }

    public class TakeoffClearance: AirportFixAtcInstruction
    {
        public TakeoffClearance() { } 

        public TakeoffClearance(Airport airport) : base(airport) { }

        public override string ToString()
        {
            AddUniverseInfo();
            return "Cleared for takeoff at " + LocationOrLimit.DisplayName;
        }
    }

    public class HoldInstruction: AtcInstruction
    {
        public HoldInstruction() { }

        public HoldInstruction(Fix fix) : base(fix) { }

        public override string ToString()
        {
            AddUniverseInfo();
            return "Hold at " + LocationOrLimit.DisplayName;
        }
    }

    public class EnrouteClearance: AtcInstruction
    {
        public IList<Fix> FlightPath { get; set; }

        public EnrouteClearance() { }

        public EnrouteClearance(Fix limit, IList<Fix> flightPath) : base(limit)
        {
            Requires.NotNull(flightPath, nameof(flightPath));
            this.FlightPath = flightPath;
            Requires.ValidState(flightPath.Contains(limit), "The flight path must contain the clearance limit");
        }

        public override string ToString()
        {
            AddUniverseInfo();
            return "Cleared to " + LocationOrLimit.DisplayName;
        }

        public bool IsClearedTo(Fix current, Fix target)
        {
            Requires.NotNull(current, nameof(current));
            Requires.NotNull(target, nameof(target));
            Requires.That(this.FlightPath.Contains(current), nameof(current), "The 'current' fix is not part of the flight path");
            Requires.That(this.FlightPath.Contains(target), nameof(target), "The 'target' fix is not part of the flight path");
            Assumes.False(current == target, "Current fix cannot be the same as the target fix");

            int limitIndex = this.FlightPath.IndexOf(this.LocationOrLimit);
            int currentIndex = this.FlightPath.IndexOf(current);
            int targetIndex = this.FlightPath.IndexOf(target);

            bool sameDirection = currentIndex < targetIndex;
            bool notGoingBeyondTheLimit = targetIndex <= limitIndex;
            return sameDirection && notGoingBeyondTheLimit;
        }

        public override void AddUniverseInfo()
        {
            base.AddUniverseInfo();
            if (FlightPath != null)
            {
                foreach (var fix in FlightPath)
                {
                    fix.AddUniverseInfo();
                }
            }
        }
    }

    public class ApproachClearance: AirportFixAtcInstruction
    {
        public ApproachClearance() { }
        public ApproachClearance(Airport airport) : base(airport) { }

        public override string ToString()
        {
            return "Cleared for the approach to " + LocationOrLimit.DisplayName;
        }
    }
}
