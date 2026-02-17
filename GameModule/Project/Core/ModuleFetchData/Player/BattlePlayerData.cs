using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;

namespace GameModule.ModuleFetchData
{
    public class BattlePlayerData : PlayerData
    {
        private bool initialized;
        
        public BattlePlayerData(ILogger<BattlePlayerData> logger, IGameApiClient gameApiClient, string playerId) : base(logger, gameApiClient)
        {
            this.logger = logger;
            this.gameApiClient = gameApiClient;
            this.playerId = playerId;
        }
        
        protected override async Task InitializeData(IExecutionContext context)
        {
            if (!initialized)
            {
                initialized = true;
                await Initialize(context);
            }
        }
    }
}