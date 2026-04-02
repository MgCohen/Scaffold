using System.Threading.Tasks;
using GameModule.ModuleFetchData;
using GameModule.Response;
using GameModuleDTO.ModuleRequests;
using Microsoft.Extensions.Logging;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;

namespace GameModule.Modules.DirectPush
{
    using AccessKey = AccessKey.AccessKey;

    /// <summary>
    /// Cloud Code push notification service: handles player-targeted and project-wide push messages.
    /// </summary>
    public class DirectPushService
    {
        private readonly ILogger<DirectPushService> _logger;
        private readonly ModuleRequestHandler _handler;

        public DirectPushService(ILogger<DirectPushService> logger, ModuleRequestHandler handler)
        {
            _logger = logger;
            _handler = handler;
        }

        /// <summary>
        /// Sends a push notification to the calling player (self-targeted).
        /// No AccessKey validation is required since the player is messaging themselves.
        /// </summary>
        /// <param name="context">The current cloud execution context.</param>
        /// <param name="pushClient">The Unity push notification client.</param>
        /// <param name="request">The self-push request payload.</param>
        /// <returns>A response indicating whether the push was sent successfully.</returns>
        [CloudCodeFunction(nameof(SendSelfPushRequest))]
        public async Task<SendPushResponse> SendSelfPush(
            IExecutionContext context, PushClient pushClient, SendSelfPushRequest request)
        {
            _logger.LogInformation(
                "[DirectPush] Sending self push type '{MessageType}' to playerId '{PlayerId}'",
                request.MessageType, context.PlayerId);

            await pushClient.SendPlayerMessageAsync(
                context, request.Message, request.MessageType, context.PlayerId);

            SendPushResponse response = new SendPushResponse();
            response.SetResponse(ResponseStatusType.Success, "Player message sent");
            return await _handler.ResolveResponse(context, request, response);
        }

        /// <summary>
        /// Sends a push notification to a specific player by ID.
        /// Requires AccessKey validation via the request's GUID.
        /// </summary>
        /// <param name="context">The current cloud execution context.</param>
        /// <param name="gameState">The game state used for AccessKey validation.</param>
        /// <param name="pushClient">The Unity push notification client.</param>
        /// <param name="request">The player-push request payload containing target player ID and GUID.</param>
        /// <returns>A response indicating whether the push was sent or access was denied.</returns>
        [CloudCodeFunction(nameof(SendPlayerPushRequest))]
        public async Task<SendPushResponse> SendPlayerPush(
            IExecutionContext context, IGameState gameState, PushClient pushClient, SendPlayerPushRequest request)
        {
            bool valid = await AccessKey.ValidServer(gameState, context, request.Guid);
            if (!valid)
            {
                SendPushResponse errorResponse = new SendPushResponse();
                errorResponse.SetResponseFailure("Invalid access");
                return errorResponse;
            }

            _logger.LogInformation(
                "[DirectPush] Sending player push type '{MessageType}' to playerId '{PlayerId}'",
                request.MessageType, request.PlayerId);

            await pushClient.SendPlayerMessageAsync(
                context, request.Message, request.MessageType, request.PlayerId);

            SendPushResponse response = new SendPushResponse();
            response.SetResponse(ResponseStatusType.Success, "Player message sent");
            return await _handler.ResolveResponse(context, request, response);
        }

        /// <summary>
        /// Broadcasts a push notification to all players in the project.
        /// Requires AccessKey validation via the request's GUID.
        /// </summary>
        /// <param name="context">The current cloud execution context.</param>
        /// <param name="gameState">The game state used for AccessKey validation.</param>
        /// <param name="pushClient">The Unity push notification client.</param>
        /// <param name="request">The project-push request payload containing the GUID.</param>
        /// <returns>A response indicating whether the broadcast was sent or access was denied.</returns>
        [CloudCodeFunction(nameof(SendProjectPushRequest))]
        public async Task<SendPushResponse> SendProjectPush(
            IExecutionContext context, IGameState gameState, PushClient pushClient, SendProjectPushRequest request)
        {
            bool valid = await AccessKey.ValidServer(gameState, context, request.Guid);
            if (!valid)
            {
                SendPushResponse errorResponse = new SendPushResponse();
                errorResponse.SetResponseFailure("Invalid access");
                return errorResponse;
            }

            _logger.LogInformation(
                "[DirectPush] Broadcasting project push type '{MessageType}'",
                request.MessageType);

            await pushClient.SendProjectMessageAsync(
                context, request.Message, request.MessageType);

            SendPushResponse response = new SendPushResponse();
            response.SetResponse(ResponseStatusType.Success, "Project message sent");
            return await _handler.ResolveResponse(context, request, response);
        }
    }
}
