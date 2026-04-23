namespace LiveOps.Modules.DTO.DirectPush
{
    /// <summary>
    /// Well-known message type keys for project-wide push notifications.
    /// These constants must match on both client and server to route messages correctly.
    /// </summary>
    public static class PushToProjectKeys
    {
        /// <summary>Push message type sent to disconnect all active sessions project-wide.</summary>
        public const string Disconnect = "Disconnect";
    }
}
