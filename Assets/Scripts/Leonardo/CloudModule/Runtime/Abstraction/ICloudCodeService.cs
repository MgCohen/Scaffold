using System;
using System.Collections.Generic;
using GameModuleDTO.ModuleRequests;
using UnityEngine;

namespace Scaffold.CloudModules.Shared
{
    public interface ICloudCodeService
    {
        public Action RequestError { get; }
        public Awaitable<T> CallEndpointAsync<T>(string module, string endpoint, int maxRetry, int retryCall, Dictionary<string, object> payload = null);
        public Awaitable<TResponse> CallEndpointAsync<TResponse>(ModuleRequestT<TResponse> request)
            where TResponse : ModuleResponse;
    }
}