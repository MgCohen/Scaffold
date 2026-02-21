using GameModuleDTO.GameModule;

namespace GameModuleDTO.ModuleRequests
{
    public abstract class ModuleResponse
    {
        public ResponseStatusType StatusType { get; private set; }
        public string Message { get; private set; } = "";
        public List<ModuleResponse> Responses { get; protected set; }
        public List<IGameModuleData> GameModuleDatas = new List<IGameModuleData>();
        
        public bool IsSuccess()
        {
            return StatusType == ResponseStatusType.Success;
        }

        public void SetResponse(ResponseStatusType status, string message)
        {
            StatusType = status;
            Message = message;
        }
        public void SetResponseFailure(string message)
        {
            SetResponse(ResponseStatusType.Failure, message);
        }
        
        public void SetResponseError(string message)
        {
            SetResponse(ResponseStatusType.Error, message);
        }
        
        public void SetResponseException(string message)
        {
            SetResponse(ResponseStatusType.Exception, $"Failed with exception: \n{message}");
        }

        public void AddModulesData(List<IGameModuleData> moduleList)
        {
            if (moduleList.Any())
            {
                return;
            }
            
            foreach (IGameModuleData module in moduleList)
            {
                AddModuleData(module);
            }
        }
        
        public void AddChildModulesData(List<IGameModuleData> moduleList)
        {
            if (moduleList.Any())
            {
                return;
            }
            
            AddModulesData(moduleList);
            moduleList.Clear();
        }

        public void AddModuleData(IGameModuleData module)
        {
            if (module == null)
            {
                return;
            }

            for (int i = 0; i < GameModuleDatas.Count; i++)
            {
                if (GameModuleDatas[i].GetType() == module.GetType())
                {
                    GameModuleDatas[i] = module;
                    return;
                }
            }

            GameModuleDatas.Add(module);
        }
        
        
        public void AddResponse(ModuleResponse response)
        {
            if (response == null)
            {
                return;
            }
            
            AddChildModulesData(response.GameModuleDatas);
            Responses.Add(response);
        }
        
        protected T GetModuleResponse<T>() where T : ModuleResponse
        {
            return (T)Responses.FirstOrDefault(x => x.GetType() == typeof(T));
        }
        
        protected T GetModuleData<T>() where T : IGameModuleData
        {
            return (T)GameModuleDatas.FirstOrDefault(x => x.GetType() == typeof(T));
        }
    }
}