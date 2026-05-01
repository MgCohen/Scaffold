using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LiveOps.ModuleFetchData;
using Newtonsoft.Json;
using Unity.Services.CloudCode.Core;

namespace LiveOps.Tests
{
    internal sealed class MemoryRemoteConfig : IRemoteConfig
    {
        private readonly Dictionary<string, string> _raw = new(StringComparer.Ordinal);

        public IReadOnlyCollection<string>? LastWarmupKeys { get; private set; }

        public Task<bool> Exists(IExecutionContext context, string key) => Task.FromResult(_raw.ContainsKey(key));

        public Task<T> Get<T>(IExecutionContext context, string key, T defaultValue)
        {
            if (!_raw.TryGetValue(key, out string? json))
            {
                return Task.FromResult(defaultValue);
            }

            try
            {
                T? v = JsonConvert.DeserializeObject<T>(json);
                return Task.FromResult(v ?? defaultValue);
            }
            catch
            {
                return Task.FromResult(defaultValue);
            }
        }

        public Task WarmupAsync(IExecutionContext context, IReadOnlyCollection<string>? keys = null)
        {
            LastWarmupKeys = keys;
            return Task.CompletedTask;
        }
    }
}
