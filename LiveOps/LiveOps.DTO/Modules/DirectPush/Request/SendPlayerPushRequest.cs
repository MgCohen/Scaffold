namespace GameModuleDTO.ModuleRequests
{
    /// <summary>
    /// Request to send a push notification to a specific player.
    /// Requires AccessKey validation via <see cref="Guid"/>.
    /// </summary>
    public class SendPlayerPushRequest : ModuleRequest<SendPushResponse>
    {
        public string Message { get; set; }
        public string MessageType { get; set; }
        public string PlayerId { get; set; }
        public string Guid { get; set; }
    }
}
