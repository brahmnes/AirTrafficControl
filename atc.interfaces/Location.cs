using System;

namespace AirTrafficControl.Interfaces
{
    public class Location
    {
        // Parameterless constructor for deserialization
        public Location() { }

        public Location(double latitude, double longitude) : this(latitude, longitude, null) { }

        public Location(double latitude, double longitude, double? altitude)
        {
            if (latitude < -90.0 || latitude > 90.0)
            {
                throw new ArgumentOutOfRangeException(nameof(latitude));
            }

            if (longitude < -180.0 || longitude > 180.0)
            {
                throw new ArgumentOutOfRangeException(nameof(longitude));
            }

            this.Latitude = latitude;
            this.Longitude = longitude;
            this.Altitude = altitude;
        }

        public double Latitude { get; set; }

        public double Longitude { get; set; }

        public double? Altitude { get; set; }

        public double GetDirectHeadingTo(Location another)
        {
            if (another == null) { throw new ArgumentNullException(nameof(another)); }

            double dLon = another.Longitude - this.Longitude;
            double dLat = another.Latitude - this.Latitude;
            double heading = Math.PI / 2.0 - Math.Atan2(dLat, dLon);
            if (heading < 0)
            {
                heading += 2 * Math.PI;
            }
            return heading;
        }
    }
}
