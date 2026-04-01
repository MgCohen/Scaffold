using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;
using Unity.Services.CloudCode.Shared;
using Unity.Services.CloudSave.Model;

namespace GameModule.ModuleFetchData.Unity
{
    /// <summary>
    /// Represents server data configurations explicitly.
    /// </summary>
    public class UnityGameState : UnityDataCache, IGameState
    {
        private string _key = "GameState";

        public UnityGameState(ILogger<UnityDataCache> logger, IGameApiClient gameApiClient) : base(logger, gameApiClient)
        {
            _logger = logger;
            _gameApiClient = gameApiClient;
        }

        public override string GetDebugKey(string key)
        {
            return $"'{_key}'.'{key}'";
        }

        protected override async Task InitializeData(IExecutionContext context)
        {
            SetPlayerId(context.PlayerId);
            await Initialize(context);
        }

        protected override async Task<Dictionary<string, string>> FetchData(IExecutionContext context)
        {
            try
            {
                _logger.LogInformation($"[Player.GameState] --- Starting paginated fetch for key '{_key}'. ---");

                Dictionary<string, string> allFetchedData = new Dictionary<string, string>();
                string after = null;
                int pageNumber = 0;

                do
                {
                    pageNumber++;
                    ApiResponse<GetItemsResponse> result =
                        await _gameApiClient.CloudSaveData.GetPrivateCustomItemsAsync(context, context.ServiceToken,
                            context.ProjectId, _key, keys: null, after);

                    int itemsThisPage = result.Data?.Results?.Count ?? 0;
                    if (itemsThisPage > 0)
                    {
                        foreach (Item item in result.Data.Results)
                        {
                            allFetchedData[item.Key] = item.Value?.ToString() ?? string.Empty;
                        }
                    }

                    _logger.LogInformation($"[Player.GameState] Page {pageNumber}: Fetched {itemsThisPage} items. Total so far: {allFetchedData.Count}.");

                    string nextUrl = result.Data?.Links?.Next;
                    after = null;

                    if (!string.IsNullOrEmpty(nextUrl))
                    {
                        string afterMarker = "after=";
                        int afterIndex = nextUrl.IndexOf(afterMarker);
                        if (afterIndex != -1)
                        {
                            string cursorWithPotentialParams = nextUrl.Substring(afterIndex + afterMarker.Length);
                            after = cursorWithPotentialParams.Split('&')[0];
                        }
                    }
                }
                while (!string.IsNullOrEmpty(after));
                _logger.LogInformation($"[Player.GameState] --- Pagination complete for key '{_key}'. Total items fetched: {allFetchedData.Count}. ---");
                return allFetchedData;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[Player.GameState] An error occurred while fetching data for key '{_key}': {ex.Message}");
                throw;
            }
        }

        protected override async Task SaveData(IExecutionContext context, string key, object value, bool useWriteLock)
        {
            SetItemBody item = new SetItemBody(key, value);
            await _gameApiClient.CloudSaveData.SetPrivateCustomItemAsync(context, context.ServiceToken, context.ProjectId, _key, item);
        }

        protected override async Task DeleteData(IExecutionContext context, string key)
        {
            await _gameApiClient.CloudSaveData.DeletePrivateCustomItemAsync(context, context.ServiceToken, context.ProjectId, _key, key);
        }

        public async Task Delete(IExecutionContext context, string databaseKey, string key)
        {
            _key = databaseKey;
            await Delete(context, key);
        }

        protected override async Task SaveBatchData(IExecutionContext context, List<SetItemBody> values, bool useWriteLock)
        {
            SetItemBatchBody request = new SetItemBatchBody(values);
            await _gameApiClient.CloudSaveData.SetPrivateCustomItemBatchAsync(context, context.ServiceToken, context.ProjectId, _key, request);
        }

        //Deprecated since it only search for the first page
        protected async Task<T> GetGameValue<T>(IExecutionContext context, string databaseKey, string key)
        {
            _key = databaseKey;
            return await Get<T>(context, key, default);
        }

        public async Task<Dictionary<string, T>> GetAllGameValues<T>(IExecutionContext context, string key)
        {
            _key = key;
            return await GetAllValues<T>(context);
        }

        public async Task<T> GetAllGameValue<T>(IExecutionContext context, string databaseKey, string key)
        {
            Dictionary<string, T> gameValues = await GetAllGameValues<T>(context, databaseKey);
            _logger.LogInformation($"[GameState] Fetched all game values for player {context.PlayerId}, total count: {gameValues.Count}");
            //debug all values
            //string allValues = "";
            //foreach (var kvp in gameValues)
            //{
            //    allValues += $"Key: {kvp.Key}, Value: {kvp.Value}\n";
            //}
            //_logger.LogDebug($"[GameState] All game values:\n{allValues}");


            if (gameValues.TryGetValue(key, out T value))
            {
                return value;
            }
            return default;
        }

        public async Task Set(IExecutionContext context, string databaseKey, string key, object value, bool useWriteLock = false)
        {
            _key = databaseKey;
            await Set(context, key, value, useWriteLock);
        }
    }
}