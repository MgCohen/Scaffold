using System.Threading;
using System.Threading.Tasks;
using LiveOps.DTO.GameModule;
using LiveOps.DTO.Keys;
using LiveOps.GameModule;
using LiveOps.GameApi;
using LiveOps.Modules.GameData;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace LiveOps.Tests
{
    public sealed class ModulePrefetchKeysTests
    {
        [LiveOpsKey("StubData")]
        private sealed class StubData : IGameModuleData
        {
        }

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

            public Task<IGameModuleData> InitializeAsync(GameApiSession session, CancellationToken cancellationToken = default)
            {
                return Task.FromResult<IGameModuleData>(new StubData());
            }
        }

        private static GameDataHandler Handler(params IGameModule[] mods)
        {
            return new GameDataHandler(NullLogger<GameDataHandler>.Instance, mods);
        }

        [Fact]
        public void PlayerKeys_WhenAnyModuleReturnsNull_ReturnsNull_AndConfigKeysStillUnions()
        {
            GameDataHandler handler = Handler(
                new StubModule("a", new[] { "x" }, new[] { "c" }),
                new StubModule("b", null, new[] { "d" }));

            Assert.Null(handler.PlayerKeys());

            string[]? ck = handler.ConfigKeys();
            Assert.NotNull(ck);
            Assert.Equal(2, ck!.Length);
            Assert.Contains("c", ck);
            Assert.Contains("d", ck);
        }

        [Fact]
        public void PlayerKeys_And_ConfigKeys_MergeDistinctKeys()
        {
            GameDataHandler handler = Handler(
                new StubModule("a", new[] { "p1", "p2" }, new[] { "c1" }),
                new StubModule("b", new[] { "p2", "p3" }, new[] { "c1", "c2" }));

            string[]? pk = handler.PlayerKeys();
            string[]? ck = handler.ConfigKeys();

            Assert.NotNull(pk);
            Assert.Equal(3, pk!.Length);
            Assert.Contains("p1", pk);
            Assert.Contains("p2", pk);
            Assert.Contains("p3", pk);

            Assert.NotNull(ck);
            Assert.Equal(2, ck!.Length);
        }

        [Fact]
        public void PlayerKeys_AllEmptyArrays_ReturnsEmpty()
        {
            GameDataHandler handler = Handler(
                new StubModule("a", System.Array.Empty<string>(), System.Array.Empty<string>()),
                new StubModule("b", System.Array.Empty<string>(), System.Array.Empty<string>()));

            string[]? pk = handler.PlayerKeys();
            Assert.NotNull(pk);
            Assert.Empty(pk!);
        }
    }
}
