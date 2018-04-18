using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using Validation;

namespace atcsvc.TableStorage
{
    internal class CloudTableEx<TEntity> where TEntity : ITableEntity, new()
    {
        public const string DefaultPartition = "default";
        private const string ConnectionStringEnvVariable = "AZURE_STORAGE_CONNECTION_STRING";

        private bool existenceChecked_ = false;
        private CloudTable storageTable_;
        private TableQuery<TEntity> allEntitiesQuery_;
        private TableQuery<TEntity> allEntitiesPartitionKeyOnlyQuery_;
        private IConfiguration configuration_;
        private string tableName_;
        private object instanceLock_;

        protected CloudTableEx(IConfiguration configuration, string tableName)
        {
            Requires.NotNull(configuration, nameof(configuration));
            Requires.NotNullOrWhiteSpace(tableName, nameof(tableName));

            instanceLock_ = new object();
            configuration_ = configuration;
            tableName_ = tableName;
            allEntitiesQuery_ = new TableQuery<TEntity>()
                .Where(TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, DefaultPartition));
            allEntitiesPartitionKeyOnlyQuery_ = new TableQuery<TEntity>()
                .Where(TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, DefaultPartition))
                .Select(new string[] {"PartitionKey"});
            
        }

        public virtual bool HasValidConfiguration() {
            string storageAccountConnectionString = configuration_[ConnectionStringEnvVariable];
            return CloudStorageAccount.TryParse(storageAccountConnectionString, out CloudStorageAccount ignored);
        }

        protected Task<IEnumerable<TEntity>> GetAllEntitiesDefaultPartitionAsync(CancellationToken cToken)
        {
            return ExecuteQueryAsync(allEntitiesQuery_, cToken);
        }

        protected async Task<int> GetEntityCountDefaultPartitionAsync(CancellationToken cToken)
        {
            var entityTags = await ExecuteQueryAsync(allEntitiesPartitionKeyOnlyQuery_, cToken);
            return entityTags.Count();
        }

        protected async Task InsertEntityAsync(TEntity e, CancellationToken cToken)
        {
            Requires.NotNullAllowStructs(e, nameof(e));

            EnsureStorageTable();
            await EnsureTableExistsAsync(cToken);

            var insertOp = TableOperation.Insert(e);
            var result = await storageTable_.ExecuteAsync(insertOp, null, null, cToken);
            CheckResult(result, "Entity insertion has failed", e);
        }

        protected async Task UpdateEntityAsync(TEntity e, CancellationToken cToken)
        {
            Requires.NotNullAllowStructs(e, nameof(e));

            EnsureStorageTable();
            var updateOp = TableOperation.Replace(e);
            var result = await storageTable_.ExecuteAsync(updateOp, null, null, cToken);
            CheckResult(result, "Entity update has failed", e);
        }

        protected async Task DeleteEntityAsync(TEntity e, CancellationToken cToken)
        {
            Requires.NotNullAllowStructs(e, nameof(e));

            EnsureStorageTable();
            if (string.IsNullOrEmpty(e.ETag))
            {
                e.ETag = "*"; // Means "any version of the entity"
            }
            var deleteOp = TableOperation.Delete(e);
            var result = await storageTable_.ExecuteAsync(deleteOp, null, null, cToken);
            CheckResult(result, "Entity deletion has failed", e);
        }

        protected async Task DeleteAllEntitiesDefaultPartitionAsync(CancellationToken cToken)
        {
            EnsureStorageTable();

            var entities = await GetAllEntitiesDefaultPartitionAsync(cToken);
            if (!entities.Any())
            {
                return;
            }

            var batch = new TableBatchOperation();
            foreach(var e in entities)
            {
                batch.Add(TableOperation.Delete(e));
            }
            await Task.WhenAll(storageTable_.ExecuteBatchAsync(batch, null, null, cToken));
        }

        // We defer the creation of storage table object to just before its use 
        // (instead of creating it in the constructor) to ensure that CloudTableEx instances
        // can be constructed even if the configuration is invalid
        private void EnsureStorageTable() {
            lock (instanceLock_) {
                if (storageTable_ == null) {
                    string storageAccountConnectionString = configuration_[ConnectionStringEnvVariable];
                    CloudStorageAccount storageAccount = CloudStorageAccount.Parse(storageAccountConnectionString);

                    var tableClient = storageAccount.CreateCloudTableClient();
                    storageTable_ = tableClient.GetTableReference(tableName_);
                }
            }
        }

        private async Task EnsureTableExistsAsync(CancellationToken cToken)
        {
            if (!existenceChecked_)
            {
                await storageTable_.CreateIfNotExistsAsync(null, null, cToken);
                existenceChecked_ = true;
            }
        }

        private void CheckResult(TableResult result, string errorMessage, TEntity e)
        {
            if (result.HttpStatusCode < 200 || result.HttpStatusCode >= 300)
            {
                var ex = new Exception(errorMessage);
                ex.Data.Add("TableResult", result);
                ex.Data.Add("Entity", e);
                throw ex;
            }
        }

        private async Task<IEnumerable<TEntity>> ExecuteQueryAsync(TableQuery<TEntity> query, CancellationToken cToken)
        {
            EnsureStorageTable();
            await EnsureTableExistsAsync(cToken);

            var retval = new List<TEntity>();
            TableContinuationToken tableToken = null;

            do {
                // TODO: probably need to take a closer look at the request options & operation context
                var segment = await storageTable_.ExecuteQuerySegmentedAsync(query, tableToken, null, null, cToken);
                tableToken = segment.ContinuationToken;
                retval.AddRange(segment);
            } while (tableToken != null && !cToken.IsCancellationRequested);

            if (cToken.IsCancellationRequested) {
                return Enumerable.Empty<TEntity>();
            }

            return retval;
        }
    }
}
