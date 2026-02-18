using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;
using GameModuleDTO.Json;
using Unity.Services.CloudSave.Model;

namespace GameModule.ModuleFetchData
{
    public abstract class DataCache
    {
        public DataCache(ILogger logger, IGameApiClient gameApiClient)
        {
            this.logger = logger;
            this.gameApiClient = gameApiClient;
        }

        public DataCache(ILogger logger, IGameApiClient gameApiClient, string playerId) : this(logger, gameApiClient)
        {
            this.logger = logger;
            this.gameApiClient = gameApiClient;
            this.playerId = playerId;
        }

        protected string playerId;
        protected string accessToken;

        protected ILogger logger;
        protected IGameApiClient gameApiClient;

        protected Dictionary<string, string> cache = new Dictionary<string, string>();
        protected Dictionary<string, object> objectCache = new Dictionary<string, object>();
        
        protected abstract Task<Dictionary<string, string>> FetchData(IExecutionContext context);
        protected abstract Task SaveData(IExecutionContext context, string key, object value, bool useWriteLock);
        protected abstract Task SaveBatchData(IExecutionContext context, List<SetItemBody> values, bool useWriteLock);

        protected abstract Task DeleteData(IExecutionContext context, string key);

        public string PlayerId
        {
            get
            {
                return playerId;
            }
        }

        protected void SetPlayerId(string playerId)
        {
            this.playerId = playerId;
        }

        protected virtual async Task InitializeData(IExecutionContext context)
        {
            if (context.AccessToken != accessToken)
            {
                // Only set the PlayerId from the context if it hasn't already been set by the constructor.
                if (string.IsNullOrEmpty(this.playerId))
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
            cache = await FetchData(context);
            objectCache.Clear();
            accessToken = context.AccessToken;
            logger.LogInformation($"[{GetType().Name}] Refreshed cache for player {playerId}");
        }
        
        public async Task<T> Get<T>(IExecutionContext context, string key, T defaultValue)
        {
            await InitializeData(context);
            
            if (objectCache.TryGetValue(key, out object? cachedObj) && cachedObj is T cachedTyped)
            {
                logger.LogInformation($"[{GetType().Name}.Get] Key {GetDebugKey(key)}  for player: '{playerId}' with cached value of type '{typeof(T).FullName}' from cache.");
                return cachedTyped;
            }

            if (cache.TryGetValue(key, out string value))
            {
                logger.LogInformation($"[{GetType().Name}.Get] Key {GetDebugKey(key)} for player: '{playerId}' with value: {value}' of type '{typeof(T).FullName}'.");
                T deserialized = value.FromJson<T>();
                objectCache[key] = deserialized;
                return deserialized;
            }

            logger.LogInformation($"[{GetType().Name}.Get] Key {GetDebugKey(key)} for player: '{playerId}' not found of type '{typeof(T).FullName}', returning default value.");
            return defaultValue;
        }
        
        //This does not update objectCache, be aware if you need to edit or get cached values
        public async Task<Dictionary<string, T>> GetAllValues<T>(IExecutionContext context)
        {
            await InitializeData(context);
            Dictionary<string, T> tempCache = new Dictionary<string, T>();
            foreach (KeyValuePair<string, string> pair in cache)
            {
                T deserialized = pair.Value.FromJson<T>();
                tempCache.Add(pair.Key, deserialized);
            }
            return tempCache;
        }

        private void InternalSet(string key, object value)
        {
            //string json = value.ToInternalJson();
            //cache[key] = json;
            objectCache[key] = value;
            logger.LogInformation($"[{GetType().Name}.Set] Saved key {GetDebugKey(key)} for player: '{playerId}', value: {value.ToJson()}.");
        }
        
        public async Task Set(IExecutionContext context, string key, object value, bool useWriteLock = false)
        {
            await InitializeData(context);
            InternalSet(key, value);
            await SaveData(context, key, value, useWriteLock);
        }

        public async Task SetBatch(IExecutionContext context, List<SetItemBody> values, bool useWriteLock = false)
        {
            await InitializeData(context);
            logger.LogInformation($"[{GetType().Name}.Set] Save Batch for player '{playerId}'.");
            foreach (SetItemBody item in values)
            {
                if (useWriteLock)
                {
                    //item.WriteLock = Guid.NewGuid().ToString();
                }
                InternalSet(item.Key, item.Value);
            }
            await SaveBatchData(context, values, useWriteLock);
        }

        public async Task<bool> Exists(IExecutionContext context, string key)
        {
            await InitializeData(context);
            return cache.ContainsKey(key);
        }

        public async Task<T> GetOrSet<T>(IExecutionContext context, string key, T defaultValue, bool useWriteLock = false)
        {
            if (await Exists(context, key))
            {
                return await Get<T>(context, key, defaultValue);
            }

            await Set(context, key, defaultValue, useWriteLock);
            return defaultValue;
        }

        public async Task<string> GetRaw(IExecutionContext context, string key)
        {
            await InitializeData(context);

            if (cache.TryGetValue(key, out string value))
            {
                return value;
            }

            return null;
        }
    }
}