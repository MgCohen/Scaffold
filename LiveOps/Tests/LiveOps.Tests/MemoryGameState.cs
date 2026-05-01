using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LiveOps.ModuleFetchData;
using Newtonsoft.Json;
using Unity.Services.CloudCode.Core;

namespace LiveOps.Tests
{
    internal sealed class MemoryGameState : IGameState
    {
        private const string DefaultDb = "GameState";
        private readonly Dictionary<string, string> _raw = new(StringComparer.Ordinal);

        public Task WarmupAsync(IExecutionContext context, IReadOnlyCollection<string>? keys = null) => Task.CompletedTask;

        public Task<bool> Exists(IExecutionContext context, string key) => Task.FromResult(_raw.ContainsKey(Combine(DefaultDb, key)));

        public Task<T> Get<T>(IExecutionContext context, string key, T defaultValue) => GetImpl<T>(context, DefaultDb, key, defaultValue);

        public Task<T> Get<T>(IExecutionContext context, string databaseKey, string itemKey, T defaultValue) =>
            GetImpl<T>(context, databaseKey, itemKey, defaultValue);

        public Task Set(IExecutionContext context, string key, object value, bool useWriteLock = false) =>
            SetImpl(context, DefaultDb, key, value, useWriteLock);

        public Task Set(IExecutionContext context, string databaseKey, string key, object value, bool useWriteLock = false) =>
            SetImpl(context, databaseKey, key, value, useWriteLock);

        public Task FlushAsync(IExecutionContext context) => Task.CompletedTask;

        public Task Delete(IExecutionContext context, string key) => DeleteImpl(context, DefaultDb, key);

        public Task Delete(IExecutionContext context, string databaseKey, string key) => DeleteImpl(context, databaseKey, key);

        public IAsyncDisposable BeginBatch() => new NoOpScope();

        private static string Combine(string databaseKey, string key) => databaseKey + "|" + key;

        private Task<T> GetImpl<T>(IExecutionContext context, string databaseKey, string itemKey, T defaultValue)
        {
            string c = Combine(databaseKey, itemKey);
            if (!_raw.TryGetValue(c, out string? json))
            {
                return Task.FromResult(defaultValue);
            }

            try
            {
                T? v = JsonConvert.DeserializeObject<T>(json);
                return Task.FromResult(v ?? defaultValue);
            }
            catch
            {
                return Task.FromResult(defaultValue);
            }
        }

        private Task SetImpl(IExecutionContext context, string databaseKey, string key, object value, bool useWriteLock)
        {
            _raw[Combine(databaseKey, key)] = JsonConvert.SerializeObject(value);
            return Task.CompletedTask;
        }

        private Task DeleteImpl(IExecutionContext context, string databaseKey, string key)
        {
            _raw.Remove(Combine(databaseKey, key));
            return Task.CompletedTask;
        }

        private sealed class NoOpScope : IAsyncDisposable
        {
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }
}
