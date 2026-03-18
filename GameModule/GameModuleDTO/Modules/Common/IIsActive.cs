namespace GameModuleDTO.Modules.Common
{
    /// <summary>
    /// Interface for modules that can be active or inactive.
    /// </summary>
    public interface IIsActive
    {
        /// <summary>Gets a value indicating whether the module is active.</summary>
        bool IsActive { get; }

        /// <summary>Sets the active state of the module.</summary>
        void SetActive(bool value);
    }
}
