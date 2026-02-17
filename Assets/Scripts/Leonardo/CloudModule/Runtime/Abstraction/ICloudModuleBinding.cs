using System;
using System.Collections.Generic;
using GameModuleDTO.GameModule;
using UnityEngine;

namespace Scaffold.CloudModules.Shared
{
    public interface ICloudModuleBinding
    {
        public string ModuleName { get; }
        public List<IGameModule> Modules { get; }
        public string GetEndpointName(string endpointName);
        public Action RequestError { get; }
        public Awaitable<GameData> InitializeModules();
        public Awaitable<T> CallEndpointAsync<T>(string module, string endpoint, Dictionary<string, object> payload = null);
        public Awaitable CallEndpointAsync(string module, string endpoint, Dictionary<string, object> payload = null);
    }
}