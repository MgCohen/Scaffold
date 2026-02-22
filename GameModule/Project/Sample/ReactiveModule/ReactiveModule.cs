using System.Threading.Tasks;
using GameModule.GameModule;
using GameModule.ModuleFetchData;
using GameModuleDTO.GameModule;
using GameModuleDTO.Sample.ReactiveModule;
using Microsoft.Extensions.Logging;
using Unity.Services.CloudCode.Core;

namespace GameModule.Sample
{
    public class ReactiveModule: IGameModule
    {
        public ReactiveModule(ILogger<ReactiveModule> logger, IExecutionContext context, PlayerData playerData, CounterModule counterModule)
        {
            _logger = logger;
            _counterModule = counterModule;
            _playerData = playerData;
            _context = context;
        }
                
        private readonly ILogger<ReactiveModule> _logger;
        private CounterModule _counterModule;
        private PlayerData _playerData;
        private IExecutionContext _context;
        
        #region IGameModule implementation
        public bool Client { get { return true; } }
        public bool Server { get; }
        public string Key { get { return ReactiveModuleData.StaticKey; } }
        
        public async Task<IGameModuleData> Initialize(IExecutionContext context, PlayerData playerData, GameState gameState, RemoteConfig remoteConfig)
        {
            _counterModule.OnValueChange += OnCounterValueChange;
            return await playerData.GetOrSet<ReactiveModuleData>(context);
        }

        private async void OnCounterValueChange(int value)
        {
            ReactiveModuleData reactiveModuleData = await _playerData.GetOrSet<ReactiveModuleData>(_context);
            reactiveModuleData.IncreaseValue(value);
        }

        #endregion
    }
}