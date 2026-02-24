using System.Threading.Tasks;
using GameModule.ModuleFetchData;
using GameModule.Response;
using GameModuleDTO.GameModule;
using GameModuleDTO.ModuleRequests;
using GameModuleDTO.Sample.CounterModule;
using Microsoft.Extensions.Logging;
using Unity.Services.CloudCode.Core;

namespace GameModule.Sample
{
    public class CounterModule : GameModuleT<CounterModuleData>
    {
        public CounterModule(ILogger<CounterModule> logger, ModuleRequestHandler moduleRequestHandler)
        {
            _logger = logger;
            _moduleRequestHandler = moduleRequestHandler;
        }

        private readonly ILogger<CounterModule> _logger;
        private readonly ModuleRequestHandler _moduleRequestHandler;

        #region IGameModule implementation
        public override bool Client { get { return true; } }
        public override bool Server { get; }

        public override async Task<IGameModuleData> Initialize(IExecutionContext context, PlayerData playerData, GameState gameState, RemoteConfig remoteConfig)
        {
            return await playerData.GetOrSet<CounterModuleData>(context);
        }
        #endregion

        [CloudCodeFunction(nameof(IncrementCounterRequest))]
        public async Task<IncrementCounterResponse> IncrementCounter(IExecutionContext context, PlayerData playerData, IncrementCounterRequest request)
        {
            _logger.LogInformation("[IncrementCounterRequest] Starting");
            CounterModuleData counterData = await playerData.GetOrSet<CounterModuleData>(context);
            int valueToIncrement = 1;
            counterData.IncreaseValue(valueToIncrement);
            playerData.SaveModuleDataToCache(counterData);
            IncrementCounterResponse incrementCounterResponse = new IncrementCounterResponse(valueToIncrement);
            return await _moduleRequestHandler.ResolveResponse(request, incrementCounterResponse, context, playerData);
        }
    }
}