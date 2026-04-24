using System;
using System.Threading.Tasks;
using LiveOps.DTO.GameApi;
using LiveOps.DTO.ModuleRequest;
using LiveOps.GameApi;
using LiveOps.Signal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json.Linq;
using Unity.Services.CloudCode.Core;
using Xunit;
using MGameState = LiveOps.Tests.MemoryGameState;
using MPlayer = LiveOps.Tests.MemoryPlayerData;
using PGameApi = LiveOps.GameApi.GameApiDispatcher;

namespace LiveOps.Tests
{
    public sealed class GameApiDispatcherTests
    {
        public sealed class PingRequest : ModuleRequest<PingResponse>
        {
        }

        public sealed class PingResponse : ModuleResponse
        {
        }

        public sealed class ConfigurablePingHandler : IGameApiHandler<PingRequest, PingResponse>
        {
            public string[]? PlayerKeyHint { get; set; }

            public string[]? ConfigKeyHint { get; set; }

            public string[]? PlayerKeys() => PlayerKeyHint;

            public string[]? ConfigKeys() => ConfigKeyHint;

            public Task<PingResponse> HandleAsync(GameApiSession session, PingRequest request) =>
                Task.FromResult(new PingResponse());
        }

        public sealed class BoomRequest : ModuleRequest<BoomResponse>
        {
        }

        public sealed class BoomResponse : ModuleResponse
        {
        }

        public sealed class BoomHandler : IGameApiHandler<BoomRequest, BoomResponse>
        {
            public Task<BoomResponse> HandleAsync(GameApiSession session, BoomRequest request) =>
                throw new InvalidOperationException("boom");
        }

        private static (PGameApi Dispatcher, IServiceProvider Sp) CreateDispatcher(
            Action<IServiceCollection>? extra = null,
            string[]? playerKeys = null,
            string[]? configKeys = null)
        {
            var registry = new GameApiRegistry();
            registry.RegisterHandlerType(typeof(ConfigurablePingHandler));
            registry.RegisterHandlerType(typeof(BoomHandler));

            IServiceCollection services = new ServiceCollection();
            services.AddSingleton(registry);
            services.AddSingleton<SignalModule>();
            services.AddSingleton<ILogger<PGameApi>>(_ => NullLogger<PGameApi>.Instance);

            services.AddSingleton<ConfigurablePingHandler>(sp => new ConfigurablePingHandler
            {
                PlayerKeyHint = playerKeys,
                ConfigKeyHint = configKeys
            });
            services.AddSingleton<BoomHandler>();
            services.AddSingleton(sp => (PGameApi)ActivatorUtilities.CreateInstance(sp, typeof(PGameApi)));
            extra?.Invoke(services);

            ServiceProvider sp = services.BuildServiceProvider();
            PGameApi d = sp.GetRequiredService<PGameApi>();
            return (d, sp);
        }

        [Fact]
        public async Task Happy_path_resolves_typed_request_and_returns_success()
        {
            (PGameApi d, IServiceProvider _) = CreateDispatcher();
            IExecutionContext ctx = new TestExecutionContext();
            var player = new MPlayer();
            var state = new MGameState();
            var remote = new MemoryRemoteConfig();

            GameApiEnvelopeRequest env = new()
            {
                RequestKey = nameof(PingRequest),
                Payload = new JObject()
            };

            GameApiEnvelopeResponse res = await d.Invoke(ctx, player, state, remote, env).ConfigureAwait(false);
            Assert.Equal(ResponseStatusType.Success, res.StatusType);
            Assert.IsType<PingResponse>(res.Result);
        }

        [Fact]
        public async Task Unknown_key_returns_exception_envelope()
        {
            (PGameApi d, IServiceProvider _) = CreateDispatcher();
            IExecutionContext ctx = new TestExecutionContext();
            var player = new MPlayer();
            var state = new MGameState();
            var remote = new MemoryRemoteConfig();
            GameApiEnvelopeResponse res = await d.Invoke(
                ctx,
                player,
                state,
                remote,
                new GameApiEnvelopeRequest
                {
                    RequestKey = "nope",
                    Payload = new JObject()
                }).ConfigureAwait(false);

            Assert.Equal(ResponseStatusType.Exception, res.StatusType);
        }

        [Fact]
        public async Task Handler_exception_maps_to_exception_envelope()
        {
            (PGameApi d, IServiceProvider _) = CreateDispatcher();
            IExecutionContext ctx = new TestExecutionContext();
            var player = new MPlayer();
            var state = new MGameState();
            var remote = new MemoryRemoteConfig();
            GameApiEnvelopeResponse res = await d.Invoke(
                ctx,
                player,
                state,
                remote,
                new GameApiEnvelopeRequest
                {
                    RequestKey = nameof(BoomRequest),
                    Payload = new JObject()
                }).ConfigureAwait(false);

            Assert.Equal(ResponseStatusType.Exception, res.StatusType);
        }

        [Fact]
        public async Task Warmup_passes_null_player_hint_when_handler_returns_null()
        {
            (PGameApi d, IServiceProvider _) = CreateDispatcher(null, null, new[] { "c" });
            IExecutionContext ctx = new TestExecutionContext();
            var player = new RecordingPlayerData();
            var state = new MGameState();
            var remote = new MemoryRemoteConfig();
            await d.Invoke(
                ctx,
                player,
                state,
                remote,
                new GameApiEnvelopeRequest
                {
                    RequestKey = nameof(PingRequest),
                    Payload = new JObject()
                }).ConfigureAwait(false);

            Assert.Null(player.LastWarmupKeys);
            Assert.Equal(new[] { "c" }, remote.LastWarmupKeys);
        }

        [Fact]
        public async Task Warmup_passes_empty_player_hint_as_empty_collection()
        {
            (PGameApi d, IServiceProvider _) = CreateDispatcher(null, System.Array.Empty<string>(), new[] { "c" });
            IExecutionContext ctx = new TestExecutionContext();
            var player = new RecordingPlayerData();
            var state = new MGameState();
            var remote = new MemoryRemoteConfig();
            await d.Invoke(
                ctx,
                player,
                state,
                remote,
                new GameApiEnvelopeRequest
                {
                    RequestKey = nameof(PingRequest),
                    Payload = new JObject()
                }).ConfigureAwait(false);

            Assert.NotNull(player.LastWarmupKeys);
            Assert.Empty(player.LastWarmupKeys!);
            Assert.Equal(new[] { "c" }, remote.LastWarmupKeys);
        }

        [Fact]
        public async Task Warmup_passes_keyed_hints()
        {
            (PGameApi d, IServiceProvider _) = CreateDispatcher(null, new[] { "p" }, new[] { "c" });
            IExecutionContext ctx = new TestExecutionContext();
            var player = new RecordingPlayerData();
            var state = new MGameState();
            var remote = new MemoryRemoteConfig();
            await d.Invoke(
                ctx,
                player,
                state,
                remote,
                new GameApiEnvelopeRequest
                {
                    RequestKey = nameof(PingRequest),
                    Payload = new JObject()
                }).ConfigureAwait(false);

            Assert.Equal(new[] { "p" }, player.LastWarmupKeys);
            Assert.Equal(new[] { "c" }, remote.LastWarmupKeys);
        }
    }
}
