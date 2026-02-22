namespace GameModuleDTO.ModuleRequests
{
    public abstract class ModuleRequestT<T> : ModuleRequest where T : ModuleResponse
    {
    }
}