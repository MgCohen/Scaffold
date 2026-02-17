using GameModuleDTO.GameModule;
using Utility.List;

namespace GameModuleDTO.Response
{
    public class Response
    {
        public enum ResponseType { Success, Failure, Error, Exception }
        
        public ResponseType status;
        public string message;
        public List<Response> responses = new List<Response>();
        public List<IGameModuleData> modules = new List<IGameModuleData>();

        public Response(){ }

        public bool IsSuccess()
        {
            return status == ResponseType.Success;
        }

        public void SetResponse(ResponseType status, string message)
        {
            this.status = status;
            this.message = message;
        }
        
        public void SetFailureResponse(string message)
        {
            SetResponse(ResponseType.Failure, message);
        }
        
        public void SetErrorResponse(string message)
        {
            SetResponse(ResponseType.Error, message);
        }
        
        public void SetExceptionResponse(string message)
        {
            SetResponse(ResponseType.Exception, $"Failed with exception: \n{message}");
        }

        public void AddModuleData(IGameModuleData module)
        {
            if (module == null)
            {
                return;
            }

            for (int i = 0; i < modules.Count; i++)
            {
                if (modules[i].GetType() == module.GetType())
                {
                    modules[i] = module;
                    return;
                }
            }

            modules.Add(module);
        }

        public void AddModulesData(List<IGameModuleData> moduleList)
        {
            if (moduleList.IsNullOrEmpty())
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
            if (moduleList.IsNullOrEmpty())
            {
                return;
            }
            
            AddModulesData(moduleList);
            moduleList.Clear();
        }
        
        public void AddResponse(Response response)
        {
            if (response == null)
            {
                return;
            }
            
            AddChildModulesData(response.modules);
            responses.Add(response);
        }
        
        protected T FirstModuleAsT<T>() where T : IGameModuleData
        {
            return (T)modules.FirstOrDefault(x => x.GetType() == typeof(T));
        }
        
        protected T FirstResponseAsT<T>() where T : Response
        {
            return (T)responses.FirstOrDefault(x => x.GetType() == typeof(T));
        }
    }
}