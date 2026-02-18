using System;
using System.Collections.Generic;
using GameModuleDTO.GameModule;
using Scaffold.Logging;
using Unity.Services.CloudCode;
using UnityEngine;
using VContainer;

namespace Scaffold.CloudModules.Shared
{
    public class GameModuleBindings : ICloudModuleBinding
    {
        public string ModuleName
        {
            get
            {
                return "GameModule";
            }
        }
        private const int RetryCall = 2; // In secconds
        private const int MaxRetries = 2;
        public Action RequestError { get; }
        
        [Inject]
        public List<IGameModule> Modules { get; }

        string ICloudModuleBinding.GetEndpointName(string endpointName)
        {
            return GetEndpointName(endpointName);
        }

        private ICloudCodeService CloudService
        {
            get { return CloudCodeService.Instance; }
        }

        protected string GetEndpointName(string endpointName)
        {
            return $"{ModuleName}.{endpointName}";
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

        public async Awaitable<T> CallEndpointAsync<T>(string module, string endpoint, Dictionary<string, object> payload = null)
        {
            string debugName = GetEndpointName(nameof(CallEndpointAsync));
            string response;
            try
            {
                // First Attempt
                response = await CloudService.CallModuleEndpointAsync(module, endpoint, payload ?? new Dictionary<string, object>());
            }
            catch (Exception e)
            {
#if !SERVER
                // Enter retry loop if the error is actually retryable (Network, 5xx, RateLimit)
                if (IsRetryableError(e))
                {
                    for (int i = 0; i < MaxRetries; i++)
                    {
                        await Awaitable.WaitForSecondsAsync(RetryCall); // Ideally use a backoff strategy, but 2s is fine for now
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
                                e = retryEx; // Update 'e' to the newest error for the popup
                                break;
                            }

                            GameDebug.LogError($"Retry {i + 1}/{MaxRetries} failed: {retryEx.Message}", debugName);
                        }
                    }
                }
                else
                {
                    GameDebug.LogError($"Logic error detected, skipping retries. {e.Message}", debugName);
                }

                RequestError?.Invoke();
#endif
                GameDebug.LogException(e, debugName);
                return default;
            }
            GameDebug.Log(response, debugName);
            return response.FromJson<T>();
        }

        public async Awaitable CallEndpointAsync(string module, string endpoint, Dictionary<string, object> payload = null)
        {
            // Call the Cloud Code endpoint
            await CloudService.CallModuleEndpointAsync(module, endpoint, payload ?? new Dictionary<string, object>());
            GameDebug.Log("GameModuleBindings.CallEndpointAsync", $"{module}.{endpoint}");
        }

        public async Awaitable<GameData> InitializeModules()
        {
            return await CallEndpointAsync<GameData>(
                ModuleName,
                "InitializeModules",
                new Dictionary<string, object>
                {
                    { "serverKey", GameModuleAuthKey.guid }
                });
        }
    }
}