using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace atcsvc.TableStorage
{
    internal class WorldStateTable: CloudTableEx<WorldStateEntity>
    {
        public const string SingletonEntityKey = "default";

        public WorldStateTable(IConfiguration configuration): base(configuration, "WorldState")
        {
        }

        public async Task<WorldStateEntity> GetWorldStateAsync(CancellationToken cToken)
        {
            var entities = await GetAllEntitiesDefaultPartitionAsync(cToken);
            var eCount = entities.Count();

            if (eCount == 0)
            {
                var worldState = new WorldStateEntity();
                worldState.CurrentTime = 0;
                await InsertEntityAsync(worldState, cToken);
                return worldState;
            }
            else if (eCount == 1)
            {
                return entities.First();
            }
            else {
                throw new Exception("Only one WorldState entity is expected");
            }
        }

        public Task SetWorldStateAsync(WorldStateEntity worldState, CancellationToken cToken)
        {
            return InsertEntityAsync(worldState, cToken);
        }
    }
}
