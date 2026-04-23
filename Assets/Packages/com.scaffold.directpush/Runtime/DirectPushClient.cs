using System.Threading;
using System.Threading.Tasks;
using LiveOps.Core.DTO.ModuleRequest;
using LiveOps.Modules.DTO.ModuleRequests;
using Scaffold.LiveOps;

namespace Scaffold.DirectPush
{
    public sealed class DirectPushClient
    {
        public DirectPushClient(ILiveOpsService liveOpsService)
        {
            this.liveOpsService = liveOpsService;
        }

        private readonly ILiveOpsService liveOpsService;

        public Task<SendPushResponse> SendSelfPushAsync(string message, string messageType, CancellationToken cancellationToken = default)
        {
            SendSelfPushRequest request = new SendSelfPushRequest
            {
                Message = message,
                MessageType = messageType
            };

            return liveOpsService.CallAsync(request, cancellationToken);
        }

        public Task<SendPushResponse> SendPlayerPushAsync(string message, string messageType, string playerId, string guid, CancellationToken cancellationToken = default)
        {
            SendPlayerPushRequest request = new SendPlayerPushRequest
            {
                Message = message,
                MessageType = messageType,
                PlayerId = playerId,
                Guid = guid
            };

            return liveOpsService.CallAsync(request, cancellationToken);
        }

        public Task<SendPushResponse> SendProjectPushAsync(string message, string messageType, string guid, CancellationToken cancellationToken = default)
        {
            SendProjectPushRequest request = new SendProjectPushRequest
            {
                Message = message,
                MessageType = messageType,
                Guid = guid
            };

            return liveOpsService.CallAsync(request, cancellationToken);
        }
    }
}
