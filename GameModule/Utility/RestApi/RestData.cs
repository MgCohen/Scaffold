namespace Utility.RestApi
{
    public class RestData
    {
        public string status;
        public string message;
        public string error;
        public string data;
        
        public bool IsSuccess
        {
            get
            {
                return string.IsNullOrEmpty(error);
            }
        }
    
        public bool IsError
        {
            get
            {
                return !string.IsNullOrEmpty(error);
            }
        }
    }
}