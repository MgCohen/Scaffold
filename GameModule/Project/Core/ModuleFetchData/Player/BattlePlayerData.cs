using System.Threading.Tasks;
using GameModule.ModuleFetchData.Unity;
using Microsoft.Extensions.Logging;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;

namespace GameModule.ModuleFetchData
{
    /// <summary>
    /// Subclass handling player fighting states.
    /// </summary>
    public class BattlePlayerData : UnityPlayerData
    {
        private bool _initialized;

        public BattlePlayerData(ILogger<BattlePlayerData> logger, IGameApiClient gameApiClient, string playerId) : base(logger, gameApiClient, playerId)
        {
            _logger = logger;
            _gameApiClient = gameApiClient;
            _playerId = playerId;
        }

        protected override async Task InitializeData(IExecutionContext context)
        {
            if (!_initialized)
            {
                _initialized = true;
                await Initialize(context);
            }
        }
    }
}