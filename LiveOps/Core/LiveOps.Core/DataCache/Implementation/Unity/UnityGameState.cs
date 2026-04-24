using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;
using Unity.Services.CloudCode.Shared;
using Unity.Services.CloudSave.Model;

namespace LiveOps.ModuleFetchData.Unity
{
    /// <summary>
    /// Cloud Save "custom database" namespace (see <c>GetPrivateCustomItemsAsync</c> <paramref name="databaseKey" />) with isolated in-memory state per database key.
    /// </summary>
    public class UnityGameState : UnityDataCache, IGameState
    {
        private const string DefaultDatabaseKey = "GameState";
        private string _activeDatabaseKey = DefaultDatabaseKey;

        private readonly Dictionary<string, NamespaceState> _namespaces = new(StringComparer.Ordinal);
        private string? _activeAccessToken;
        private readonly HashSet<string> _fetchedKeysForToken = new(StringComparer.Ordinal);

        private sealed class NamespaceState
        {
            public readonly Dictionary<string, string> Cache = new(StringComparer.Ordinal);
            public readonly Dictionary<string, object> ObjectCache = new(StringComparer.Ordinal);
        }

        public UnityGameState(ILogger<UnityDataCache> logger, IGameApiClient gameApiClient) : base(logger, gameApiClient)
        {
        }

        public override string GetDebugKey(string key)
        {
            return $"'{_activeDatabaseKey}'.'{key}'";
        }

        public override async Task<T> Get<T>(IExecutionContext context, string key, T defaultValue)
        {
            await EnsureNamespaceAsync(context, DefaultDatabaseKey).ConfigureAwait(false);
            return await base.Get(context, key, defaultValue).ConfigureAwait(false);
        }

        public override async Task Set(IExecutionContext context, string key, object value, bool useWriteLock = false)
        {
            await EnsureNamespaceAsync(context, DefaultDatabaseKey).ConfigureAwait(false);
            await base.Set(context, key, value, useWriteLock).ConfigureAwait(false);
        }

        public override async Task Delete(IExecutionContext context, string key)
        {
            await EnsureNamespaceAsync(context, DefaultDatabaseKey).ConfigureAwait(false);
            await base.Delete(context, key).ConfigureAwait(false);
        }

        public override async Task<bool> Exists(IExecutionContext context, string key)
        {
            await EnsureNamespaceAsync(context, DefaultDatabaseKey).ConfigureAwait(false);
            return await base.Exists(context, key).ConfigureAwait(false);
        }

        public override async Task WarmupAsync(IExecutionContext context, IReadOnlyCollection<string>? keys = null)
        {
            await EnsureNamespaceAsync(context, DefaultDatabaseKey).ConfigureAwait(false);
            await base.WarmupAsync(context, keys).ConfigureAwait(false);
        }

        public async Task<T> Get<T>(IExecutionContext context, string databaseKey, string itemKey, T defaultValue)
        {
            await EnsureNamespaceAsync(context, databaseKey).ConfigureAwait(false);
            return await base.Get(context, itemKey, defaultValue).ConfigureAwait(false);
        }

        public async Task Set(IExecutionContext context, string databaseKey, string key, object value, bool useWriteLock = false)
        {
            await EnsureNamespaceAsync(context, databaseKey).ConfigureAwait(false);
            await base.Set(context, key, value, useWriteLock).ConfigureAwait(false);
        }

        public async Task Delete(IExecutionContext context, string databaseKey, string key)
        {
            await EnsureNamespaceAsync(context, databaseKey).ConfigureAwait(false);
            await base.Delete(context, key).ConfigureAwait(false);
        }

        private async Task EnsureNamespaceAsync(IExecutionContext context, string databaseKey)
        {
            if (string.IsNullOrEmpty(databaseKey))
            {
                databaseKey = DefaultDatabaseKey;
            }

            if (_activeAccessToken != context.AccessToken)
            {
                _activeAccessToken = context.AccessToken;
                _fetchedKeysForToken.Clear();
                _accessToken = null;
                _namespaces.Clear();
            }

            _activeDatabaseKey = databaseKey;
            if (!_namespaces.TryGetValue(databaseKey, out NamespaceState? ns))
            {
                ns = new NamespaceState();
                _namespaces[databaseKey] = ns;
            }

            _cache = ns.Cache;
            _objectCache = ns.ObjectCache;
            if (!_fetchedKeysForToken.Contains(databaseKey))
            {
                if (string.IsNullOrEmpty(_playerId))
                {
                    SetPlayerId(context.PlayerId);
                }

                _lastContext = context;
                await Initialize(context).ConfigureAwait(false);
                _fetchedKeysForToken.Add(databaseKey);
            }
        }

        /// <summary>
        /// Fetches the custom database identified by the current <see cref="_activeDatabaseKey" />.
        /// </summary>
        protected override async Task<Dictionary<string, string>> FetchData(IExecutionContext context)
        {
            try
            {
                _logger.LogInformation("[Player.GameState] --- Starting paginated fetch for key '{Key}'. ---", _activeDatabaseKey);

                Dictionary<string, string> allFetchedData = new(StringComparer.Ordinal);
                string? after = null;
                int pageNumber = 0;

                do
                {
                    pageNumber++;
                    ApiResponse<GetItemsResponse> result =
                        await _gameApiClient.CloudSaveData.GetPrivateCustomItemsAsync(context, context.ServiceToken,
                            context.ProjectId, _activeDatabaseKey, keys: null, after);

                    int itemsThisPage = result.Data?.Results?.Count ?? 0;
                    if (itemsThisPage > 0)
                    {
                        foreach (Item item in result.Data.Results)
                        {
                            allFetchedData[item.Key] = item.Value?.ToString() ?? string.Empty;
                        }
                    }

                    _logger.LogInformation("[Player.GameState] Page {Page}: Fetched {Count} items. Total so far: {Total}.", pageNumber, itemsThisPage, allFetchedData.Count);

                    string? nextUrl = result.Data?.Links?.Next;
                    after = null;

                    if (!string.IsNullOrEmpty(nextUrl))
                    {
                        const string afterMarker = "after=";
                        int afterIndex = nextUrl.IndexOf(afterMarker, StringComparison.Ordinal);
                        if (afterIndex != -1)
                        {
                            string cursorWithPotentialParams = nextUrl[(afterIndex + afterMarker.Length)..];
                            after = cursorWithPotentialParams.Split('&')[0];
                        }
                    }
                }
                while (!string.IsNullOrEmpty(after));
                _logger.LogInformation("[Player.GameState] --- Pagination complete for key '{Key}'. Total items: {Count}. ---", _activeDatabaseKey, allFetchedData.Count);
                return allFetchedData;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Player.GameState] Error while fetching data for key '{Key}': {Message}", _activeDatabaseKey, ex.Message);
                throw;
            }
        }

        protected override async Task SaveData(IExecutionContext context, string key, object value, bool useWriteLock)
        {
            SetItemBody item = new SetItemBody(key, value);
            await _gameApiClient.CloudSaveData.SetPrivateCustomItemAsync(context, context.ServiceToken, context.ProjectId, _activeDatabaseKey, item);
        }

        protected override async Task DeleteData(IExecutionContext context, string key)
        {
            await _gameApiClient.CloudSaveData.DeletePrivateCustomItemAsync(context, context.ServiceToken, context.ProjectId, _activeDatabaseKey, key);
        }

        protected override async Task SaveBatchData(IExecutionContext context, List<SetItemBody> values, bool useWriteLock)
        {
            SetItemBatchBody request = new SetItemBatchBody(values);
            await _gameApiClient.CloudSaveData.SetPrivateCustomItemBatchAsync(context, context.ServiceToken, context.ProjectId, _activeDatabaseKey, request);
        }
    }
}
