using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Unity.Services.CloudCode;

namespace Scaffold.CloudCode
{
    public sealed class CloudCodeModuleService : ICloudCodeModuleService
    {
        public async Task<T> CallEndpointAsync<T>(string module, string endpoint, int maxRetries = 2, int retryCall = 2, Dictionary<string, object> payload = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(module))
            {
                throw new ArgumentException("Module name is required.", nameof(module));
            }

            if (string.IsNullOrWhiteSpace(endpoint))
            {
                throw new ArgumentException("Endpoint name is required.", nameof(endpoint));
            }

            return await CallAsync<T>(module, endpoint, maxRetries, retryCall, payload, cancellationToken);
        }

        internal async Task<T> CallAsync<T>(string module, string endpoint, int maxRetries = 2, int retryCall = 2, Dictionary<string, object> payload = null, CancellationToken cancellationToken = default)
        {
            UnityEngine.Debug.Log($"[CloudCode] Calling module '{module}' endpoint '{endpoint}'...");
            cancellationToken.ThrowIfCancellationRequested();
            Dictionary<string, object> finalPayload = payload ?? new Dictionary<string, object>();
            string response = await CloudCodeService.Instance.CallModuleEndpointAsync(module, endpoint, finalPayload);
            return Deserialize<T>(response);
        }

        private T Deserialize<T>(string response)
        {
            UnityEngine.Debug.Log($"[CloudCode] Deserializing response to '{typeof(T).Name}'... Raw '{response}'");
            JsonSerializerSettings settings = new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.Auto,
                ContractResolver = new CamelCasePropertyNamesContractResolver(),
            };
            return JsonConvert.DeserializeObject<T>(response, settings);
        }
    }
}
