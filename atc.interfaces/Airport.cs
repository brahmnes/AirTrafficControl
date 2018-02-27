using System.Linq;
using Newtonsoft.Json;

namespace AirTrafficControl.Interfaces
{
    #pragma warning disable 0659

    [JsonObject(MemberSerialization.OptIn)]
    public class Airport: Fix
    {
        // Serialization constructor
        public Airport() { }

        public Airport(string name, string displayName, Direction publishedHoldBearing): base(name, displayName)
        {
            this.PublishedHoldBearing = publishedHoldBearing;
        }

        public Airport(string name, string displayName, Direction publishedHoldBearing, Location location) : base(name, displayName, location)
        { }

        public Direction PublishedHoldBearing { get; private set; }

        public override void AddUniverseInfo()
        {
            if (string.IsNullOrEmpty(DisplayName))
            {
                DisplayName = Universe.Current.Fixes.Where(f => f.Name == this.Name).Select(f => f.DisplayName).FirstOrDefault();
            }
        }
    }

    #pragma warning restore 0659
}
