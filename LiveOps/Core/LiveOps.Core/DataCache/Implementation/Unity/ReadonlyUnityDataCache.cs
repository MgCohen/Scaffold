using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;
using LiveOps.DTO.Json;

namespace LiveOps.ModuleFetchData
{
    /// <summary>
    /// Read-only Cloud Save or Remote Config cache (implements <see cref="IReadableDataCache" /> only).
    /// </summary>
    public abstract class ReadonlyUnityDataCache : IReadableDataCache
    {
        protected ReadonlyUnityDataCache(ILogger logger, IGameApiClient gameApiClient)
        {
            _logger = logger;
            _gameApiClient = gameApiClient;
        }

        protected ReadonlyUnityDataCache(ILogger logger, IGameApiClient gameApiClient, string playerId) : this(logger, gameApiClient)
        {
            _playerId = playerId;
        }

        protected string _playerId;
        protected string _accessToken;
        protected IExecutionContext? _lastContext;

        protected ILogger _logger;
        protected IGameApiClient _gameApiClient;

        protected Dictionary<string, string> _cache = new();
        protected Dictionary<string, object> _objectCache = new();

        protected abstract Task<Dictionary<string, string>> FetchData(IExecutionContext context);

        public string PlayerId => _playerId;

        protected void SetPlayerId(string playerId)
        {
            _playerId = playerId;
        }

        protected virtual async Task InitializeData(IExecutionContext context)
        {
            _lastContext = context;
            if (context.AccessToken != _accessToken)
            {
                if (string.IsNullOrEmpty(_playerId))
                {
                    SetPlayerId(context.PlayerId);
                }

                await Initialize(context).ConfigureAwait(false);
            }
        }

        public virtual string GetDebugKey(string key) => $"'{key}'";

        protected async Task Initialize(IExecutionContext context)
        {
            _cache = await FetchData(context).ConfigureAwait(false);
            _objectCache.Clear();
            _accessToken = context.AccessToken;
            _logger.LogDebug("[{Type}] Refreshed cache for player {PlayerId}", GetType().Name, _playerId);
        }

        public virtual async Task WarmupAsync(IExecutionContext context, IReadOnlyCollection<string>? keys = null)
        {
            if (keys is { Count: 0 })
            {
                return;
            }

            await InitializeData(context).ConfigureAwait(false);
        }

        public virtual async Task<T> Get<T>(IExecutionContext context, string key, T defaultValue)
        {
            await InitializeData(context).ConfigureAwait(false);
            if (_objectCache.TryGetValue(key, out object? cachedObj) && cachedObj is T cachedTyped)
            {
                _logger.LogDebug("[{Type}.Get] Key {Key} for player {PlayerId} hit _objectCache ({Clr}).", GetType().Name, GetDebugKey(key), _playerId, typeof(T).FullName);
                return cachedTyped;
            }

            if (_cache.TryGetValue(key, out string? value))
            {
                _logger.LogDebug("[{Type}.Get] Key {Key} for player {PlayerId} from backing cache ({Clr}).", GetType().Name, GetDebugKey(key), _playerId, typeof(T).FullName);
                T deserialized = value.FromJson<T>();
                _objectCache[key] = deserialized;
                return deserialized;
            }

            _logger.LogDebug("[{Type}.Get] Key {Key} for player {PlayerId} not found, default ({Clr}).", GetType().Name, GetDebugKey(key), _playerId, typeof(T).FullName);
            return defaultValue;
        }

        public virtual async Task<bool> Exists(IExecutionContext context, string key)
        {
            await InitializeData(context).ConfigureAwait(false);
            return _cache.ContainsKey(key) || _objectCache.ContainsKey(key);
        }
    }
}
