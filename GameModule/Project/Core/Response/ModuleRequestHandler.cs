using GameModuleDTO.ModuleRequests;

namespace GameModule.Response
{
    public class ModuleRequestHandler
    {
        public ModuleRequest Request { get; private set; }
        
        public void SetCurrentRequest(ModuleRequest request)
        {
            Request = request;
        }
    }
}