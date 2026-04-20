using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Scaffold.CloudCode
{
    internal sealed class CloudCodeService : ICloudCodeService
    {
        internal CloudCodeService(CloudCodeSettings settings, CloudCodeSdkCallHandler sdkCallHandler, CloudCodeOptimisticHandlerRegistry optimisticRegistry, CloudCodeErrorHandler cloudCodeErrorHandler)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            if (sdkCallHandler == null)
            {
                throw new ArgumentNullException(nameof(sdkCallHandler));
            }

            jsonSettings = new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.Auto,
                ContractResolver = new CamelCasePropertyNamesContractResolver(),
            };
            this.settings = settings;
            callHandler = BuildCallHandlerChain(sdkCallHandler);
            this.optimisticRegistry = optimisticRegistry ?? throw new ArgumentNullException(nameof(optimisticRegistry));
            this.cloudCodeErrorHandler = cloudCodeErrorHandler ?? throw new ArgumentNullException(nameof(cloudCodeErrorHandler));
        }

        private readonly JsonSerializerSettings jsonSettings;
        private readonly CloudCodeSettings settings;
        private readonly ICloudCodeCallHandler callHandler;
        private readonly CloudCodeOptimisticHandlerRegistry optimisticRegistry;
        private readonly CloudCodeErrorHandler cloudCodeErrorHandler;

        private ICloudCodeCallHandler BuildCallHandlerChain(CloudCodeSdkCallHandler sdkCallHandler)
        {
            ICloudCodeCallHandler inner = sdkCallHandler;
            inner = new CloudCodeTimeoutCallHandler(settings, inner);
            inner = new CloudCodeResponseBodyLoggingCallHandler(settings, inner);
            inner = new CloudCodeRetryCallHandler(settings, inner);
            return new CloudCodeSingleFlightCallHandler(inner);
        }

        public async Task<T> CallEndpointAsync<T>(string module, string endpoint, object payload = null, CancellationToken cancellationToken = default)
        {
            ValidateModuleEndpoint(module, endpoint);
            cancellationToken.ThrowIfCancellationRequested();
            Task<string> serverTask = callHandler.InvokeAsync(module, endpoint, WrapPayload(payload), cancellationToken);
            if (TryGetOptimisticResponse<T>(module, endpoint, payload, out IRequestHandler<T> handler, out T optimisticResponse))
            {
                RunReconciliationInTheBackground<T>(serverTask, handler, optimisticResponse, module, endpoint, payload);
                return optimisticResponse;
            }

            return await AwaitServerWithCloudCodeErrorHandling<T>(serverTask, module, endpoint, payload);
        }

        private async Task<T> AwaitServerWithCloudCodeErrorHandling<T>(Task<string> serverTask, string module, string endpoint, object payload)
        {
            try
            {
                return await CallAsync<T>(serverTask);
            }
            catch (Exception ex)
            {
                cloudCodeErrorHandler.Handle(ex, module, endpoint, payload, null);
                throw;
            }
        }

        private Dictionary<string, object> WrapPayload(object payload)
        {
            return payload == null ? new() : new() { { "request", payload } };
        }

        private bool TryGetOptimisticResponse<TResponse>(string module, string endpoint, object payload, out IRequestHandler<TResponse> handler, out TResponse optimisticResponse)
        {
            return optimisticRegistry.TryResolve(module, endpoint, payload, out handler, out optimisticResponse);
        }

        private async void RunReconciliationInTheBackground<T>(Task<string> serverTask, IRequestHandler<T> handler, T optimisticResponse, string module, string endpoint, object requestPayload)
        {
            try
            {
                T response = await CallAsync<T>(serverTask);
                handler.Validate(response, optimisticResponse);
            }
            catch (Exception ex)
            {
                cloudCodeErrorHandler.Handle(ex, module, endpoint, requestPayload, optimisticResponse);
            }
        }

        private async Task<T> CallAsync<T>(Task<string> serverTask)
        {
            string response = await serverTask;
            return DeserializeResponse<T>(response);
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
