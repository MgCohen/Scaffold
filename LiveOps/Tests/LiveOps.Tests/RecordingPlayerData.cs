using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LiveOps.ModuleFetchData;
using Unity.Services.CloudCode.Core;

namespace LiveOps.Tests
{
    internal sealed class RecordingPlayerData : IPlayerData
    {
        private readonly MemoryPlayerData _inner = new();
        public IReadOnlyCollection<string>? LastWarmupKeys { get; private set; }

        public string PlayerId => _inner.PlayerId;

        public Task WarmupAsync(IExecutionContext context, IReadOnlyCollection<string>? keys = null)
        {
            LastWarmupKeys = keys;
            return _inner.WarmupAsync(context, keys);
        }

        public Task<bool> Exists(IExecutionContext context, string key) => _inner.Exists(context, key);

        public Task<T> Get<T>(IExecutionContext context, string key, T defaultValue) => _inner.Get(context, key, defaultValue);

        public Task Set(IExecutionContext context, string key, object value, bool useWriteLock = false) => _inner.Set(context, key, value, useWriteLock);

        public Task FlushAsync(IExecutionContext context) => _inner.FlushAsync(context);

        public Task Delete(IExecutionContext context, string key) => _inner.Delete(context, key);

        public IAsyncDisposable BeginBatch() => _inner.BeginBatch();
    }
}
