namespace GameModuleDTO.ModuleRequests
{
    /// <summary>
    /// Represents a strongly-typed module request returning a specific response type.
    /// </summary>
    /// <typeparam name="T">The expected response type inheriting from ModuleResponse.</typeparam>
    public abstract class ModuleRequest<T> : ModuleRequest where T : ModuleResponse
    {
    }
}