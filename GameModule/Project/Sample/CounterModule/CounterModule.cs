using System;
using System.Threading.Tasks;
using GameModule.GameModule;
using GameModule.ModuleFetchData;
using GameModule.Response;
using GameModuleDTO.GameModule;
using GameModuleDTO.ModuleRequests;
using GameModuleDTO.Sample.CounterModule;
using Microsoft.Extensions.Logging;
using Unity.Services.CloudCode.Core;

namespace GameModule.Sample
{
    public class CounterModule : IGameModule
    {
        public CounterModule(ILogger<CounterModule> logger)
        {
            _logger = logger;
        }
                
        private readonly ILogger<CounterModule> _logger;
        
        #region IGameModule implementation
        public bool Client { get { return true; } }
        public bool Server { get; }
        public string Key { get { return CounterModuleData.StaticKey; } }
        
        public async Task<IGameModuleData> Initialize(IExecutionContext context, PlayerData playerData, GameState gameState, RemoteConfig remoteConfig)
        {
            return await playerData.GetOrSet<CounterModuleData>(context);
        }
        #endregion
        
        //TODO: Adicionar Request nesse Action ou isso deve passar pelo handler?
        public Action<int> OnValueChange { get; set; }
        
        [CloudCodeFunction(nameof(IncrementCounterRequest))]
        public async Task<IncrementCounterResponse> InitializeModules(IExecutionContext context, GameState gameState, PlayerData playerData, IncrementCounterRequest request)
        {
            _logger.LogInformation("[IncrementCounterRequest] Starting");
            CounterModuleData counterData = await playerData.GetOrSet<CounterModuleData>(context);
            int valueToIncrement = IncreaseValue(counterData, 1);
            IncrementCounterResponse incrementCounterResponse = new IncrementCounterResponse(valueToIncrement);
            return await request.ResolveResponse(incrementCounterResponse, context, playerData);
        }

        private int IncreaseValue(CounterModuleData counterData, int valueToIncrement)
        {
            counterData.IncreaseValue(valueToIncrement);
            OnValueChange.Invoke(valueToIncrement);
            return valueToIncrement;
        }
    }
}