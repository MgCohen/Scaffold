using System;
using System.Collections.Generic;
using GameModuleDTO.ModuleRequests;
using Scaffold.AwaitableQueue;
using UnityEngine;

namespace Scaffold.CloudModules.Shared
{
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