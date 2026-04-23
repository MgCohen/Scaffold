using System.Collections.Generic;
using System.Threading.Tasks;
using LiveOps.Core.GameModule;
using LiveOps.Core.ModuleFetchData;
using LiveOps.Core.DTO.GameModule;
using Unity.Services.CloudCode.Core;
using Xunit;

namespace LiveOps.Tests
{
    public sealed class ModulePrefetchKeysTests
    {
        private sealed class StubModule : IGameModule
        {
            private readonly string[]? _playerKeys;
            private readonly string[]? _configKeys;

            public StubModule(string key, string[]? playerKeys, string[]? configKeys)
            {
                Key = key;
                _playerKeys = playerKeys;
                _configKeys = configKeys;
            }

            public string Key { get; }

            public string[]? PlayerKeys() => _playerKeys;

            public string[]? ConfigKeys() => _configKeys;

            public Task<IGameModuleData> Initialize(IExecutionContext context, IPlayerData Player, IGameState gameState, IRemoteConfig remoteConfig)
            {
                return Task.FromResult<IGameModuleData>(null!);
            }
        }

        [Fact]
        public void UnionOrAll_WhenAnyModuleReturnsNull_ReturnsNull()
        {
            IGameModule[] mods =
            {
                new StubModule("a", new[] { "x" }, new[] { "c" }),
                new StubModule("b", null, new[] { "d" }),
            };

            Assert.Null(ModulePrefetchKeys.UnionOrAll(mods, m => m.PlayerKeys()));

            string[]? ck = ModulePrefetchKeys.UnionOrAll(mods, m => m.ConfigKeys());
            Assert.NotNull(ck);
            Assert.Equal(2, ck!.Length);
            Assert.Contains("c", ck);
            Assert.Contains("d", ck);
        }

        [Fact]
        public void UnionOrAll_MergesDistinctKeys()
        {
            IGameModule[] mods =
            {
                new StubModule("a", new[] { "p1", "p2" }, new[] { "c1" }),
                new StubModule("b", new[] { "p2", "p3" }, new[] { "c1", "c2" }),
            };

            string[]? pk = ModulePrefetchKeys.UnionOrAll(mods, m => m.PlayerKeys());
            string[]? ck = ModulePrefetchKeys.UnionOrAll(mods, m => m.ConfigKeys());

            Assert.NotNull(pk);
            Assert.Equal(3, pk!.Length);
            Assert.Contains("p1", pk);
            Assert.Contains("p2", pk);
            Assert.Contains("p3", pk);

            Assert.NotNull(ck);
            Assert.Equal(2, ck!.Length);
        }

        [Fact]
        public void UnionOrAll_AllEmptyArrays_ReturnsEmpty()
        {
            IGameModule[] mods =
            {
                new StubModule("a", System.Array.Empty<string>(), System.Array.Empty<string>()),
                new StubModule("b", System.Array.Empty<string>(), System.Array.Empty<string>()),
            };

            string[]? pk = ModulePrefetchKeys.UnionOrAll(mods, m => m.PlayerKeys());
            Assert.NotNull(pk);
            Assert.Empty(pk!);
        }
    }
}
