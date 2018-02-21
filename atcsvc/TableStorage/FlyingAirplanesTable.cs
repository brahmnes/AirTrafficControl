using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using Validation;

namespace atcsvc.TableStorage
{
    internal class FlyingAirplanesTable
    {
        public static readonly string DefaultPartition = "default";

        private bool existenceChecked_ = false;
        private CloudTable storageTable_;
        private TableQuery<FlyingAirplanesEntity> allAirplanesQuery_;

        public FlyingAirplanesTable(IConfiguration configuration)
        {
            Requires.NotNull(configuration, nameof(configuration));

            string storageAccountConnectionStringPrefix = configuration["AzureStorageConnectionString"];
            string storageAccountKey = configuration["AZURE_STORAGE_ACCOUNT_KEY"];
            string storageAccountConnectionString = $"{storageAccountConnectionStringPrefix};AccountKey={storageAccountKey}";
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(storageAccountConnectionString);

            var tableClient = storageAccount.CreateCloudTableClient();
            storageTable_ = tableClient.GetTableReference("FlyingAirplanes");

            allAirplanesQuery_ = new TableQuery<FlyingAirplanesEntity>().Where(TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, DefaultPartition));

        }

        public async Task<IEnumerable<string>> GetFlyingAirplaneCallSigns(CancellationToken cToken) {
            if (!existenceChecked_)
            {
                await storageTable_.CreateIfNotExistsAsync();
                existenceChecked_ = true;
            }

            var retval = new List<string>();
            TableContinuationToken tableToken = null;

            do
            {
                // TODO: probably need to take a closer look at the request options & operation context
                // Also pass the cancellation token to the ExecuteQuerySegmentedAsync() method
                var segment = await storageTable_.ExecuteQuerySegmentedAsync(allAirplanesQuery_, tableToken);
                tableToken = segment.ContinuationToken;
                retval.AddRange(segment.Select(entity => entity.CallSign));
            } while (tableToken != null && !cToken.IsCancellationRequested);

            if (cToken.IsCancellationRequested)
            {
                return Enumerable.Empty<string>();
            }

            return retval;
        }
    }
}
