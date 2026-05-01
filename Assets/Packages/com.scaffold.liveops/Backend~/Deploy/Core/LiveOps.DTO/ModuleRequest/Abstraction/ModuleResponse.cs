using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace LiveOps.DTO.ModuleRequest
{

    public abstract class ModuleResponse
    {

        public ResponseStatusType StatusType { get; private set; }

        public string Message { get; private set; } = string.Empty;

        [JsonProperty(ItemTypeNameHandling = TypeNameHandling.Auto)]
        public List<ModuleResponse> Responses { get; protected set; } = new List<ModuleResponse>();

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

        public T GetModuleResponse<T>() where T : ModuleResponse
        {
            return (T)Responses.FirstOrDefault(x => x.GetType() == typeof(T));
        }
    }
}
