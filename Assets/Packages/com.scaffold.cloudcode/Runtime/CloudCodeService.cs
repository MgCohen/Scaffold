using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using UnityEngine;

namespace Scaffold.CloudCode
{
    internal sealed class CloudCodeService : ICloudCodeService
    {
        internal CloudCodeService(CloudCodeSettings settings, ICloudCodeCallHandler callHandler, CloudCodeOptimisticHandlerRegistry optimisticRegistry)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            jsonSettings = new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.Auto,
                ContractResolver = new CamelCasePropertyNamesContractResolver(),
            };
            this.callHandler = callHandler ?? throw new ArgumentNullException(nameof(callHandler));
            this.optimisticRegistry = optimisticRegistry ?? throw new ArgumentNullException(nameof(optimisticRegistry));
        }

        private readonly JsonSerializerSettings jsonSettings;
        private readonly ICloudCodeCallHandler callHandler;
        private readonly CloudCodeOptimisticHandlerRegistry optimisticRegistry;

        public async Task<T> CallEndpointAsync<T>(string module, string endpoint, object payload = null, CancellationToken cancellationToken = default)
        {
            ValidateModuleEndpoint(module, endpoint);
            cancellationToken.ThrowIfCancellationRequested();
            Dictionary<string, object> finalPayload = payload == null
                ? new Dictionary<string, object>()
                : new Dictionary<string, object> { { "request", payload } };
            Task<string> serverTask = callHandler.InvokeAsync(module, endpoint, finalPayload, cancellationToken);
            if (TryGetOptimisticResponse<T>(module, endpoint, finalPayload, out object optimisticResponse, out IRequestHandler handler, out object requestBody))
            {
                _ = ReconcileAfterOptimisticReturnAsync<T>(handler, module, endpoint, finalPayload, serverTask, requestBody, optimisticResponse);
                return CoerceOptimisticToT<T>(optimisticResponse);
            }

            string response = await serverTask.ConfigureAwait(false);
            return DeserializeResponse<T>(response);
        }

        private bool TryGetOptimisticResponse<TResponse>(string module, string endpoint, Dictionary<string, object> payload, out object optimisticResponse, out IRequestHandler handler, out object requestBody)
        {
            optimisticResponse = null;
            handler = null;
            requestBody = null;
            if (!TryGetRequestBody(payload, out object body))
            {
                return false;
            }

            if (!TryResolveOptimisticHandler<TResponse>(module, endpoint, body, out IRequestHandler found, out object optimistic))
            {
                return false;
            }

            optimisticResponse = optimistic;
            handler = found;
            requestBody = body;
            return true;
        }

        private bool TryGetRequestBody(Dictionary<string, object> payload, out object body)
        {
            body = null;
            if (payload == null || payload.Count == 0)
            {
                return false;
            }

            return payload.TryGetValue("request", out body) && body != null;
        }

        private bool TryResolveOptimisticHandler<TResponse>(string module, string endpoint, object body, out IRequestHandler handler, out object optimisticResponse)
        {
            handler = null;
            optimisticResponse = null;
            Type requestType = body.GetType();
            Type responseType = typeof(TResponse);
            if (!optimisticRegistry.TryGetHandler(requestType, responseType, out IRequestHandler found) || found == null)
            {
                return false;
            }

            if (!found.TryMatch(module, endpoint, body))
            {
                return false;
            }

            optimisticResponse = found.GetOptimisticResponse(body);
            handler = found;
            return true;
        }

        private async Task ReconcileAfterOptimisticReturnAsync<TResponse>(IRequestHandler handler, string module, string endpoint, IReadOnlyDictionary<string, object> wirePayload, Task<string> serverTask, object requestBody, object optimisticResponse)
        {
            try
            {
                string json = await serverTask.ConfigureAwait(false);
                TResponse serverResponse = JsonConvert.DeserializeObject<TResponse>(json, jsonSettings);
                handler.OnDeferredServerResponse(requestBody, optimisticResponse, serverResponse, wirePayload);
            }
            catch (OperationCanceledException)
            {
                Debug.LogError($"Cloud Code call was cancelled after an optimistic return for {module}/{endpoint}.");
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        private T CoerceOptimisticToT<T>(object optimisticResponse)
        {
            if (optimisticResponse is T typed)
            {
                return typed;
            }

            if (optimisticResponse == null && default(T) == null)
            {
                return default;
            }

            throw new InvalidOperationException(
                $"Optimistic response type {optimisticResponse?.GetType().Name ?? "null"} is not compatible with {typeof(T).Name}.");
        }

        private T DeserializeResponse<T>(string response)
        {
            return JsonConvert.DeserializeObject<T>(response, jsonSettings);
        }

        private void ValidateModuleEndpoint(string module, string endpoint)
        {
            if (string.IsNullOrWhiteSpace(module))
            {
                throw new ArgumentException("Module name is required.", nameof(module));
            }

            if (string.IsNullOrWhiteSpace(endpoint))
            {
                throw new ArgumentException("Endpoint name is required.", nameof(endpoint));
            }
        }
    }
}
