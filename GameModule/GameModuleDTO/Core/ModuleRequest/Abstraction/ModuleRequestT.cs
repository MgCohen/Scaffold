namespace GameModuleDTO.ModuleRequests
{
    public abstract class ModuleRequestT<T> : ModuleRequest where T : ModuleResponse
    {
        public virtual string GetResponse(T moduleResponse)
        {
            return SerializeModule(moduleResponse);
        }
    }
}