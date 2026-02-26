using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GameModuleDTO.ModuleRequests;
using Scaffold.Logging;
using Scaffold.AwaitableQueue;
using Scaffold.RetryAwaitable.Shared;
using Unity.Services.CloudCode;
using UnityEngine;

namespace Scaffold.CloudModules.Shared
{
    public class CloudCodeUGSService : ICloudCodeService
    {
        public CloudCodeUGSService(TaskQueueHandler taskQueueHandler)
        {
            CloudService = CloudCodeService.Instance;
            _taskQueueHandler = taskQueueHandler;
            OnResponseReceived = new CompositeTaskQueueEvent<ModuleResponse>(taskQueueHandler);
        }

        private Unity.Services.CloudCode.ICloudCodeService CloudService { get; }
        private readonly TaskQueueHandler _taskQueueHandler;
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
            // Rate Limits are always retryable (Wait and Retry)
            // This specific exception is thrown when the server rejects a request due to too many calls (HTTP 429)
            if (ex is CloudCodeRateLimitedException)
            {
                return true;
            }

            // Cloud Code specific exceptions
            if (ex is CloudCodeException ccEx)
            {
                // 400-499: Logic and Client errors (e.g., Not Enough Gold, Invalid Input) -> DO NOT RETRY
                if (ccEx.ErrorCode >= 400 && ccEx.ErrorCode < 500)
                {
                    return false;
                }
            }

            // Generic Network/System errors (Timeout, Connection Failed) -> RETRY
            return true;
        }

        public async Awaitable<T> CallEndpointAsync<T>(string module, string endpoint, int maxRetries = 2, int retryCall = 2, Dictionary<string, object> payload = null)
        {
            string debugName = $"{module}.{endpoint}";

            try
            {
                string response = await new Func<Task<string>>(() => CloudService.CallModuleEndpointAsync(module, endpoint, payload ?? new Dictionary<string, object>()))
                    .Retry(maxRetries)
                    .WithDelay(retryCall)
                    .WithCondition(IsRetryableError)
                    .OnRetry((ex, attempt) => GameDebug.LogError($"Retry {attempt}/{maxRetries} failed: {ex.Message}", debugName))
                    .ExecuteAsAwaitableAsync();

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

            TResponse response = await CallEndpointAsync<TResponse>(request.ModuleName, request.FunctionName, request.MaxRetries, request.RetryCall, new Dictionary<string, object>() { { "request", request } });

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