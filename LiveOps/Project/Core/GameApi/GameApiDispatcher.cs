using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GameModule.ModuleFetchData;
using GameModule.Signal;
using GameModuleDTO.GameApi;
using GameModuleDTO.Json;
using GameModuleDTO.ModuleRequests;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Unity.Services.CloudCode.Core;

namespace GameModule.GameApi
{
    /// <summary>
    /// Single Cloud Code entry point that dispatches <see cref="GameApiEnvelopeRequest"/> to typed handlers.
    /// </summary>
    public class GameApiDispatcher
    {
        private readonly ILogger<GameApiDispatcher> _logger;
        private readonly GameApiRegistry _registry;
        private readonly SignalModule _signals;
        private readonly IServiceProvider _services;
        private readonly JsonSerializer _serializer;

        public GameApiDispatcher(
            ILogger<GameApiDispatcher> logger,
            GameApiRegistry registry,
            SignalModule signals,
            IServiceProvider services)
        {
            _logger = logger;
            _registry = registry;
            _signals = signals;
            _services = services;
            _serializer = JsonSerializer.Create(new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.Auto,
                SerializationBinder = new CrossPlatformTypeBinder(),
                ContractResolver = new CamelCasePropertyNamesContractResolver(),
                NullValueHandling = NullValueHandling.Ignore,
            });
        }

        [CloudCodeFunction("GameApi")]
        public async Task<GameApiEnvelopeResponse> Invoke(
            IExecutionContext context,
            IPlayerData player,
            IGameState gameState,
            IRemoteConfig remoteConfig,
            GameApiEnvelopeRequest request)
        {
            if (request == null || string.IsNullOrEmpty(request.RequestKey))
            {
                return GameApiEnvelopeResponse.Exception("<unknown>", new ArgumentException("Missing RequestKey."));
            }

            if (!_registry.TryGet(request.RequestKey, out HandlerEntry entry))
            {
                return GameApiEnvelopeResponse.Exception(request.RequestKey, new InvalidOperationException($"Unknown RequestKey '{request.RequestKey}'."));
            }

            try
            {
                object requestObj = request.Payload != null
                    ? request.Payload.ToObject(entry.RequestType, _serializer)
                    : Activator.CreateInstance(entry.RequestType);

                object handlerObj = _services.GetService(entry.HandlerType);
                if (handlerObj is not IGameApiHandler handler)
                {
                    throw new InvalidOperationException($"No handler registered for {entry.RequestType.Name}.");
                }

                string[]? playerKeys = handler.PlayerKeys();
                string[]? configKeys = handler.ConfigKeys();

                await Task.WhenAll(
                    player.WarmupAsync(context, playerKeys),
                    remoteConfig.WarmupAsync(context, configKeys)
                ).ConfigureAwait(false);

                ModuleResponse result;
                List<ModuleResponse> nestedResponses;
                await using (player.BeginBatch())
                await using (gameState.BeginBatch())
                {
                    GameApiSession session = new GameApiSession(_services, _registry, context, player, gameState, remoteConfig);
                    result = await handler.HandleAsync(session, requestObj).ConfigureAwait(false);
                    nestedResponses = new List<ModuleResponse>(session.Nested);
                }

                _signals.Push(requestObj);

                return GameApiEnvelopeResponse.Success(request.RequestKey, result, nestedResponses);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[GameApi] {Key} failed: {Message}", request.RequestKey, ex.Message);
                return GameApiEnvelopeResponse.Exception(request.RequestKey, ex);
            }
        }
    }
}
