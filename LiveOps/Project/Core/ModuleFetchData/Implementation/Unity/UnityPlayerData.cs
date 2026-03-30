using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;
using Unity.Services.CloudCode.Shared;
using Unity.Services.CloudSave.Model;


namespace GameModule.ModuleFetchData.Unity
{
    /// <summary>
    /// Orchestrates user profile details gracefully.
    /// </summary>
    public class UnityPlayerData : UnityDataCache, IPlayerData
    {
        protected Dictionary<string, string> _writeLockCache = new Dictionary<string, string>();

        public UnityPlayerData(ILogger<UnityPlayerData> logger, IGameApiClient gameApiClient) : base(logger, gameApiClient)
        {
            _logger = logger;
            _gameApiClient = gameApiClient;
        }

        public UnityPlayerData(ILogger<UnityPlayerData> logger, IGameApiClient gameApiClient, string playerId) : base(logger, gameApiClient, playerId)
        {
            _logger = logger;
            _gameApiClient = gameApiClient;
        }

        protected override async Task<Dictionary<string, string>> FetchData(IExecutionContext context)
        {
            try
            {
                ApiResponse<GetItemsResponse> result = await _gameApiClient.CloudSaveData.GetProtectedItemsAsync(
                    context, context.ServiceToken, context.ProjectId, _playerId);

                if (result.Data == null || result.Data.Results == null)
                {
                    return new Dictionary<string, string>();
                }
                _writeLockCache = result.Data.Results.ToDictionary(item => item.Key, item => item.WriteLock);
                Dictionary<string, string> fetchedData = result.Data.Results
                    .ToDictionary(item => item.Key, item => item.Value?.ToString() ?? string.Empty);

                return fetchedData;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[Player.FetchData] An error occurred while fetching player data: PlayerId:'{_playerId}'");
                throw;
            }
        }

        protected override async Task SaveData(IExecutionContext context, string key, object value, bool useWriteLock)
        {
            SetItemBody item = new SetItemBody(key, value);
            if (useWriteLock)
            {
                if (_writeLockCache.TryGetValue(key, out string writeLock))
                {
                    item.WriteLock = writeLock;
                }
            }
            await _gameApiClient.CloudSaveData.SetProtectedItemAsync(context, context.ServiceToken, context.ProjectId, _playerId, item);
        }

        protected override async Task SaveBatchData(IExecutionContext context, List<SetItemBody> values, bool useWriteLock)
        {
            foreach (SetItemBody item in values)
            {
                if (useWriteLock)
                {
                    if (_writeLockCache.TryGetValue(item.Key, out string writeLock))
                    {
                        item.WriteLock = writeLock;
                    }
                }
            }
            SetItemBatchBody request = new SetItemBatchBody(values);
            await _gameApiClient.CloudSaveData.SetProtectedItemBatchAsync(context, context.ServiceToken, context.ProjectId, _playerId, request);
        }

        protected override async Task DeleteData(IExecutionContext context, string key)
        {
            await _gameApiClient.CloudSaveData.DeleteProtectedItemAsync(context, context.ServiceToken, context.ProjectId, _playerId, key);
        }

        public override async Task Delete(IExecutionContext context, string key)
        {
            _writeLockCache.Remove(key);
            await base.Delete(context, key);
        }

        public string GetWriteLock(string key)
        {
            return _writeLockCache.GetValueOrDefault(key, "");
        }
    }
}