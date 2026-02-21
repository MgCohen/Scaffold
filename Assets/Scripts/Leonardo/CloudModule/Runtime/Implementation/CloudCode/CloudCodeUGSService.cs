using System;
using System.Collections.Generic;
using GameModuleDTO.ModuleRequests;
using Scaffold.Logging;
using Unity.Services.CloudCode;
using UnityEngine;

namespace Scaffold.CloudModules.Shared
{
    public class CloudCodeUGSService : ICloudCodeService
    {
        public CloudCodeUGSService()
        {
            CloudService = CloudCodeService.Instance;
        }
        
        public Action RequestError { get; }

        private Unity.Services.CloudCode.ICloudCodeService CloudService
        {
            get;
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
            string response;
            try
            {
                // First Attempt
                response = await CloudService.CallModuleEndpointAsync(module, endpoint, payload ?? new Dictionary<string, object>());
            }
            catch (Exception exception)
            {
#if SERVER
                return HandleError(exception);
#endif
                return await HandleErrorWithRetry(exception);
            }
            GameDebug.Log(response, debugName);
            return response.FromJson<T>();

            async Awaitable<T> HandleErrorWithRetry(Exception exception)
            {
                // Enter retry loop if the error is actually retryable (Network, 5xx, RateLimit)
                if (IsRetryableError(exception))
                {
                    for (int i = 0; i < maxRetries; i++)
                    {
                        await Awaitable.WaitForSecondsAsync(retryCall); // Ideally use a backoff strategy, 2s is fine for now
                        try
                        {
                            response = await CloudService.CallModuleEndpointAsync(module, endpoint, payload ?? new Dictionary<string, object>());
                            // Success on retry
                            GameDebug.Log(response, debugName);
                            return response.FromJson<T>();
                        }
                        catch (Exception retryEx)
                        {
                            // If the RETRY creates a Permanent Error (e.g., now we got a 400), STOP retrying.
                            if (!IsRetryableError(retryEx))
                            {
                                GameDebug.LogError($"Aborting Retries on Non-Retryable Error: {retryEx.Message}", debugName);
                                // Break the loop and fall through to the final error handler
                                exception = retryEx; // Update 'e' to the newest error for the popup
                                break;
                            }

                            GameDebug.LogError($"Retry {i + 1}/{maxRetries} failed: {retryEx.Message}", debugName);
                        }
                    }
                }
                else
                {
                    GameDebug.LogError($"Logic error detected, skipping retries. {exception.Message}", debugName);
                }

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
            
            TResponse response = await CallEndpointAsync<TResponse>(request.ModuleName, request.FunctionName, request.MaxRetries, request.RetryCall,new Dictionary<string, object>() {{"request", request}});
            return response;
        }
    }
}