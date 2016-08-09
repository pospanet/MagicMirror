﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using Mirror.Common.Configuration;

namespace Mirror.Common.MSAL
{
    public class AzureTableStoreTokenCache : TokenCache
    {
        private const string PartitionKey = "UserTokens";
        private const string UserTokenTableName = "UserTokens";
        private readonly ITokenCacheConfig _tokenCacheConfig;
        private readonly string _userId;
        private readonly CloudTable _tokenCacheTable;
        private TokenCacheEntity _tokenCacheEntity;

        private AzureTableStoreTokenCache(ITokenCacheConfig tokenCacheConfig, string userId, CloudTable tokenCacheTable)
        {
            _tokenCacheConfig = tokenCacheConfig;
            BeforeAccess = BeforeAccessNotification;
            AfterAccess = AfterAccessNotification;
            _userId = userId;
            _tokenCacheTable = tokenCacheTable;
        }

        private void AfterAccessNotification(TokenCacheNotificationArgs args)
        {
            TokenCacheEntity tokenCacheEntity = new TokenCacheEntity(PartitionKey, _userId);
            tokenCacheEntity.SetData(Serialize());
            TableOperation tokenCacheTableOperation = TableOperation.InsertOrReplace(tokenCacheEntity);
            Task<TableResult> tableOperationTask = _tokenCacheTable.ExecuteAsync(tokenCacheTableOperation);
            tableOperationTask.RunSynchronously();
        }

        private void BeforeAccessNotification(TokenCacheNotificationArgs args)
        {
            TableOperation tokenCacheTableOperation = TableOperation.Retrieve<TokenCacheEntity>(PartitionKey, _userId);
            Task<TableResult> tableOperationTask = _tokenCacheTable.ExecuteAsync(tokenCacheTableOperation);
            tableOperationTask.RunSynchronously();

            TableResult tokenRecords = tableOperationTask.Result;
            if (tokenRecords.Result == null)
            {
                throw new ArgumentOutOfRangeException(nameof(_userId),
                    string.Concat("No data found for User ID: ", _userId));
            }
            TokenCacheEntity tokenCacheEntity = (TokenCacheEntity)tokenRecords.Result;
            Deserialize(tokenCacheEntity.GetData());
            _tokenCacheEntity = tokenCacheEntity;
        }

        public override void Clear()
        {
            base.Clear();
            BeforeAccessNotification(null);
            TableOperation tokenCacheTableOperation = TableOperation.Delete(_tokenCacheEntity);
            Task<TableResult> tableOperationTask = _tokenCacheTable.ExecuteAsync(tokenCacheTableOperation);
            tableOperationTask.RunSynchronously();
        }

        public static async Task<AzureTableStoreTokenCache> GetTokenCacheAsync(ITokenCacheConfig tokenCacheConfig,
            string userId)
        {
            CloudTable tokenCacheTable = await GetTokenCacheTableAsync(tokenCacheConfig);
            AzureTableStoreTokenCache tokenCache = new AzureTableStoreTokenCache(tokenCacheConfig, userId, tokenCacheTable);
            return tokenCache;
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
        public TokenCacheEntity(string partition, string userName):this()
        {
            PartitionKey = partition;
            RowKey = userName;
        }

        public TokenCacheEntity()
        {
            InitData();
        }

        public byte[] DataChunk1 { get; set; }
        public byte[] DataChunk2 { get; set; }
        public byte[] DataChunk3 { get; set; }
        public byte[] DataChunk4 { get; set; }
        public byte[] DataChunk5 { get; set; }
        public byte[] DataChunk6 { get; set; }
        public byte[] DataChunk7 { get; set; }
        public byte[] DataChunk8 { get; set; }
        public byte[] DataChunk9 { get; set; }

        public byte[] GetData()
        {
            List<byte[]> tokenCacheDataList = new List<byte[]>
            {
                DataChunk1,
                DataChunk2,
                DataChunk3,
                DataChunk4,
                DataChunk5,
                DataChunk6,
                DataChunk7,
                DataChunk8,
                DataChunk9
            };
            return tokenCacheDataList.SelectMany(chunk => chunk).ToArray();
        }

        public void SetData(byte[] data)
        {
            InitData();
            DataChunk1 = data;
        }

        private void InitData()
        {
            DataChunk1 = new byte[0];
            DataChunk2 = new byte[0];
            DataChunk3 = new byte[0];
            DataChunk4 = new byte[0];
            DataChunk5 = new byte[0];
            DataChunk6 = new byte[0];
            DataChunk7 = new byte[0];
            DataChunk8 = new byte[0];
            DataChunk9 = new byte[0];
        }
    }
}