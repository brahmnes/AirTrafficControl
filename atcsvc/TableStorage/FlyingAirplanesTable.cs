using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Validation;

namespace atcsvc.TableStorage
{
    internal class FlyingAirplanesTable: CloudTableEx<FlyingAirplanesEntity>
    {
        public FlyingAirplanesTable(IConfiguration configuration): base(configuration, "FlyingAirplanes")
        {
        }

        public async Task<IEnumerable<string>> GetFlyingAirplaneCallSignsAsync(CancellationToken cToken) {
            var entities = await base.GetAllEntitiesDefaultPartitionAsync(cToken);
            return entities.Select(e => e.CallSign);
        }

        public Task DeleteFlyingAirplaneCallSignAsync(string callSign, CancellationToken cToken)
        {
            Requires.NotNullOrWhiteSpace(callSign, nameof(callSign));

            var entity = new FlyingAirplanesEntity(callSign);
            return DeleteEntityAsync(entity, cToken);
        }

        public Task AddFlyingAirplaneCallSignAsync(string callSign, CancellationToken cToken)
        {
            Requires.NotNullOrWhiteSpace(callSign, nameof(callSign));

            var entity = new FlyingAirplanesEntity(callSign);
            return InsertEntityAsync(entity, cToken);
        }

        public Task DeleteAllFlyingAirplaneCallSignsAsync(CancellationToken cToken)
        {
            return DeleteAllEntitiesDefaultPartitionAsync(cToken);
        }
    }
}
