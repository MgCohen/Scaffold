using System.Collections.Generic;
using System.Threading.Tasks;
using LiveOps.ModuleFetchData;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;
using Unity.Services.CloudSave.Model;
using Xunit;

namespace LiveOps.Tests
{
    public sealed class UnityDataCacheBatchTests
    {
        private sealed class TestWriteCache : UnityDataCache
        {
            public int SaveDataCalls;
            public int SaveBatchDataCalls;
            public List<SetItemBody>? LastBatch;

            public TestWriteCache() : base(NullLogger.Instance, Mock.Of<IGameApiClient>(MockBehavior.Loose))
            {
            }

            protected override Task<Dictionary<string, string>> FetchData(IExecutionContext context)
            {
                return Task.FromResult(new Dictionary<string, string>(System.StringComparer.Ordinal));
            }

            protected override Task SaveData(IExecutionContext context, string key, object value, bool useWriteLock)
            {
                SaveDataCalls++;
                return Task.CompletedTask;
            }

            protected override Task SaveBatchData(IExecutionContext context, List<SetItemBody> values, bool useWriteLock)
            {
                SaveBatchDataCalls++;
                LastBatch = values;
                return Task.CompletedTask;
            }

            protected override Task DeleteData(IExecutionContext context, string key) => Task.CompletedTask;
        }

        [Fact]
        public async Task BeginBatch_batched_set_flushes_once_on_last_dispose()
        {
            var ctx = new TestExecutionContext();
            var c = new TestWriteCache();
            await c.WarmupAsync(ctx, null);
            c.SaveDataCalls = 0;
            c.SaveBatchDataCalls = 0;

            await c.Set(ctx, "a", 1, useWriteLock: false);
            Assert.Equal(1, c.SaveDataCalls);
            c.SaveDataCalls = 0;

            await using (c.BeginBatch())
            {
                await c.Set(ctx, "b", 2, useWriteLock: false);
            }

            Assert.Equal(0, c.SaveDataCalls);
            Assert.Equal(1, c.SaveBatchDataCalls);
            Assert.NotNull(c.LastBatch);
            Assert.Contains(c.LastBatch!, s => s.Key == "b");
        }

        [Fact]
        public async Task BeginBatch_nesting_does_not_flush_until_outer_disposes()
        {
            var ctx = new TestExecutionContext();
            var c = new TestWriteCache();
            await c.WarmupAsync(ctx, null);
            c.SaveBatchDataCalls = 0;

            await using (c.BeginBatch())
            {
                await c.Set(ctx, "x", 1, useWriteLock: false);
                await using (c.BeginBatch())
                {
                    await c.Set(ctx, "y", 2, useWriteLock: false);
                }

                Assert.Equal(0, c.SaveBatchDataCalls);
            }

            Assert.Equal(1, c.SaveBatchDataCalls);
        }

        [Fact]
        public async Task Flush_with_no_deltas_does_not_invoke_batch()
        {
            var ctx = new TestExecutionContext();
            var c = new TestWriteCache();
            await c.WarmupAsync(ctx, null);
            c.SaveBatchDataCalls = 0;
            c.SaveDataCalls = 0;

            await c.Set(ctx, "k", new Payload { N = 1 });
            c.SaveDataCalls = 0;
            c.SaveBatchDataCalls = 0;

            await c.FlushAsync(ctx);
            Assert.Equal(0, c.SaveBatchDataCalls);
        }

        private sealed class Payload
        {
            public int N { get; set; }
        }
    }
}
