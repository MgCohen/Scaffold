using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;
using LiveOps.DTO.Json;
using Unity.Services.CloudSave.Model;

namespace LiveOps.ModuleFetchData
{

    public abstract class UnityDataCache : ReadonlyUnityDataCache, IWriteableDataCache
    {
        protected UnityDataCache(ILogger logger, IGameApiClient gameApiClient) : base(logger, gameApiClient)
        {
        }

        protected UnityDataCache(ILogger logger, IGameApiClient gameApiClient, string playerId) : base(logger, gameApiClient, playerId)
        {
        }

        protected bool InBatch => _batchDepth > 0;
        private int _batchDepth;

        protected abstract Task SaveData(IExecutionContext context, string key, object value, bool useWriteLock);
        protected abstract Task SaveBatchData(IExecutionContext context, List<SetItemBody> values, bool useWriteLock);
        protected abstract Task DeleteData(IExecutionContext context, string key);

        private void InternalSetSynced(string key, object value)
        {
            _objectCache[key] = value;
            _cache[key] = value.ToJson();
            _logger.LogDebug("[{Type}.Set] Synced key {Key} for player {PlayerId}.", GetType().Name, GetDebugKey(key), _playerId);
        }

        private void InternalSetPending(string key, object value)
        {
            _objectCache[key] = value;
            _logger.LogDebug("[{Type}.Set] Pending key {Key} for player {PlayerId}.", GetType().Name, GetDebugKey(key), _playerId);
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
            if (context is null)
            {
                return Task.CompletedTask;
            }

            return FlushDirtyInternal(context);
        }

        protected async Task FlushDirtyInternal(IExecutionContext context)
        {
            await InitializeData(context).ConfigureAwait(false);
            List<SetItemBody>? dirty = null;
            foreach (KeyValuePair<string, object> kv in _objectCache)
            {
                if (kv.Value is null)
                {
                    continue;
                }

                string nowJson = kv.Value.ToJson() ?? string.Empty;
                if (!_cache.TryGetValue(kv.Key, out string? thenJson) || thenJson != nowJson)
                {
                    _cache[kv.Key] = nowJson;
                    dirty ??= new List<SetItemBody>();
                    dirty.Add(new SetItemBody(kv.Key, kv.Value));
                }
            }

            if (dirty is { Count: > 0 })
            {
                await SaveBatchData(context, dirty, useWriteLock: true).ConfigureAwait(false);
            }
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
