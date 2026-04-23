using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;
using GameModuleDTO.Json;
using Unity.Services.CloudSave.Model;

namespace GameModule.ModuleFetchData
{
    /// <summary>
    /// Base abstraction for data structures.
    /// </summary>
    public abstract class UnityDataCache : IWriteableDataCache, IReadableDataCache
    {
        public UnityDataCache(ILogger logger, IGameApiClient gameApiClient)
        {
            _logger = logger;
            _gameApiClient = gameApiClient;
        }

        public UnityDataCache(ILogger logger, IGameApiClient gameApiClient, string playerId) : this(logger, gameApiClient)
        {
            _logger = logger;
            _gameApiClient = gameApiClient;
            _playerId = playerId;
        }

        protected string _playerId;
        protected string _accessToken;
        protected IExecutionContext? _lastContext;

        protected ILogger _logger;
        protected IGameApiClient _gameApiClient;

        protected Dictionary<string, string> _cache = new Dictionary<string, string>();
        protected Dictionary<string, object> _objectCache = new Dictionary<string, object>();

        private int _batchDepth;

        protected bool InBatch => _batchDepth > 0;

        protected abstract Task<Dictionary<string, string>> FetchData(IExecutionContext context);
        protected abstract Task SaveData(IExecutionContext context, string key, object value, bool useWriteLock);
        protected abstract Task SaveBatchData(IExecutionContext context, List<SetItemBody> values, bool useWriteLock);
        protected abstract Task DeleteData(IExecutionContext context, string key);

        public string PlayerId
        {
            get
            {
                return _playerId;
            }
        }

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

        public virtual string GetDebugKey(string key)
        {
            return $"'{key}'";
        }

        protected async Task Initialize(IExecutionContext context)
        {
            _cache = await FetchData(context).ConfigureAwait(false);
            _objectCache.Clear();
            _accessToken = context.AccessToken;
            _logger.LogInformation($"[{GetType().Name}] Refreshed cache for player {_playerId}");
        }

        public virtual async Task WarmupAsync(IExecutionContext context, IReadOnlyCollection<string>? keys = null)
        {
            if (keys != null && keys.Count == 0)
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
                _logger.LogInformation($"[{GetType().Name}.Get] Key {GetDebugKey(key)} for player: '{_playerId}' with cached value of type '{typeof(T).FullName}' from _cache.");
                return cachedTyped;
            }
            if (_cache.TryGetValue(key, out string value))
            {
                _logger.LogInformation($"[{GetType().Name}.Get] Key {GetDebugKey(key)} for player: '{_playerId}' with value: {value}' of type '{typeof(T).FullName}'.");
                T deserialized = value.FromJson<T>();
                _objectCache[key] = deserialized;
                return deserialized;
            }
            _logger.LogInformation($"[{GetType().Name}.Get] Key {GetDebugKey(key)} for player: '{_playerId}' not found of type '{typeof(T).FullName}', returning default value.");
            return defaultValue;
        }

        /// <summary>
        /// Updates in-memory object and persisted string cache (used after successful write or outside batch).
        /// </summary>
        private void InternalSetSynced(string key, object value)
        {
            _objectCache[key] = value;
            _cache[key] = value.ToJson();
            _logger.LogInformation($"[{GetType().Name}.Set] Saved key {GetDebugKey(key)} for player: '{_playerId}', value: {_cache[key]}.");
        }

        /// <summary>
        /// Updates only <see cref="_objectCache"/> so <see cref="FlushDirtyInternal"/> can detect dirty vs last persisted <see cref="_cache"/>.
        /// </summary>
        private void InternalSetPending(string key, object value)
        {
            _objectCache[key] = value;
            _logger.LogInformation($"[{GetType().Name}.Set] Pending key {GetDebugKey(key)} for player: '{_playerId}'.");
        }

        public virtual async Task Set(IExecutionContext context, string key, object value, bool useWriteLock = false)
        {
            await InitializeData(context).ConfigureAwait(false);
            if (InBatch)
            {
                InternalSetPending(key, value);
                return;
            }

            InternalSetSynced(key, value);
            await SaveData(context, key, value, useWriteLock).ConfigureAwait(false);
        }

        public virtual async Task Delete(IExecutionContext context, string key)
        {
            await InitializeData(context).ConfigureAwait(false);
            _cache.Remove(key);
            _objectCache.Remove(key);
            await DeleteData(context, key).ConfigureAwait(false);
        }

        public virtual Task FlushAsync(IExecutionContext context)
        {
            if (context == null)
            {
                return Task.CompletedTask;
            }

            return FlushDirtyInternal(context);
        }

        protected async Task FlushDirtyInternal(IExecutionContext context)
        {
            await InitializeData(context).ConfigureAwait(false);
            List<SetItemBody> dirty = null;
            foreach (KeyValuePair<string, object> kv in _objectCache)
            {
                if (kv.Value == null)
                {
                    continue;
                }

                string nowJson = kv.Value.ToJson() ?? string.Empty;
                if (!_cache.TryGetValue(kv.Key, out string thenJson) || thenJson != nowJson)
                {
                    _cache[kv.Key] = nowJson;
                    if (dirty == null)
                    {
                        dirty = new List<SetItemBody>();
                    }

                    dirty.Add(new SetItemBody(kv.Key, kv.Value));
                }
            }

            if (dirty != null && dirty.Count > 0)
            {
                await SaveBatchData(context, dirty, useWriteLock: true).ConfigureAwait(false);
            }
        }

        public virtual async Task<bool> Exists(IExecutionContext context, string key)
        {
            await InitializeData(context).ConfigureAwait(false);
            return _cache.ContainsKey(key) || _objectCache.ContainsKey(key);
        }

        public virtual IAsyncDisposable BeginBatch()
        {
            _batchDepth++;
            return new BatchHandle(this);
        }

        private sealed class BatchHandle : IAsyncDisposable
        {
            private readonly UnityDataCache _owner;
            private bool _disposed;

            public BatchHandle(UnityDataCache owner)
            {
                _owner = owner;
            }

            public async ValueTask DisposeAsync()
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                _owner._batchDepth--;
                if (_owner._batchDepth == 0 && _owner._lastContext != null)
                {
                    await _owner.FlushAsync(_owner._lastContext).ConfigureAwait(false);
                }
            }
        }
    }
}
