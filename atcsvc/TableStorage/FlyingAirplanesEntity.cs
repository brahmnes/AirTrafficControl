using Microsoft.WindowsAzure.Storage.Table;
using Validation;

namespace atcsvc.TableStorage
{
    internal class FlyingAirplanesEntity: TableEntity
    {
        public FlyingAirplanesEntity(string callSign)
        {
            Requires.NotNull(callSign, nameof(callSign));

            this.PartitionKey = FlyingAirplanesTable.DefaultPartition;
            this.RowKey = callSign;
        }

        // Serialization constructor
        public FlyingAirplanesEntity() {
            this.PartitionKey = FlyingAirplanesTable.DefaultPartition;
            this.RowKey = "<invalid>";
        }

        public string CallSign => this.RowKey;
    }
}
