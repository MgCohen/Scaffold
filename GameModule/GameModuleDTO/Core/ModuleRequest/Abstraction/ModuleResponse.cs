using Newtonsoft.Json;

namespace GameModuleDTO.ModuleRequests
{
    public abstract class ModuleResponse
    {
        public ResponseStatusType StatusType { get; private set; }
        public string Message { get; private set; } = "";
        public List<ModuleResponse> Responses { get; protected set; } = new List<ModuleResponse>();
        [JsonIgnore]
        public ModuleDataToSave? ModuleDataToSave { get; protected set; }

        public abstract bool IsValid();

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

        protected T GetModuleResponse<T>() where T : ModuleResponse
        {
            return (T)Responses.FirstOrDefault(x => x.GetType() == typeof(T));
        }

        // Override if
        // 1. You only want to save data if StatusType == ResponseStatusType.Success
        // 2. You need to add or remove modules to the list
        public virtual List<string> GetModulesUsed()
        {
            if (ModuleDataToSave == null)
            {
                return [];
            }

            return ModuleDataToSave.ModulesRequired;
        }
    }
}