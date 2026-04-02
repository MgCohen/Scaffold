namespace GameModuleDTO.Modules.DirectPush
{
    /// <summary>
    /// Well-known message type keys for player-targeted push notifications.
    /// These constants must match on both client and server to route messages correctly.
    /// </summary>
    public static class PushToPlayerKeys
    {
        /// <summary>Push message type sent when a player's account is detected on multiple sessions.</summary>
        public const string PushDisconnectMultiplePlayerAccounts = "PushDisconnectMultiplePlayerAccounts";
    }
}
