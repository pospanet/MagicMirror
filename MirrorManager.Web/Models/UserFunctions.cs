using Microsoft.Extensions.Configuration;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using MirrorManager.Web.MSAL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MirrorManager.Web.Models
{
    public class UserFunctions
    {
        private readonly CloudTable _tokenCache = null;
        private const string PartitionKey = "UserTokens";
        private const string UserTokenTableName = "UserTokens";
        public UserFunctions(IConfiguration configuration)
        {
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(configuration["TOKEN_STORAGE"]);
            CloudTableClient tableClient = storageAccount.CreateCloudTableClient();
            CloudTable tokenCacheTable = tableClient.GetTableReference(UserTokenTableName);
            tokenCacheTable.CreateIfNotExistsAsync().Wait();
            _tokenCache = tokenCacheTable;
        }
        public async Task<string> getPersonIdAsync(string userId)
        {
            TableOperation retrieveOperation = TableOperation.Retrieve<TokenCacheEntity>(PartitionKey, userId);
            TableResult retrievedResult = await _tokenCache.ExecuteAsync(retrieveOperation);

            return ((TokenCacheEntity)retrievedResult.Result).personId;
        }

        public async Task setPersonIdAsync(string userId, string faceId)
        {
            TableOperation retrieveOperation = TableOperation.Retrieve<TokenCacheEntity>(PartitionKey, userId);
            TableResult retrievedResult = await _tokenCache.ExecuteAsync(retrieveOperation);
            TokenCacheEntity entity = (TokenCacheEntity)retrievedResult.Result;

            TableOperation insertOrReplaceOperation = TableOperation.InsertOrReplace(entity);
            await _tokenCache.ExecuteAsync(insertOrReplaceOperation);
        }
    }
}
