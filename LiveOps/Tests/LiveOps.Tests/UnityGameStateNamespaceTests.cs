using System.Collections.Generic;
using System.Threading.Tasks;
using LiveOps.ModuleFetchData;
using LiveOps.ModuleFetchData.Unity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;
using Unity.Services.CloudSave.Model;
using Xunit;

namespace LiveOps.Tests
{
    public sealed class UnityGameStateNamespaceTests
    {
        private sealed class TestableUnityGameState : UnityGameState
        {
            public TestableUnityGameState(ILogger<UnityDataCache> logger, IGameApiClient gameApiClient) : base(logger, gameApiClient)
            {
            }

            /// <summary>Avoids cloud in tests; <see cref="UnityGameState" /> fetches in <see cref="FetchData" /> per namespace.</summary>
            protected override Task<Dictionary<string, string>> FetchData(IExecutionContext context)
            {
                return Task.FromResult(new Dictionary<string, string>(System.StringComparer.Ordinal));
            }

            protected override Task SaveData(IExecutionContext context, string key, object value, bool useWriteLock) => Task.CompletedTask;

            protected override Task SaveBatchData(IExecutionContext context, List<SetItemBody> values, bool useWriteLock) => Task.CompletedTask;

            protected override Task DeleteData(IExecutionContext context, string key) => Task.CompletedTask;
        }

        [Fact]
        public async Task Keys_in_one_database_namespace_do_not_leak_to_another()
        {
            IExecutionContext context = new TestExecutionContext { PlayerId = "p1" };

            IGameApiClient api = Mock.Of<IGameApiClient>(MockBehavior.Loose);
            ILogger<UnityDataCache> logger = NullLogger<UnityDataCache>.Instance;
            var state = new TestableUnityGameState(logger, api);

            await state.Set(context, "dbA", "k", 42, useWriteLock: false);
            int inA = await state.Get(context, "dbA", "k", 0);
            int inB = await state.Get(context, "dbB", "k", 0);

            Assert.Equal(42, inA);
            Assert.Equal(0, inB);
        }
    }
}
