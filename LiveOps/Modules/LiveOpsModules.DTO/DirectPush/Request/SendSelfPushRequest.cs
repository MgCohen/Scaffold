using LiveOps.DTO.GameApi;
using LiveOps.DTO.ModuleRequest;

namespace LiveOpsModules.DTO.ModuleRequests
{
    /// <summary>
    /// Request to send a push notification to the calling player (self-push).
    /// </summary>
    [UsesGameApi]
    [GameApiKey("SendSelfPush")]
    public class SendSelfPushRequest : ModuleRequest<SendPushResponse>
    {
        public string Message { get; set; }
        public string MessageType { get; set; }
    }
}
