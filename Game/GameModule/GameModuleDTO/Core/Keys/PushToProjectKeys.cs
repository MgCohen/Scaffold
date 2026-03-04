namespace GameModuleDTO.Keys
{
    /// <summary>
    /// Contains string constants for broadcasting messages across the entire game project instance.
    /// </summary>
    public static class PushToProjectKeys
    {
        /// <summary>Command requesting a global broadcast transmission to all connected nodes.</summary>
        public const string SendProjectMessage = "SendProjectMessage";

        //MessageTypes
        /// <summary>Command instructing a broad environmental socket shutdown.</summary>
        public const string Disconnect = "Disconnect";
    }
}
