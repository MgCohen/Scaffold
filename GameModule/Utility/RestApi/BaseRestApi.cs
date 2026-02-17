using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Utility.Json;

namespace Utility.RestApi
{
    public class BaseRestApi
    {
        private string url;
        private string bearerToken;
        private readonly HttpClient http = new HttpClient();

        protected ILogger<BaseRestApi> logger;

        public string GetDebugName(string method)
        {
            return $"{GetType().Name}.{method}:";
        }

        public BaseRestApi(ILogger<BaseRestApi> logger)
        {
            this.logger = logger;
        }
        
        public void Initialize(string url, string bearerToken)
        {
            SetUrl(url);
            SetBearerToken(bearerToken);
        }
        
        public void SetUrl(string url)
        {
            logger.LogInformation($"{GetDebugName("SetUrl")} {url}");
            this.url = url;
        }

        public void SetBearerToken(string bearerToken)
        {
            logger.LogInformation($"{GetDebugName("SetBearerToken")} {bearerToken}");
            this.bearerToken = bearerToken;
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        }
        
        private async Task<HttpResponseMessage> PostAsync(string endpoint, HttpContent content)
        {
            try
            {
                string fullUrl = $"{url}{endpoint}";
                logger.LogInformation($"{GetDebugName("PostAsync")} '{fullUrl}', body: {content.ReadAsStringAsync().Result}");
                return await http.PostAsync(fullUrl, content);
            }
            catch (HttpRequestException e)
            {
                logger.LogError($"{GetDebugName("PostAsync")} Request exception in {endpoint}: {e.Message}");
                throw;
            }
        }

        private async Task<HttpResponseMessage> GetAsync(string endpoint)
        {
            try
            {
                string fullUrl = $"{url}{endpoint}";
                logger.LogInformation($"{GetDebugName("GetAsync")} '{fullUrl}'");
                return await http.GetAsync(fullUrl);
            }
            catch (HttpRequestException e)
            {
                logger.LogError($"{GetDebugName("GetAsync")} Request exception in {endpoint}: {e.Message}");
                throw;
            }
        }

        private async Task<string> ReadResponseStringAsync(HttpResponseMessage response, string endpoint, string functionName)
        {
            string responseString = await response.Content.ReadAsStringAsync();
            logger.LogInformation($"{GetDebugName("ReadResponseStringAsync")} {functionName} - {endpoint} with response '{responseString}'");
            if (!response.IsSuccessStatusCode)
            {
                RestData errorData = JsonConvert.DeserializeObject<RestData>(responseString);
                logger.LogError($"{GetDebugName("ReadResponseStringAsync")} {functionName} - {endpoint} {errorData?.error}");
            }
            return responseString;
        }

        private async Task<T> DeserializeResponseAsync<T>(HttpResponseMessage response, string endpoint, string functionName)
        {
            string responseString = await ReadResponseStringAsync(response, endpoint, functionName);
            RestData<T> responseData = responseString.FromJson<RestData<T>>();
            return responseData.Deserialize();
        }

        public async Task<RestData> Post(string endpoint, Dictionary<string, object> body)
        {
            StringContent content = new StringContent(JsonConvert.SerializeObject(body), Encoding.UTF8, "application/json");
            HttpResponseMessage response = await PostAsync(endpoint, content);
            string responseString = await ReadResponseStringAsync(response, endpoint, "f:Post");
            return responseString.FromJson<RestData>();
        }

        public async Task<T> PostRestDataT<T>(string endpoint, Dictionary<string, object> body)
        {
            StringContent content = new StringContent(JsonConvert.SerializeObject(body), Encoding.UTF8, "application/json");
            HttpResponseMessage response = await PostAsync(endpoint, content);
            return await DeserializeResponseAsync<T>(response, endpoint, $"f:Post<{typeof(T)}>");
        }
        
        public async Task<T> PostT<T>(string endpoint, Dictionary<string, object> body)
        {
            StringContent content = new StringContent(JsonConvert.SerializeObject(body), Encoding.UTF8, "application/json");
            HttpResponseMessage response = await PostAsync(endpoint, content);
            string responseString = await ReadResponseStringAsync(response, endpoint, "f:Post");
            return responseString.FromJson<T>();
        }

        public async Task<RestData> PostJson<T>(string endpoint, T body)
        {
            StringContent content = new StringContent(JsonConvert.SerializeObject(body), Encoding.UTF8, "application/json");
            HttpResponseMessage response = await PostAsync(endpoint, content);
            string responseString = await ReadResponseStringAsync(response, endpoint, $"f:PostJson<{typeof(T)}>");
            return responseString.FromJson<RestData>();
        }

        public async Task<T> PostJson<T>(string endpoint, object body)
        {
            StringContent content = new StringContent(JsonConvert.SerializeObject(body), Encoding.UTF8, "application/json");
            HttpResponseMessage response = await PostAsync(endpoint, content);
            return await DeserializeResponseAsync<T>(response, endpoint, $"f:PostJson<{typeof(T)}>");
        }
        public async Task<T> Get<T>(string endpoint)
        {
            HttpResponseMessage response = await GetAsync(endpoint);
            return await DeserializeResponseAsync<T>(response, endpoint, $"f:Get<{typeof(T)}>");
        }

        protected async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request)
        {
            try
            {
                return await http.SendAsync(request);
            }
            catch (Exception e)
            {
                logger.LogError(e, "An exception occurred while sending a custom HTTP request.");
                throw;
            }
        }
    }
}