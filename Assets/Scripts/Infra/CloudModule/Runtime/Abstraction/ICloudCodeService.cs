using System;
using System.Collections.Generic;
using GameModuleDTO.ModuleRequests;
using Scaffold.AwaitableQueue;
using UnityEngine;

namespace Scaffold.CloudModules
{
    /// <summary>
    /// Represents the core contract for interacting with remote Cloud Code execution.
    /// The main goal is to provide a unified API capable of submitting requests, managing retries, and broadcasting responses.
    /// It is used by game modules and services whenever they need to communicate with backend logic dynamically.
    /// </summary>
    public interface ICloudCodeService
    {
        public CompositeTaskQueueEvent<ModuleResponse> OnResponseReceived { get; }

        public Action RequestError { get; }

        public void SubscribeToResponse<TResponse>(Func<TResponse, Awaitable> callback, bool immediate = false) where TResponse : ModuleResponse;

        public void UnsubscribeFromResponse<TResponse>(Func<TResponse, Awaitable> callback, bool immediate = false) where TResponse : ModuleResponse;

        public Awaitable<T> CallEndpointAsync<T>(string module, string endpoint, int maxRetry, int retryCall, Dictionary<string, object> payload = null);

        public Awaitable<TResponse> CallEndpointAsync<TResponse>(ModuleRequestT<TResponse> request)
            where TResponse : ModuleResponse;
    }
}