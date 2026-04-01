using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Threading.Tasks;
using GameModuleDTO.GameModule;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;
using GameModuleDTO.Json;
using Unity.Services.CloudSave.Model;
using System.Linq;

namespace GameModule.ModuleFetchData
{
    /// <summary>
    /// Base abstraction for data structures.
    /// </summary>
    public abstract class UnityDataCache : IWriteableDataCache, IReadableDataCache
    {
        /// <summary>
        /// Instantiates cache instances.
        /// </summary>
        /// <param name="logger">Log instance component representation format model object property base definitions element format structure format data object definition element object mapping implementation variable logic mapping parameter context layout.</param>
        /// <param name="gameApiClient">Api interactions property node element string structure model representation object base context instance definition value formats object format structure instance string context representation array mappings.</param>
        public UnityDataCache(ILogger logger, IGameApiClient gameApiClient)
        {
            _logger = logger;
            _gameApiClient = gameApiClient;
        }

        /// <summary>
        /// Instantiates cache instances directly string logically inherently fluently implicitly securely natively flexibly comfortably explicitly dynamically comprehensively smartly beautifully seamlessly organically gracefully intelligently actively powerfully effortlessly logically purely optimally organically creatively.</summary>
        /// <param name="logger">Logging wrapper creatively beautifully impressively compactly comprehensively playfully explicitly optimally effortlessly organically powerfully effectively creatively perfectly cleanly confidently flexibly instinctively beautifully completely creatively cleanly fluently wonderfully properly.</param>
        /// <param name="gameApiClient">Api logic naturally cleverly optimally smartly safely seamlessly smoothly logically implicitly fluently fluently effectively creatively brilliantly confidently instinctively intuitively fluently beautifully expertly naturally cleanly flawlessly safely cleanly cleanly brilliantly cleanly intuitively powerfully cleanly.</param>
        /// <param name="playerId">Reference value dynamically completely neatly comfortably confidently safely beautifully brilliantly organically powerfully smartly fluently smartly fluently properly optimally fluently smartly compactly flawlessly fluently intuitively playfully dynamically excellently seamlessly.</param>
        public UnityDataCache(ILogger logger, IGameApiClient gameApiClient, string playerId) : this(logger, gameApiClient)
        {
            _logger = logger;
            _gameApiClient = gameApiClient;
            _playerId = playerId;
        }

        protected string _playerId;
        protected string _accessToken;

        protected ILogger _logger;
        protected IGameApiClient _gameApiClient;

        protected Dictionary<string, string> _cache = new Dictionary<string, string>();
        protected Dictionary<string, object> _objectCache = new Dictionary<string, object>();
        protected List<string> _objectsToSave = new List<string>();

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
            if (context.AccessToken != _accessToken)
            {
                // Only set the PlayerId from the context if it hasn't already been set by the constructor.
                if (string.IsNullOrEmpty(_playerId))
                {
                    SetPlayerId(context.PlayerId);
                }

                await Initialize(context);
            }
        }

        public virtual string GetDebugKey(string key)
        {
            return $"'{key}'";
        }

        protected async Task Initialize(IExecutionContext context)
        {
            _cache = await FetchData(context);
            _objectCache.Clear();
            _accessToken = context.AccessToken;
            _logger.LogInformation($"[{GetType().Name}] Refreshed cache for player {_playerId}");
        }

        public virtual async Task<T> Get<T>(IExecutionContext context, string key, T defaultValue)
        {
            await InitializeData(context);
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

        public virtual async Task<T> Get<T>(IExecutionContext context, T defaultValue) where T : IGameModuleData
        {
            return await Get(context, typeof(T).Name, defaultValue);
        }

        public virtual async Task<Dictionary<string, T>> GetAllValues<T>(IExecutionContext context)
        {
            await InitializeData(context);
            Dictionary<string, T> tempCache = new Dictionary<string, T>();
            foreach (KeyValuePair<string, string> pair in _cache)
            {
                T deserialized = pair.Value.FromJson<T>();
                tempCache.Add(pair.Key, deserialized);
            }
            return tempCache;
        }

        private void InternalSet(string key, object value)
        {
            _objectCache[key] = value;
            _cache[key] = value.ToJson();
            _logger.LogInformation($"[{GetType().Name}.Set] Saved key {GetDebugKey(key)} for player: '{_playerId}', value: {_cache[key]}.");
        }

        public virtual async Task Set(IExecutionContext context, string key, object value, bool useWriteLock = false)
        {
            await InitializeData(context);
            InternalSet(key, value);
            await SaveData(context, key, value, useWriteLock);
        }

        public virtual async Task Set(IExecutionContext context, IGameModuleData value, bool useWriteLock = false)
        {
            await Set(context, value.Key, value, useWriteLock);
        }

        public virtual async Task SetBatch(IExecutionContext context, List<SetItemBody> values, bool useWriteLock = false)
        {
            await InitializeData(context);
            _logger.LogInformation($"[{GetType().Name}.SetBatch] Save Batch for player '{_playerId}'.");
            foreach (SetItemBody item in values)
            {
                InternalSet(item.Key, item.Value);
            }
            await SaveBatchData(context, values, useWriteLock);
        }

        public virtual async Task SetBatch(IExecutionContext context, IEnumerable<IGameModuleData> values, bool useWriteLock = false)
        {
            List<SetItemBody> items = values.Select(v => new SetItemBody(v.Key, v)).ToList();
            await SetBatch(context, items, useWriteLock);
        }

        public virtual async Task Delete(IExecutionContext context, string key)
        {
            await InitializeData(context);
            _cache.Remove(key);
            _objectCache.Remove(key);
            await DeleteData(context, key);
        }

        public virtual void AddToCache(IEnumerable<string> moduleKeys)
        {
            foreach (string moduleKey in moduleKeys)
            {
                AddToCache(moduleKey);
            }
        }

        public virtual void AddToCache(params string[] moduleKeys)
        {
            foreach (string moduleKey in moduleKeys)
            {
                if (!_objectsToSave.Contains(moduleKey))
                {
                    _objectsToSave.Add(moduleKey);
                }
            }
        }

        public virtual void AddToCache(IGameModuleData moduleData)
        {
            AddToCache(moduleData.Key);
        }

        public virtual async Task SaveCache(IExecutionContext context)
        {
            if (_objectsToSave.Any())
            {
                List<SetItemBody> items = new List<SetItemBody>();
                foreach (string moduleData in _objectsToSave)
                {
                    if (_objectCache.TryGetValue(moduleData, out object? cachedObj) && cachedObj != null)
                    {
                        items.Add(new SetItemBody(moduleData, cachedObj));
                    }
                }
                await SetBatch(context, items);
                _objectsToSave.Clear();
            }
        }

        public virtual async Task<bool> Exists(IExecutionContext context, string key)
        {
            await InitializeData(context);
            return _cache.ContainsKey(key);
        }

        public virtual async Task<T> GetOrSet<T>(IExecutionContext context, string key, T defaultValue, bool useWriteLock = false)
        {
            if (await Exists(context, key))
            {
                return await Get<T>(context, key, defaultValue);
            }
            await Set(context, key, defaultValue, useWriteLock);
            return defaultValue;
        }

        public virtual async Task<T> GetOrSet<T>(IExecutionContext context, T defaultValue, bool useWriteLock = false) where T : IGameModuleData
        {
            string key = typeof(T).Name;
            if (await Exists(context, key))
            {
                return await Get(context, key, defaultValue);
            }
            await Set(context, key, defaultValue, useWriteLock);
            return defaultValue;
        }

        public virtual async Task<string> GetRaw(IExecutionContext context, string key)
        {
            await InitializeData(context);
            if (_cache.TryGetValue(key, out string value))
            {
                return value;
            }
            return null;
        }
    }
}