using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LiveOps.ModuleFetchData;
using Newtonsoft.Json;
using Unity.Services.CloudCode.Core;

namespace LiveOps.Tests
{
    /// <summary>Minimal fake for tests that only need property tokens (no cloud calls).</summary>
    internal sealed class TestExecutionContext : IExecutionContext
    {
        public string ProjectId { get; set; } = "project";
        public string PlayerId { get; set; } = "player";
        public string EnvironmentId { get; set; } = "env";
        public string EnvironmentName { get; set; } = "envName";
        public string AccessToken { get; set; } = "access";
        public string UserId { get; set; } = "user";
        public string Issuer { get; set; } = "issuer";
        public string ServiceToken { get; set; } = "service";
        public string AnalyticsUserId { get; set; } = "analytics";
        public string UnityInstallationId { get; set; } = "unity";
        public string CorrelationId { get; set; } = "corr";
    }

    /// <summary>Records the last <see cref="IReadableDataCache.WarmupAsync" /> key hint for assertions.</summary>
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

    /// <summary>Read-only in-memory <see cref="IRemoteConfig" /> for GameApi tests.</summary>
    internal sealed class MemoryRemoteConfig : IRemoteConfig
    {
        private readonly Dictionary<string, string> _raw = new(StringComparer.Ordinal);

        public IReadOnlyCollection<string>? LastWarmupKeys { get; private set; }

        public Task<bool> Exists(IExecutionContext context, string key) => Task.FromResult(_raw.ContainsKey(key));

        public Task<T> Get<T>(IExecutionContext context, string key, T defaultValue)
        {
            if (!_raw.TryGetValue(key, out string? json))
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

        public Task WarmupAsync(IExecutionContext context, IReadOnlyCollection<string>? keys = null)
        {
            LastWarmupKeys = keys;
            return Task.CompletedTask;
        }
    }

    /// <summary>In-memory <see cref="IGameState" /> (composite <c>database|key</c> storage for multi-namespace).</summary>
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

    /// <summary>Minimal in-memory read/write player cache for GameApi tests (pattern from <see cref="DataCacheExtensionsTests" />).</summary>
    internal sealed class MemoryPlayerData : IPlayerData
    {
        private readonly Dictionary<string, string> _raw = new(StringComparer.Ordinal);
        public string PlayerId => "test-player";

        public Task WarmupAsync(IExecutionContext context, IReadOnlyCollection<string>? keys = null) => Task.CompletedTask;

        public Task<bool> Exists(IExecutionContext context, string key) => Task.FromResult(_raw.ContainsKey(key));

        public Task<T> Get<T>(IExecutionContext context, string key, T defaultValue)
        {
            if (!_raw.TryGetValue(key, out string? json))
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

        public Task Set(IExecutionContext context, string key, object value, bool useWriteLock = false)
        {
            _raw[key] = JsonConvert.SerializeObject(value);
            return Task.CompletedTask;
        }

        public Task FlushAsync(IExecutionContext context) => Task.CompletedTask;

        public Task Delete(IExecutionContext context, string key)
        {
            _raw.Remove(key);
            return Task.CompletedTask;
        }

        public IAsyncDisposable BeginBatch() => new NoOpScope();

        private sealed class NoOpScope : IAsyncDisposable
        {
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }
}
