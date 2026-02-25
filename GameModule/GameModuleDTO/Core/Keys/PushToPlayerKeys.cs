namespace GameModuleDTO.Keys
{
    /// <summary>
    /// Groups string constants responsible for individual user notification routing.
    /// </summary>
    public static class PushToPlayerKeys
    {
        /// <summary>Command requesting a message dispatch back to the caller.</summary>
        public const string SendToSelfPlayerMessage = "SendToSelfPlayerMessage";

        //MessageTypes
        /// <summary>Command forcing multi-session closures for a single user.</summary>
        public const string PushDisconnectMultiplePlayerAccounts = "PushDisconnectMultiplePlayerAccounts";

        //---//
        /// <summary>General outward command requesting a message transmission to a specified target.</summary>
        public const string SendPlayerMessage = "SendPlayerMessage";

        //Self MessageTypes
        /// <summary>Command initiating an immediate socket or session termination.</summary>
        public const string Disconnect = "Disconnect";
    }
}