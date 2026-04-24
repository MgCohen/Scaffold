using System.Threading.Tasks;
using LiveOps.GameApi;
using LiveOps.DTO.ModuleRequest;
using LiveOps.Modules.DTO.ModuleRequests;
using Microsoft.Extensions.Logging;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;

namespace LiveOps.Modules.DirectPush
{

    public sealed class DirectPushSelfPushHandler : IGameApiHandler<SendSelfPushRequest, SendPushResponse>
    {
        private readonly ILogger<DirectPushSelfPushHandler> _logger;
        private readonly PushClient _pushClient;

        public DirectPushSelfPushHandler(ILogger<DirectPushSelfPushHandler> logger, PushClient pushClient)
        {
            _logger = logger;
            _pushClient = pushClient;
        }

        public async Task<SendPushResponse> HandleAsync(GameApiSession session, SendSelfPushRequest request)
        {
            IExecutionContext context = session.Context;

            _logger.LogInformation(
                "[DirectPush] Sending self push type '{MessageType}' to playerId '{PlayerId}'",
                request.MessageType, context.PlayerId);

            await _pushClient.SendPlayerMessageAsync(
                context, request.Message, request.MessageType, context.PlayerId);

            SendPushResponse response = new SendPushResponse();
            response.SetResponse(ResponseStatusType.Success, "Player message sent");
            return response;
        }
    }
}
