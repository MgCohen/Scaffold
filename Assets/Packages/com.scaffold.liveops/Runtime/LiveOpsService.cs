using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using GameModuleDTO.GameApi;
using GameModuleDTO.GameModule;
using GameModuleDTO.ModuleRequests;
using Scaffold.LayeredScope;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using Scaffold.CloudCode;
using VContainer;

namespace Scaffold.LiveOps
{
    internal sealed class LiveOpsService : ILiveOpsService, IAsyncInitializable
    {
        public LiveOpsService(ICloudCodeService cloudCodeService, IObjectResolver objectResolver, CloudCodeOptimisticHandlerRegistry optimisticRegistry, CloudCodeErrorHandler cloudCodeErrorHandler)
        {
            if (cloudCodeService == null)
            {
                throw new ArgumentNullException(nameof(cloudCodeService));
            }

            if (objectResolver == null)
            {
                throw new ArgumentNullException(nameof(objectResolver));
            }

            if (optimisticRegistry == null)
            {
                throw new ArgumentNullException(nameof(optimisticRegistry));
            }

            if (cloudCodeErrorHandler == null)
            {
                throw new ArgumentNullException(nameof(cloudCodeErrorHandler));
            }

            this.cloudCodeService = cloudCodeService;
            this.moduleResponseDispatchService = new ModuleResponseDispatchService(objectResolver);
            this.optimisticRegistry = optimisticRegistry;
            this.cloudCodeErrorHandler = cloudCodeErrorHandler;
        }

        private static readonly JsonSerializerSettings liveOpsJsonSettings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            NullValueHandling = NullValueHandling.Ignore,
        };

        private readonly ICloudCodeService cloudCodeService;
        private readonly ModuleResponseDispatchService moduleResponseDispatchService;
        private readonly CloudCodeOptimisticHandlerRegistry optimisticRegistry;
        private readonly CloudCodeErrorHandler cloudCodeErrorHandler;
        private GameData gameData;

        public T GetModuleData<T>() where T : class, IGameModuleData
        {
            return gameData == null ? null : gameData.GetModuleData<T>();
        }

        public Task InitializeAsync(CancellationToken cancellationToken)
        {
            return LoadInitialGameDataAsync(cancellationToken);
        }

        private async Task LoadInitialGameDataAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            GameDataRequest request = new GameDataRequest();
            GameDataResponse response = await CallAsync(request, cancellationToken);
            gameData = response?.GameData;
        }

        public async Task<TResponse> CallAsync<TResponse>(ModuleRequest<TResponse> request, CancellationToken cancellationToken = default) where TResponse : ModuleResponse
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (UsesGameApiRequest(request))
            {
                return await CallGameApiAsync(request, cancellationToken);
            }

            Task<TResponse> endpointCall = cloudCodeService.CallEndpointAsync<TResponse>(request.ModuleName, request.FunctionName, payload: request, cancellationToken: cancellationToken);
            TResponse response = await endpointCall;
            moduleResponseDispatchService.DispatchNestedResponses(response);
            return response;
        }

        private async Task<TResponse> CallGameApiAsync<TResponse>(ModuleRequest<TResponse> request, CancellationToken cancellationToken) where TResponse : ModuleResponse
        {
            GameApiEnvelopeRequest envelope = new GameApiEnvelopeRequest { RequestKey = request.GetType().Name, Payload = JObject.FromObject(request, JsonSerializer.Create(liveOpsJsonSettings)), };
            Task<GameApiEnvelopeResponse> serverTask = cloudCodeService.CallEndpointAsync<GameApiEnvelopeResponse>(request.ModuleName, "GameApi", envelope, cancellationToken);

            if (optimisticRegistry.TryResolve(request.ModuleName, "GameApi", request, out IRequestHandler<TResponse> handler, out TResponse optimistic))
            {
                RunGameApiReconciliationInTheBackground(serverTask, handler, optimistic, request);
                return optimistic;
            }

            GameApiEnvelopeResponse resp = await serverTask;
            return UnwrapAndDispatchGameApi<TResponse>(resp);
        }

        private async void RunGameApiReconciliationInTheBackground<TResponse>(Task<GameApiEnvelopeResponse> serverTask, IRequestHandler<TResponse> handler, TResponse optimistic, ModuleRequest<TResponse> request) where TResponse : ModuleResponse
        {
            try
            {
                GameApiEnvelopeResponse resp = await serverTask;
                TResponse server = UnwrapAndDispatchGameApi<TResponse>(resp);
                handler.Validate(server, optimistic);
            }
            catch (Exception ex)
            {
                cloudCodeErrorHandler.Handle(ex, request.ModuleName, "GameApi", request, optimistic);
            }
        }

        private TResponse UnwrapAndDispatchGameApi<TResponse>(GameApiEnvelopeResponse resp) where TResponse : ModuleResponse
        {
            EnsureGameApiEnvelope(resp);
            TResponse typed = (TResponse)resp.Result;
            EnsureTypedResult(typed);
            MergeNestedResponsesInto(resp, typed);
            moduleResponseDispatchService.DispatchNestedResponses(typed);
            return typed;
        }

        private void EnsureGameApiEnvelope(GameApiEnvelopeResponse resp)
        {
            if (resp == null)
            {
                throw new InvalidOperationException("GameApi returned null response.");
            }

            if (resp.StatusType == ResponseStatusType.Exception)
            {
                throw new InvalidOperationException(string.IsNullOrEmpty(resp.Message) ? "GameApi failed." : resp.Message);
            }
        }

        private void EnsureTypedResult<TResponse>(TResponse typed) where TResponse : ModuleResponse
        {
            if (typed == null)
            {
                throw new InvalidOperationException("GameApi returned null result payload.");
            }
        }

        private void MergeNestedResponsesInto<TResponse>(GameApiEnvelopeResponse resp, TResponse typed) where TResponse : ModuleResponse
        {
            if (resp.NestedResponses != null && resp.NestedResponses.Count > 0)
            {
                typed.Responses.AddRange(resp.NestedResponses);
            }
        }

        private bool UsesGameApiRequest<TResponse>(ModuleRequest<TResponse> request) where TResponse : ModuleResponse
        {
            return request.GetType().GetCustomAttribute<UsesGameApiAttribute>(inherit: false) != null;
        }
    }
}
