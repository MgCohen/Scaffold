using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GameModuleDTO.ModuleRequests;
using Scaffold.Logging;
using Scaffold.AwaitableQueue;
using Scaffold.AwaitableRetry;
using Unity.Services.CloudCode;
using UnityEngine;

namespace Scaffold.CloudModules
{
    /// <summary>
    /// An implementation of the Cloud Code service bridging the Unity Gaming Services (UGS) backend.
    /// The main goal is to securely route endpoint requests across the network, applying intelligent retries upon failures.
    /// It is used by client modules to dispatch payloads and wait for remote computations and responses.
    /// </summary>
    public class CloudCodeUGSService : ICloudCodeService
    {
        private readonly TaskQueueHandler _taskQueueHandler;

        public CloudCodeUGSService(TaskQueueHandler taskQueueHandler)
        {
            _CloudService = Unity.Services.CloudCode.CloudCodeService.Instance;
            _taskQueueHandler = taskQueueHandler;
            OnResponseReceived = new CompositeTaskQueueEvent<ModuleResponse>(taskQueueHandler);
        }

        private Unity.Services.CloudCode.ICloudCodeService _CloudService { get; }

        public CompositeTaskQueueEvent<ModuleResponse> OnResponseReceived { get; }

        public Action RequestError { get; }

        public void SubscribeToResponse<TResponse>(Func<TResponse, Awaitable> callback, bool immediate = false) where TResponse : ModuleResponse
        {
            OnResponseReceived.Subscribe(callback, immediate);
        }

        public void UnsubscribeFromResponse<TResponse>(Func<TResponse, Awaitable> callback, bool immediate = false) where TResponse : ModuleResponse
        {
            OnResponseReceived.Unsubscribe(callback, immediate);
        }

        private bool IsRetryableError(Exception ex)
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

        public async Awaitable<T> CallEndpointAsync<T>(string module, string endpoint, int maxRetries = 2, int retryCall = 2, Dictionary<string, object> payload = null)
        {
            string debugName = $"{module}.{endpoint}";

            try
            {
                var finalPayload = payload ?? new Dictionary<string, object>();
                var retryHandler = new Func<Task<string>>(() => _CloudService.CallModuleEndpointAsync(module, endpoint, finalPayload))
                    .Retry(maxRetries)
                    .WithDelay(retryCall)
                    .WithCondition(IsRetryableError)
                    .OnRetry((ex, attempt) => GameDebug.LogError($"Retry {attempt}/{maxRetries} failed: {ex.Message}", debugName));

                string response = await retryHandler.ExecuteAsAwaitableAsync();

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

        public async Awaitable<TResponse> CallEndpointAsync<TResponse>(ModuleRequestT<TResponse> request)
            where TResponse : ModuleResponse
        {
            if (request == null)
            {
                GameDebug.LogError($"{nameof(request)} is null");
                return null;
            }

            var payload = new Dictionary<string, object>() { { "request", request } };
            TResponse response = await CallEndpointAsync<TResponse>(request.ModuleName, request.FunctionName, request.MaxRetries, request.RetryCall, payload);

            _ = RaiseResponseEventDelayed(response);

            return response;
        }

        private async Awaitable RaiseResponseEventDelayed(ModuleResponse response)
        {
            await Awaitable.NextFrameAsync();
            await OnResponseReceived.InvokeAsync(response);
        }
    }
}