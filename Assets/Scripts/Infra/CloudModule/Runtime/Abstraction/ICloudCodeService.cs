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
        /// <summary>
        /// Gets the event that triggers when a response is received.
        /// The main goal is to notify listeners of incoming backend responses.
        /// It is used by request coordinators to route responses back to callers.
        /// </summary>
        public CompositeTaskQueueEvent<ModuleResponse> OnResponseReceived { get; }

        /// <summary>
        /// Gets the action triggered when a request fails.
        /// The main goal is to signal network or server side errors.
        /// It is used for broad error handling or state recovery mechanisms.
        /// </summary>
        public Action RequestError { get; }

        /// <summary>
        /// Subscribes a callback to a specific type of module response.
        /// The main goal is to register response handlers locally.
        /// It is used to listen for asynchronous results specific to a game feature.
        /// </summary>
        public void SubscribeToResponse<TResponse>(Func<TResponse, Awaitable> callback, bool immediate = false) where TResponse : ModuleResponse;

        /// <summary>
        /// Unsubscribes a previously registered callback.
        /// The main goal is to remove stale listeners.
        /// It is used during teardown phases of feature modules to avoid memory leaks.
        /// </summary>
        public void UnsubscribeFromResponse<TResponse>(Func<TResponse, Awaitable> callback, bool immediate = false) where TResponse : ModuleResponse;

        /// <summary>
        /// Calls a raw backend endpoint with a dynamic payload.
        /// The main goal is to send module requests and await the custom response.
        /// It is used as the foundational communication layer for remote interaction.
        /// </summary>
        public Awaitable<T> CallEndpointAsync<T>(string module, string endpoint, int maxRetry, int retryCall, Dictionary<string, object> payload = null);

        /// <summary>
        /// Calls a typed backend endpoint based on a predefined request structure.
        /// The main goal is to securely route endpoint requests and deserialize responses directly.
        /// It is used by strongly-typed module services to send well-defined RPCs.
        /// </summary>
        public Awaitable<TResponse> CallEndpointAsync<TResponse>(ModuleRequestT<TResponse> request)
            where TResponse : ModuleResponse;
    }
}