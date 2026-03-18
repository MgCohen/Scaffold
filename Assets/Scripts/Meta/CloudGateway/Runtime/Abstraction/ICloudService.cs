using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GameModuleDTO.ModuleRequests;
using Scaffold.Utility.AwaitableQueue;

namespace Scaffold.CloudGateway
{
    /// <summary>
    /// Represents the core contract for interacting with remote Cloud Code execution.
    /// The main goal is to provide a unified API capable of submitting requests, managing retries, and broadcasting responses.
    /// It is used by game modules and services whenever they need to communicate with backend logic dynamically.
    /// </summary>
    public interface ICloudService
    {
        public CompositeTaskQueueEvent<ModuleResponse> TaskQueueHandler { get; }

        public Action RequestError { get; }

        public void SubscribeToResponse<TResponse>(Func<TResponse, Task> callback, bool immediate = false) where TResponse : ModuleResponse;

        public void UnsubscribeFromResponse<TResponse>(Func<TResponse, Task> callback, bool immediate = false) where TResponse : ModuleResponse;

        public Task<T> CallEndpointAsync<T>(string module, string endpoint, int maxRetry, int retryCall, Dictionary<string, object> payload = null);

        public Task<TResponse> CallEndpointAsync<TResponse>(ModuleRequest<TResponse> request)
            where TResponse : ModuleResponse;
    }
}