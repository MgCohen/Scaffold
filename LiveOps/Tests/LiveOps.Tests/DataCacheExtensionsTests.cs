using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LiveOps.ModuleFetchData;
using LiveOps.Modules.DTO.Ads;
using Newtonsoft.Json;
using Unity.Services.CloudCode.Core;
using Xunit;

namespace LiveOps.Tests
{
    public sealed class DataCacheExtensionsTests
    {
        private sealed class MemoryPlayerCache : IPlayerData
        {
            private readonly Dictionary<string, string> _raw = new(StringComparer.Ordinal);
            public string PlayerId => "test-player";

            public Task WarmupAsync(IExecutionContext context, IReadOnlyCollection<string>? keys = null)
            {
                return Task.CompletedTask;
            }

            public Task<bool> Exists(IExecutionContext context, string key)
            {
                return Task.FromResult(_raw.ContainsKey(key));
            }

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

        [Fact]
        public async Task GetOrSet_WhenMissing_WritesDefaultAndReturnsIt()
        {
            IPlayerData cache = new MemoryPlayerCache();
            IExecutionContext ctx = null!;

            AdsPersistence first = await cache.GetOrSet(ctx, new AdsPersistence(), useWriteLock: false);
            Assert.NotNull(first);

            AdsPersistence second = await cache.GetOrSet(ctx, new AdsPersistence(), useWriteLock: false);
            Assert.NotNull(second);
            Assert.True(await cache.Exists(ctx, nameof(AdsPersistence)));
        }
    }
}
