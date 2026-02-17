using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;
using Unity.Services.CloudCode.Shared;
using Unity.Services.CloudSave.Model;

namespace GameModule.ModuleFetchData
{
    public class PlayerData : DataCache
    {
        protected Dictionary<string, string> writeLockCache = new Dictionary<string, string>();

        public PlayerData(ILogger<PlayerData> logger, IGameApiClient gameApiClient) : base(logger, gameApiClient)
        {
            this.logger = logger;
            this.gameApiClient = gameApiClient;
        }
        
        public PlayerData(ILogger<PlayerData> logger, IGameApiClient gameApiClient, string playerId) : base(logger, gameApiClient, playerId)
        {
            this.logger = logger;
            this.gameApiClient = gameApiClient;
        }

        protected override async Task<Dictionary<string, string>> FetchData(IExecutionContext context)
        {
            try
            {
                ApiResponse<GetItemsResponse> result = await gameApiClient.CloudSaveData.GetProtectedItemsAsync(
                    context, context.ServiceToken, context.ProjectId, playerId);

                if (result.Data == null || result.Data.Results == null)
                {
                    return new Dictionary<string, string>();
                }
                writeLockCache = result.Data.Results.ToDictionary(item => item.Key, item => item.WriteLock);
                Dictionary<string, string> fetchedData = result.Data.Results
                    .ToDictionary(item => item.Key, item => item.Value?.ToString() ?? string.Empty);

                return fetchedData;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"[PlayerData.FetchData] An error occurred while fetching player data: PlayerId:'{playerId}'");
                throw;
            }
        }

        protected override async Task SaveData(IExecutionContext context, string key, object value, bool useWriteLock)
        {
            SetItemBody item = new SetItemBody(key, value);
            if (useWriteLock)
            {
                if (writeLockCache.TryGetValue(key, out string writeLock))
                {
                    item.WriteLock = writeLock;
                }
            }
            await gameApiClient.CloudSaveData.SetProtectedItemAsync(context, context.ServiceToken, context.ProjectId, playerId, item);
        }

        protected override async Task SaveBatchData(IExecutionContext context, List<SetItemBody> values, bool useWriteLock)
        {
            foreach (SetItemBody item in values)
            {
                if (useWriteLock)
                {
                    if (writeLockCache.TryGetValue(item.Key, out string writeLock))
                    {
                        item.WriteLock = writeLock;
                    } 
                }
            }
            SetItemBatchBody request = new SetItemBatchBody(values);
            await gameApiClient.CloudSaveData.SetProtectedItemBatchAsync(context, context.ServiceToken, context.ProjectId, playerId, request);
        }

        protected override async Task DeleteData(IExecutionContext context, string key)
        {
            await gameApiClient.CloudSaveData.DeleteProtectedItemAsync(context, context.ServiceToken, context.ProjectId, playerId, key);
        }

        public async Task Delete(IExecutionContext context, string key)
        {
            await DeleteData(context, key);
        }

        public string GetWriteLock(string key)
        {
            return writeLockCache.GetValueOrDefault(key, "");
        }
    }
}