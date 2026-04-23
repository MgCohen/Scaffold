using LiveOps.Core.DTO.ModuleRequest;

namespace LiveOps.Modules.DTO.ModuleRequests
{
    /// <summary>
    /// Request to broadcast a push notification to the entire project.
    /// Requires AccessKey validation via <see cref="Guid"/>.
    /// </summary>
    public class SendProjectPushRequest : ModuleRequest<SendPushResponse>
    {
        public string Message { get; set; }
        public string MessageType { get; set; }
        public string Guid { get; set; }
    }
}
