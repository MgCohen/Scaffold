namespace GameModuleDTO.GameModule
{
    /// <summary>
    /// The core interface required for all module configuration mappings.
    /// The main goal is establishing a mandatory key property constraint across all feature modules.
    /// </summary>
    /// <remarks>
    /// Crucial for cross-platform network bindings and runtime dictionary lookups natively.
    /// </remarks>
    public interface IGameModuleData
    {
        /// <summary>
        /// Gets the instance identifier string mapping the current loaded type logically efficiently.
        /// </summary>
        public string Key { get; }
    }
}
