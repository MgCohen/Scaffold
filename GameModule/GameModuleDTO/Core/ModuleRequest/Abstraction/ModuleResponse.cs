namespace GameModuleDTO.ModuleRequests
{
    public abstract class ModuleResponse
    {
        public ResponseStatusType StatusType { get; private set; }
        public string Message { get; private set; } = "";
        
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
    }
}