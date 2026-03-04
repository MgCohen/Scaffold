namespace GameModuleDTO.Keys
{
    /// <summary>
    /// Maintains system-level string constants for core game modules.
    /// </summary>
    public static class ModuleKeys
    {
        /// <summary>Primary global base implementation name.</summary>
        public const string DefaultModuleName = "GameModule";
        /// <summary>General tracker property for the active match state.</summary>
        public const string GameState = "GameState";

        #region GameState
        //Keys
        /// <summary>Authentication header token for cloud services.</summary>
        public const string FirebaseBearerToken = "FirebaseBearerToken";
        /// <summary>Authentication header token for Unity cloud processes.</summary>
        public const string UnityToken = "UnityToken";
        /// <summary>Restricted access key for administrative server endpoints.</summary>
        public const string AdminFunctionsKey = "AdminFunctionsKey";
        /// <summary>Secure password string for validating administrative requests.</summary>
        public const string AdminFunctionsSecretKey = "AdminFunctionsSecretKey";

        //Id
        /// <summary>Identifier representing the foundational login process.</summary>
        public const string Auth = "Auth";
        /// <summary>Identifier for the player guild entity.</summary>
        public const string Guild = "Guild";
        /// <summary>Identifier storing the user contact address.</summary>
        public const string EmailId = "EmailId";
        /// <summary>Identifier holding the linked monetary resource storage.</summary>
        public const string WalletId = "WalletId";
        /// <summary>Identifier retaining the visible public screen name.</summary>
        public const string UsernameId = "UsernameId";
        #endregion
    }
}
