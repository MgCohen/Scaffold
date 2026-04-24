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
    /// <summary>
    /// GameApi handler: push to a specific player (AccessKey required).
    /// </summary>
    public sealed class DirectPushPlayerPushHandler : IGameApiHandler<SendPlayerPushRequest, SendPushResponse>
    {
        private readonly ILogger<DirectPushPlayerPushHandler> _logger;
        private readonly PushClient _pushClient;
        private readonly IServerAuth _serverAuth;

        public DirectPushPlayerPushHandler(
            ILogger<DirectPushPlayerPushHandler> logger,
            PushClient pushClient,
            IServerAuth serverAuth)
        {
            _logger = logger;
            _pushClient = pushClient;
            _serverAuth = serverAuth;
        }

        public async Task<SendPushResponse> HandleAsync(GameApiSession session, SendPlayerPushRequest request)
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
                "[DirectPush] Sending player push type '{MessageType}' to playerId '{PlayerId}'",
                request.MessageType, request.PlayerId);

            await _pushClient.SendPlayerMessageAsync(
                context, request.Message, request.MessageType, request.PlayerId);

            SendPushResponse response = new SendPushResponse();
            response.SetResponse(ResponseStatusType.Success, "Player message sent");
            return response;
        }
    }
}
