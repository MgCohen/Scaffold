using Utility.Json;

namespace Utility.RestApi
{
    public class RESTData<T> : RestData
    {
        public T value;

        public T Deserialize()
        {
            if (string.IsNullOrEmpty(data) || IsError)
            {
                return value;
            }
            else
            {
                return value = data.FromJson<T>();
            }
        }
    }
}