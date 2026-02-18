namespace GameModuleDTO.ModuleRequests
{
    public class ResponseHandler
    {
        public T Handle<T>(ModuleRequest moduleRequest) where T : ModuleResponse
        {
            return default;
        }
    }
}