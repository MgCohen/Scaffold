using GameModuleDTO.Json;
using GameModuleDTO.Keys;

namespace GameModuleDTO.ModuleRequests
{
    public abstract class ModuleRequest
    {
        public virtual string AuthKey { get; protected set; }
        public virtual string ModuleName { get { return ModuleKeys.DefaultModuleName; } }
        public virtual int RetryCall { get; protected set; } = 2; // In secconds
        public virtual int MaxRetries { get; protected set; } = 2;

        public string FunctionName
        {
            get
            {
                return GetType().Name;
            }
        }

        public bool HasAuth { get { return !string.IsNullOrEmpty(AuthKey); } }

        public abstract void AssertModule();

        // In case of override serialization
        protected virtual string SerializeModule(ModuleResponse moduleResponse)
        {
            return moduleResponse.ToJson();
        }
        
        public virtual string GetResponse(GameDataResponse moduleResponse) 
        {
            return SerializeModule(moduleResponse); 
        }
    }
}