using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GameModuleDTO.ModuleRequests;
using Scaffold.AwaitableRetry;
using Scaffold.Utility.AwaitableQueue;
using Scaffold.Logging;
using Unity.Services.CloudCode;

namespace Scaffold.CloudGateway
{
    /// <summary>
    /// An implementation of the Cloud Code service bridging the Unity Gaming Services (UGS) backend.
    /// The main goal is to securely route endpoint requests across the network, applying intelligent retries upon failures.
    /// It is used by client modules to dispatch payloads and wait for remote computations and responses.
    /// </summary>
    public class CloudUgsService : ICloudService
    {
        public CloudUgsService(ITaskQueueHandler taskQueueHandler)
        {
            OnResponseReceived = new CompositeTaskQueueEvent<ModuleResponse>(taskQueueHandler);
        }

        private ICloudCodeService CloudCodeUGSService
        {
            get
            {
                return CloudCodeService.Instance;
            }
        }

        public CompositeTaskQueueEvent<ModuleResponse> OnResponseReceived { get; }

        public Action RequestError { get; }

        public void SubscribeToResponse<TResponse>(Func<TResponse, Task> callback, bool immediate = false) where TResponse : ModuleResponse
        {
            OnResponseReceived.Subscribe(callback, immediate);
        }

        public void UnsubscribeFromResponse<TResponse>(Func<TResponse, Task> callback, bool immediate = false) where TResponse : ModuleResponse
        {
            OnResponseReceived.Unsubscribe(callback, immediate);
        }

        private static bool IsRetryableError(Exception ex)
        {
            if (ex is CloudCodeRateLimitedException)
            {
                return true;
            }

            if (ex is CloudCodeException ccEx)
            {
                if (ccEx.ErrorCode >= 400 && ccEx.ErrorCode < 500)
                {
                    return false;
                }
            }

            return true;
        }

        public async Task<T> CallEndpointAsync<T>(string module, string endpoint, int maxRetries = 2, int retryCall = 2, Dictionary<string, object> payload = null)
        {
            string debugName = $"{module}.{endpoint}";

            try
            {
                if (CloudCodeUGSService == null)
                {
                    throw new Exception("CloudCodeService is not initialized or not available.");
                }

                Dictionary<string, object> finalPayload = payload ?? new Dictionary<string, object>();
                GameDebug.Log($"Payload: {finalPayload.ToJson()}", debugName);
                RetryTaskBuilder<string> retryHandler = new Func<Task<string>>(() => CloudCodeUGSService.CallModuleEndpointAsync(module, endpoint, finalPayload))
                    .Retry(maxRetries)
                    .WithDelay(retryCall)
                    .WithCondition(IsRetryableError)
                    .OnRetry((ex, attempt) => GameDebug.LogError($"Retry {attempt}/{maxRetries} failed: {ex.Message}", debugName));

                string response = await retryHandler.ExecuteAsync();

                GameDebug.Log(response, debugName);
                return response.FromJson<T>();
            }
            catch (Exception exception)
            {
                return HandleError(exception);
            }

            T HandleError(Exception exception)
            {
                GameDebug.LogException(exception, debugName);
                RequestError?.Invoke();
                return default;
            }
        }

        public async Task<TResponse> CallEndpointAsync<TResponse>(ModuleRequestT<TResponse> request)
            where TResponse : ModuleResponse
        {
            if (request == null)
            {
                GameDebug.LogError($"{nameof(request)} is null");
                return null;
            }

            Dictionary<string, object> payload = new Dictionary<string, object>() { { "request", request } };
            TResponse response = await CallEndpointAsync<TResponse>(request.ModuleName, request.FunctionName, request.MaxRetries, request.RetryCall, payload);

            _ = RaiseResponseEventDelayed(response);

            return response;
        }

        private async Task RaiseResponseEventDelayed(ModuleResponse response)
        {
            await Task.Yield();
            await OnResponseReceived.InvokeAsync(response);
        }
    }
}