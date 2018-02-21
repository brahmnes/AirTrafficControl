﻿using System;
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

        private bool existenceChecked_ = false;
        private CloudTable storageTable_;
        private TableQuery<TEntity> allEntitiesQuery_;

        protected CloudTableEx(IConfiguration configuration, string tableName)
        {
            Requires.NotNull(configuration, nameof(configuration));
            Requires.NotNullOrWhiteSpace(tableName, nameof(tableName));

            string storageAccountConnectionStringPrefix = configuration["AzureStorageConnectionString"];
            string storageAccountKey = configuration["AZURE_STORAGE_ACCOUNT_KEY"];
            string storageAccountConnectionString = $"{storageAccountConnectionStringPrefix};AccountKey={storageAccountKey}";
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(storageAccountConnectionString);

            var tableClient = storageAccount.CreateCloudTableClient();
            storageTable_ = tableClient.GetTableReference(tableName);

            allEntitiesQuery_ = new TableQuery<TEntity>()
                .Where(TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, DefaultPartition));

        }

        protected async Task<IEnumerable<TEntity>> GetAllEntitiesDefaultPartitionAsync(CancellationToken cToken)
        {
            await EnsureTableExistsAsync(cToken);

            var retval = new List<TEntity>();
            TableContinuationToken tableToken = null;

            do
            {
                // TODO: probably need to take a closer look at the request options & operation context
                var segment = await storageTable_.ExecuteQuerySegmentedAsync(allEntitiesQuery_, tableToken, null, null, cToken);
                tableToken = segment.ContinuationToken;
                retval.AddRange(segment);
            } while (tableToken != null && !cToken.IsCancellationRequested);

            if (cToken.IsCancellationRequested)
            {
                return Enumerable.Empty<TEntity>();
            }

            return retval;
        }

        protected async Task InsertEntityAsync(TEntity e, CancellationToken cToken)
        {
            Requires.NotNullAllowStructs(e, nameof(e));

            await EnsureTableExistsAsync(cToken);

            var insertOp = TableOperation.Insert(e);
            var result = await storageTable_.ExecuteAsync(insertOp, null, null, cToken);
            if (result.HttpStatusCode < 200 || result.HttpStatusCode >= 300)
            {
                var ex = new Exception("Entity insertion has failed");
                ex.Data.Add("OperationResult", result);
                throw ex;
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
    }
}