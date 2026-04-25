using System.Threading.Tasks;
using LiveOps.GameApi;
using LiveOps.ModuleFetchData;
using LiveOps.DTO.ModuleRequest;
using LiveOps.ServerAuth;
using LiveOps.Modules.DTO.ModuleRequests;
using Microsoft.Extensions.Logging;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;

namespace LiveOps.Modules.DirectPush
{

    public sealed class DirectPushProjectPushHandler : IGameApiHandler<SendProjectPushRequest, SendPushResponse>
    {
        private readonly ILogger<DirectPushProjectPushHandler> _logger;
        private readonly PushClient _pushClient;
        private readonly IServerAuth _serverAuth;

        public DirectPushProjectPushHandler(
            ILogger<DirectPushProjectPushHandler> logger,
            PushClient pushClient,
            IServerAuth serverAuth)
        {
            _logger = logger;
            _pushClient = pushClient;
            _serverAuth = serverAuth;
        }

        public async Task<SendPushResponse> HandleAsync(GameApiSession session, SendProjectPushRequest request)
        {
            IExecutionContext context = session.Context;
            IGameState gameState = session.GameState;

            bool valid = await _serverAuth
                .IsValidForServerAccessAsync(gameState, context, request.Guid)
                .ConfigureAwait(false);
            if (!valid)
            {
                SendPushResponse errorResponse = new SendPushResponse();
                errorResponse.SetResponseFailure("Invalid access");
                return errorResponse;
            }

            _logger.LogInformation(
                "[DirectPush] Broadcasting project push type '{MessageType}'",
                request.MessageType);

            await _pushClient.SendProjectMessageAsync(
                context, request.Message, request.MessageType);

            SendPushResponse response = new SendPushResponse();
            response.SetResponse(ResponseStatusType.Success, "Project message sent");
            return response;
        }
    }
}
