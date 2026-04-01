namespace GameModuleDTO.ModuleRequests
{
    /// <summary>
    /// Represents the classification format for module responses.
    /// </summary>
    public enum ResponseStatusType
    {
        /// <summary>Indicates successful execution.</summary>
        Success,

        /// <summary>Indicates a failure related to game logic.</summary>
        Failure,

        /// <summary>Indicates an unexpected error state occurred.</summary>
        Error,

        /// <summary>Indicates a critical exception was caught.</summary>
        Exception
    }
}