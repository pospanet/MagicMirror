using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using MirrorManager.Web.MSAL.Configuration;

namespace MirrorManager.Web.MSAL
{
    public class AzureTableStoreTokenCache : TokenCache
    {
        private const string PartitionKey = "UserTokens";
        private const string UserTokenTableName = "UserTokens";
        private readonly CloudTable _tokenCacheTable;
        private readonly string _userId;
        private TokenCacheEntity _tokenCacheEntity;

        private AzureTableStoreTokenCache(string userId, CloudTable tokenCacheTable)
        {
            BeforeAccess = BeforeAccessNotification;
            AfterAccess = AfterAccessNotification;
            _userId = userId;
            _tokenCacheTable = tokenCacheTable;
        }

        public static AzureTableStoreTokenCache GetTokenCache(ITokenCacheConfig tokenCacheConfig,
            string userId)
        {
            Task<CloudTable> tokenCacheTableTask = GetTokenCacheTableAsync(tokenCacheConfig);
            if (!tokenCacheTableTask.IsCompleted)
            {
                tokenCacheTableTask.Wait();
            }
            AzureTableStoreTokenCache tokenCache = new AzureTableStoreTokenCache(userId, tokenCacheTableTask.Result);
            return tokenCache;
        }

        public static async Task<AzureTableStoreTokenCache> GetTokenCacheAsync(ITokenCacheConfig tokenCacheConfig,
            string userId)
        {
            CloudTable tokenCacheTable = await GetTokenCacheTableAsync(tokenCacheConfig);
            AzureTableStoreTokenCache tokenCache = new AzureTableStoreTokenCache(userId, tokenCacheTable);
            return tokenCache;
        }


        private void AfterAccessNotification(TokenCacheNotificationArgs args)
        {
            TokenCacheEntity tokenCacheEntity = new TokenCacheEntity(PartitionKey, _userId);
            tokenCacheEntity.Token = Serialize();
            TableOperation tokenCacheTableOperation = TableOperation.InsertOrReplace(tokenCacheEntity);
            Task<TableResult> tableOperationTask = _tokenCacheTable.ExecuteAsync(tokenCacheTableOperation);
            if (!tableOperationTask.IsCompleted)
            {
                tableOperationTask.Wait();
            }
        }

        private void BeforeAccessNotification(TokenCacheNotificationArgs args)
        {
            TableOperation tokenCacheTableOperation = TableOperation.Retrieve<TokenCacheEntity>(PartitionKey, _userId);
            Task<TableResult> tableOperationTask = _tokenCacheTable.ExecuteAsync(tokenCacheTableOperation);
            if (!tableOperationTask.IsCompleted)
            {
                tableOperationTask.Wait();
            }
            TableResult tokenRecords = tableOperationTask.Result;
            if (tokenRecords.Result == null)
            {
                _tokenCacheEntity = new TokenCacheEntity(PartitionKey, _userId);
            }
            else
            {
                TokenCacheEntity tokenCacheEntity = (TokenCacheEntity)tokenRecords.Result;
                Deserialize(tokenCacheEntity.Token);
                _tokenCacheEntity = tokenCacheEntity;
            }
        }

        public override void Clear()
        {
            base.Clear();
            BeforeAccessNotification(null);
            TableOperation tokenCacheTableOperation = TableOperation.Delete(_tokenCacheEntity);
            Task<TableResult> tableOperationTask = _tokenCacheTable.ExecuteAsync(tokenCacheTableOperation);
            if (!tableOperationTask.IsCompleted)
            {
                tableOperationTask.Wait();
            }
        }

        private static async Task<CloudTable> GetTokenCacheTableAsync(ITokenCacheConfig tokenCacheConfig)
        {
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(tokenCacheConfig.ConnectionString);
            CloudTableClient tableClient = storageAccount.CreateCloudTableClient();
            CloudTable tokenCacheTable = tableClient.GetTableReference(UserTokenTableName);
            await tokenCacheTable.CreateIfNotExistsAsync();
            return tokenCacheTable;
        }
    }

    internal class TokenCacheEntity : TableEntity
    {
        public string personId { get; set; }
        public TokenCacheEntity(string partition, string userName) : this()
        {
            PartitionKey = partition;
            RowKey = userName;
        }

        public TokenCacheEntity()
        {
            Token = new byte[0];
            personId = null;
        }

        public byte[] Token { get; set; }
    }
}