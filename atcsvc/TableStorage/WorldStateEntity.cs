using Microsoft.WindowsAzure.Storage.Table;
using Validation;

namespace atcsvc.TableStorage
{
    internal class WorldStateEntity : TableEntity
    {
        public WorldStateEntity()
        {
            Requires.NotNull(callSign, nameof(callSign));

            this.PartitionKey = "default";
            this.RowKey = callSign;
        }

        // Serialization constructor
        public FlyingAirplanesEntity() { }

        public string CallSign => this.RowKey;
    }
}
