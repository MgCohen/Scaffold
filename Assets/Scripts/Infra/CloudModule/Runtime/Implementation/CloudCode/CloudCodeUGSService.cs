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
        /// <summary>
        /// The async task queue utilized for callback event throttling.
        /// The main goal is to dispatch events safely on the main thread via cues.
        /// It is used behind the scenes by response event triggers.
        /// </summary>
        private readonly TaskQueueHandler _taskQueueHandler;

        /// <summary>
        /// Initializes a new instance of the UGS Cloud Code Service wrapper.
        /// The main goal is to hook up the underlying third-party plugin and event queue.
        /// It is used by VContainer during dependency injection.
        /// </summary>
        public CloudCodeUGSService(TaskQueueHandler taskQueueHandler)
        {
            _CloudService = Unity.Services.CloudCode.CloudCodeService.Instance;
            _taskQueueHandler = taskQueueHandler;
            OnResponseReceived = new CompositeTaskQueueEvent<ModuleResponse>(taskQueueHandler);
        }

        /// <summary>
        /// The privately accessed remote UGS Cloud Service singleton.
        /// The main goal is to wrap and isolate SDK logic from external files.
        /// It is used strictly for internal RPC dispatches.
        /// </summary>
        private Unity.Services.CloudCode.ICloudCodeService _CloudService { get; }

        /// <summary>
        /// The strongly typed composite event queue triggered upon successful module response.
        /// The main goal is to broadcast serialized answers locally.
        /// It is used universally to hook into network state boundaries.
        /// </summary>
        public CompositeTaskQueueEvent<ModuleResponse> OnResponseReceived { get; }

        /// <summary>
        /// The action callback triggered whenever an unrecoverable request error occurs.
        /// The main goal is to report or catch network/API failure cleanly.
        /// It is used by logging tools or UI elements handling connectivity dips.
        /// </summary>
        public Action RequestError { get; }

        /// <summary>
        /// Subscribes to backend responses globally filtered by the exact <typeparamref name="TResponse"/> type.
        /// The main goal is to provide type-safe listener hooking.
        /// It is used by module layers to hook into their localized RPC replies.
        /// </summary>
        public void SubscribeToResponse<TResponse>(Func<TResponse, Awaitable> callback, bool immediate = false) where TResponse : ModuleResponse
        {
            OnResponseReceived.Subscribe(callback, immediate);
        }

        /// <summary>
        /// Unsubscribes a previously assigned response handler.
        /// The main goal is to disconnect unneeded callbacks.
        /// It is used when a presenter or system is destroyed to avoid orphaned invocation bounds.
        /// </summary>
        public void UnsubscribeFromResponse<TResponse>(Func<TResponse, Awaitable> callback, bool immediate = false) where TResponse : ModuleResponse
        {
            OnResponseReceived.Unsubscribe(callback, immediate);
        }

        /// <summary>
        /// Determines if a caught exception is eligible for retry mechanics.
        /// The main goal is to prevent retrying permanent bad-requests (4xx) while catching timeouts/throttles.
        /// It is used intimately by the retry extension block on endpoint calls.
        /// </summary>
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

        /// <summary>
        /// Serializes and sends a dictionary payload to a dynamic endpoint, awaiting the JSON.
        /// The main goal is to handle the low-level serialization and HTTP retry rules.
        /// It is used to run bare-metal unstructured endpoint hits.
        /// </summary>
        public async Awaitable<T> CallEndpointAsync<T>(string module, string endpoint, int maxRetries = 2, int retryCall = 2, Dictionary<string, object> payload = null)
        {
            string debugName = $"{module}.{endpoint}";

            try
            {
                Dictionary<string, object> finalPayload = payload ?? new Dictionary<string, object>();
                RetryTaskBuilder<string> retryHandler = new Func<Task<string>>(() => _CloudService.CallModuleEndpointAsync(module, endpoint, finalPayload))
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

        /// <summary>
        /// Submits a typed module request automatically applying type extraction and payload bundling.
        /// The main goal is to streamline strictly typed module queries reducing boilerplate syntax.
        /// It is used systematically by module-specific game services relying on custom API routes.
        /// </summary>
        public async Awaitable<TResponse> CallEndpointAsync<TResponse>(ModuleRequestT<TResponse> request)
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

        /// <summary>
        /// Delays the broadcasting of a received response by one frame tick.
        /// The main goal is to decouple the deep call stack of the SDK completion thread.
        /// It is used continuously after successful calls to safely emit to Unity main thread.
        /// </summary>
        private async Awaitable RaiseResponseEventDelayed(ModuleResponse response)
        {
            await Awaitable.NextFrameAsync();
            await OnResponseReceived.InvokeAsync(response);
        }
    }
}