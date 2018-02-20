namespace AirTrafficControl.Interfaces
{
    #pragma warning disable 0659

    public class Airport: Fix
    {
        public Airport(string name, string displayName, Direction publishedHoldBearing): base(name, displayName)
        {
            this.PublishedHoldBearing = publishedHoldBearing;
        }

        public Airport(string name, string displayName, Direction publishedHoldBearing, Location location) : base(name, displayName, location)
        { }

        public Direction PublishedHoldBearing { get; private set; }
    }

    #pragma warning restore 0659
}
