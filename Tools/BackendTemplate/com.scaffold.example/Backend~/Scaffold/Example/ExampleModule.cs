using System.Threading;
using System.Threading.Tasks;
using LiveOps.DTO.GameModule;
using LiveOps.DTO.ModuleRequest;
using LiveOps.GameApi;
using LiveOps.GameModule;
using LiveOps.ModuleFetchData;
using LiveOps.Modules.DTO.Example;
using Microsoft.Extensions.Logging;
using Unity.Services.CloudCode.Core;

namespace LiveOps.Modules.Example
{
    public class ExampleModule : GameModule<ExampleData>, IGameApiHandler<DoThingRequest, DoThingResponse>
    {
        private readonly ILogger<ExampleModule> _logger;

        public ExampleModule(ILogger<ExampleModule> logger)
        {
            _logger = logger;
        }

        public override async Task<IGameModuleData> InitializeAsync(
            GameApiSession session,
            CancellationToken cancellationToken = default)
        {
            IExecutionContext context = session.Context;
            IPlayerData player = session.Player;
            IRemoteConfig remoteConfig = session.RemoteConfig;
            ExamplePersistence persistence = await player.GetOrSet(context, new ExamplePersistence());
            ExampleConfig config = await remoteConfig.Get(context, new ExampleConfig());
            return new ExampleData(persistence, config);
        }

        public async Task<DoThingResponse> HandleAsync(GameApiSession session, DoThingRequest request)
        {
            IExecutionContext context = session.Context;
            IPlayerData player = session.Player;
            IRemoteConfig remoteConfig = session.RemoteConfig;

            _logger.LogInformation("[DoThingRequest] {Message}", request.Message);
            ExamplePersistence persistence = await player.GetOrSet(context, new ExamplePersistence());
            ExampleConfig config = await remoteConfig.Get(context, new ExampleConfig());
            persistence.CallCount += 1;
            await player.Set(context, persistence);
            ExampleData data = new ExampleData(persistence, config);
            return new DoThingResponse(data);
        }
    }
}
