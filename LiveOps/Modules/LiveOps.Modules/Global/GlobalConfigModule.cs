using System.Threading;
using System.Threading.Tasks;
using LiveOps.GameApi;
using LiveOps.GameModule;
using LiveOps.ModuleFetchData;
using LiveOps.DTO.GameModule;
using LiveOps.Modules.DTO.Global;
using Microsoft.Extensions.Logging;

namespace LiveOps.Modules.Global
{

    public class GlobalConfigModule : GameModule<GlobalConfigData>
    {
        private readonly ILogger<GlobalConfigModule> _logger;

        public GlobalConfigModule(ILogger<GlobalConfigModule> logger)
        {
            _logger = logger;
        }

        public override async Task<IGameModuleData> InitializeAsync(
            GameApiSession session,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Initializing GlobalConfigModule");
            IRemoteConfig remoteConfig = session.RemoteConfig;
            return await remoteConfig.Get(session.Context, new GlobalConfigData());
        }
    }
}
