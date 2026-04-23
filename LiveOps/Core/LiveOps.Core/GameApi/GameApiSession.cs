using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LiveOps.Core.ModuleFetchData;
using LiveOps.Core.DTO.ModuleRequest;
using Microsoft.Extensions.DependencyInjection;
using Unity.Services.CloudCode.Core;

namespace LiveOps.Core.GameApi
{
    /// <summary>
    /// Per-request context for GameApi handlers (caches, nested side-effect responses).
    /// </summary>
    public sealed class GameApiSession
    {
        private readonly IServiceProvider _services;
        private readonly GameApiRegistry _registry;
        private readonly List<ModuleResponse> _nested = new List<ModuleResponse>();

        public GameApiSession(
            IServiceProvider services,
            GameApiRegistry registry,
            IExecutionContext context,
            IPlayerData player,
            IGameState gameState,
            IRemoteConfig remoteConfig)
        {
            _services = services ?? throw new ArgumentNullException(nameof(services));
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            Context = context;
            Player = player;
            GameState = gameState;
            RemoteConfig = remoteConfig;
        }

        public IExecutionContext Context { get; }

        public IPlayerData Player { get; }

        public IGameState GameState { get; }

        public IRemoteConfig RemoteConfig { get; }

        public IReadOnlyList<ModuleResponse> Nested => _nested;

        public void EmitSideEffect(ModuleResponse response)
        {
            if (response != null)
            {
                _nested.Add(response);
            }
        }

        public async Task<TRes> InvokeAsync<TReq, TRes>(TReq request)
            where TReq : ModuleRequest<TRes>
            where TRes : ModuleResponse
        {
            if (!_registry.TryGet(typeof(TReq).Name, out HandlerEntry entry))
            {
                throw new InvalidOperationException($"No GameApi handler registered for {typeof(TReq).Name}.");
            }

            object handlerObj = _services.GetService(entry.HandlerType);
            if (handlerObj is not IGameApiHandler<TReq, TRes> handler)
            {
                throw new InvalidOperationException($"No IGameApiHandler<{typeof(TReq).Name}, {typeof(TRes).Name}> registered for {entry.HandlerType.Name}.");
            }

            TRes result = await handler.HandleAsync(this, request).ConfigureAwait(false);
            EmitSideEffect(result);
            return result;
        }
    }
}
