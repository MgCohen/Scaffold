using System.Threading.Tasks;
using GameModule.GameModule;
using GameModule.ModuleFetchData;
using GameModuleDTO.GameModule;
using GameModuleDTO.Sample.ReactiveModule;
using GameModule.Response;
using GameModule.Signal;
using Microsoft.Extensions.Logging;
using Unity.Services.CloudCode.Core;
using GameModuleDTO.ModuleRequests;

namespace GameModule.Sample
{
    /// <summary>
    /// Example system demonstrating signal subscription capabilities.
    /// </summary>
    public class ReactiveModule : GameModule<ReactiveModuleData>
    {
        public ReactiveModule(ILogger<ReactiveModule> logger, ModuleRequestHandler moduleRequestHandler, IExecutionContext context, PlayerData playerData, SignalModule signalModule)
        {
            _logger = logger;
            _moduleRequestHandler = moduleRequestHandler;
            _signalModule = signalModule;
            _playerData = playerData;
            _context = context;
        }

        private readonly ILogger<ReactiveModule> _logger;
        private readonly ModuleRequestHandler _moduleRequestHandler;
        private readonly SignalModule _signalModule;
        private readonly PlayerData _playerData;
        private readonly IExecutionContext _context;

        #region IGameModule implementation
        public override bool Client { get { return true; } }
        public override bool Server { get; }

        public override async Task<IGameModuleData> Initialize(IExecutionContext context, PlayerData playerData, GameState gameState, RemoteConfig remoteConfig)
        {
            _signalModule.Subscribe<IncrementCounterRequest>(OnCounterRequestResolve);
            return await playerData.GetOrSet<ReactiveModuleData>(context, new ReactiveModuleData());
        }

        private async void OnCounterRequestResolve(IncrementCounterRequest request)
        {
            ReactiveModuleData reactiveModuleData = await _playerData.GetOrSet<ReactiveModuleData>(_context, new ReactiveModuleData());
            _playerData.AddToCache(reactiveModuleData);

            int valueToIncrement = 2;
            reactiveModuleData.IncreaseValueB(valueToIncrement);
            _moduleRequestHandler.AddResponse(new ReactiveCounterResponse(reactiveModuleData.valueB));
        }
        #endregion
    }
}