using Microsoft.WindowsAzure.Storage.Table;
using Validation;

namespace atcsvc.TableStorage
{
    internal class WorldStateEntity : TableEntity
    {
        public WorldStateEntity()
        {
            this.PartitionKey = WorldStateTable.DefaultPartition;
            this.RowKey = WorldStateTable.SingletonEntityKey;
        }

        public int CurrentTime { get; set; }
    }
}
