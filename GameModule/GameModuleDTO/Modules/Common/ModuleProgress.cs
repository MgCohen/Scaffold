namespace GameModuleDTO.Modules.Common
{
    /// <summary>
    /// Holds general progress data for a module.
    /// </summary>
    public class ModuleProgress
    {
        public string Id { get; set; } = string.Empty;
        public ModuleStatus Status { get; set; }
        public ModuleProgressState State { get; set; }
    }
}
