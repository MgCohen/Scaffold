using System.Threading;
using System.Threading.Tasks;
using GameModuleDTO.ModuleRequests;
using Scaffold.LiveOps;

namespace Scaffold.DirectPush
{
    /// <summary>
    /// Client-side service for sending push notifications via the LiveOps backend.
    /// Wraps the typed <see cref="ModuleRequest{T}"/> DTOs and delegates to <see cref="ILiveOpsService"/>.
    /// </summary>
    public sealed class DirectPushClient
    {
        private readonly ILiveOpsService _liveOpsService;

        public DirectPushClient(ILiveOpsService liveOpsService)
        {
            _liveOpsService = liveOpsService;
        }

        /// <summary>
        /// Sends a push notification to the calling player (self-push).
        /// </summary>
        /// <param name="message">The message payload to deliver.</param>
        /// <param name="messageType">The message type key used for routing on the receiving end.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>The server response indicating success or failure.</returns>
        public Task<SendPushResponse> SendSelfPushAsync(string message, string messageType, CancellationToken cancellationToken = default)
        {
            SendSelfPushRequest request = new SendSelfPushRequest
            {
                Message = message,
                MessageType = messageType
            };

            return _liveOpsService.CallAsync(request, cancellationToken);
        }

        /// <summary>
        /// Sends a push notification to a specific player.
        /// Requires a valid AccessKey GUID for server-side validation.
        /// </summary>
        /// <param name="message">The message payload to deliver.</param>
        /// <param name="messageType">The message type key used for routing on the receiving end.</param>
        /// <param name="playerId">The target player's ID.</param>
        /// <param name="guid">The AccessKey GUID for server validation.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>The server response indicating success or failure.</returns>
        public Task<SendPushResponse> SendPlayerPushAsync(string message, string messageType, string playerId, string guid, CancellationToken cancellationToken = default)
        {
            SendPlayerPushRequest request = new SendPlayerPushRequest
            {
                Message = message,
                MessageType = messageType,
                PlayerId = playerId,
                Guid = guid
            };

            return _liveOpsService.CallAsync(request, cancellationToken);
        }

        /// <summary>
        /// Broadcasts a push notification to all players in the project.
        /// Requires a valid AccessKey GUID for server-side validation.
        /// </summary>
        /// <param name="message">The message payload to deliver.</param>
        /// <param name="messageType">The message type key used for routing on the receiving end.</param>
        /// <param name="guid">The AccessKey GUID for server validation.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>The server response indicating success or failure.</returns>
        public Task<SendPushResponse> SendProjectPushAsync(string message, string messageType, string guid, CancellationToken cancellationToken = default)
        {
            SendProjectPushRequest request = new SendProjectPushRequest
            {
                Message = message,
                MessageType = messageType,
                Guid = guid
            };

            return _liveOpsService.CallAsync(request, cancellationToken);
        }
    }
}
