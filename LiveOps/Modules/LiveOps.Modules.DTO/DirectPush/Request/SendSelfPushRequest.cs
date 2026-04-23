using LiveOps.Core.DTO.ModuleRequest;

namespace LiveOps.Modules.DTO.ModuleRequests
{
    /// <summary>
    /// Request to send a push notification to the calling player (self-push).
    /// </summary>
    public class SendSelfPushRequest : ModuleRequest<SendPushResponse>
    {
        public string Message { get; set; }
        public string MessageType { get; set; }
    }
}
