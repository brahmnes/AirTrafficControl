using JsonSubTypes;
using Newtonsoft.Json;

namespace AirTrafficControl.Interfaces
{
    public static class Serialization
    {
        public static JsonSerializerSettings GetAtcSerializerSettings()
        {
            var settings = new JsonSerializerSettings();
            settings.ApplyAtcSerializerSettings();
            return settings;
        }

        public static JsonSerializerSettings ApplyAtcSerializerSettings(this JsonSerializerSettings settings)
        {
            settings.Converters.Add(JsonSubtypesConverterBuilder
                .Of(typeof(AirplaneState), "AirplaneStateType")
                .RegisterSubtype(typeof(TaxiingState), AirplaneStateType.Taxiing)
                .RegisterSubtype(typeof(DepartingState), AirplaneStateType.Departing)
                .RegisterSubtype(typeof(HoldingState), AirplaneStateType.Holding)
                .RegisterSubtype(typeof(ApproachState), AirplaneStateType.Approach)
                .RegisterSubtype(typeof(LandedState), AirplaneStateType.Landed)
                .RegisterSubtype(typeof(EnrouteState), AirplaneStateType.Enroute)
                .SerializeDiscriminatorProperty()
                .Build());

            settings.Converters.Add(JsonSubtypesConverterBuilder
                .Of(typeof(AtcInstruction), "AtcInstructionType")
                .RegisterSubtype(typeof(TakeoffClearance), AtcInstructionType.TakeoffClearance)
                .RegisterSubtype(typeof(HoldInstruction), AtcInstructionType.Hold)
                .RegisterSubtype(typeof(EnrouteClearance), AtcInstructionType.EnrouteClearance)
                .RegisterSubtype(typeof(ApproachClearance), AtcInstructionType.ApproachClearance)
                .SerializeDiscriminatorProperty()
                .Build());

            return settings;
        }
    }
}
