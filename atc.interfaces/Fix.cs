using System;
using JsonSubTypes;
using Newtonsoft.Json;

namespace AirTrafficControl.Interfaces
{
    [JsonConverter(typeof(JsonSubtypes))]
    [JsonSubtypes.KnownSubTypeWithProperty(typeof(Airport), "PublishedHoldBearing")]
    [JsonObject(MemberSerialization.OptIn)]
    public class Fix
    {
        // Parameterless constructur for deserialization
        public Fix() { }

        public Fix(string name, string displayName)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException();
            }

            Name = name;
            DisplayName = displayName;
        }

        public Fix(string name, string displayName, Location location): this(name, displayName)
        {
            this.Location = location;
        }

        [JsonProperty]
        public string Name { get; set; }

        [JsonProperty]
        public string DisplayName { get; set; }

        [JsonProperty]
        public Location Location { get; set; }

        public override bool Equals(object obj)
        {
            Fix other = obj as Fix;
            if (other == null)
            {
                return false;
            }

            return this.Name == other.Name;
        }

        public static bool operator ==(Fix f1, Fix f2)
        {
            if (object.ReferenceEquals(f1, null))
            {
                return object.ReferenceEquals(f2, null);
            }

            return f1.Equals(f2);
        }

        public static bool operator !=(Fix f1, Fix f2)
        {
            if (object.ReferenceEquals(f1, null))
            {
                return !object.ReferenceEquals(f2, null);
            }

            return !f1.Equals(f2);
        }

        public override int GetHashCode()
        {
            return Name.GetHashCode();
        }

        public override string ToString()
        {
            return Name;
        }
    }
}
