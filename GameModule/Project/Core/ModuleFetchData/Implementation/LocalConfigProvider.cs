using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GameModule.ModuleFetchData
{
    /// <summary>
    /// Helper class to provide local JSON configuration fallback logic.
    /// Used via composition in RemoteConfig implementations.
    /// </summary>
    public class LocalConfigProvider
    {
        private readonly ILogger _logger;
        private readonly string[] _searchPaths = { "Configs", "Project/Configs", "Project/Project/Configs" };

        public LocalConfigProvider(ILogger logger)
        {
            _logger = logger;
        }

        public Dictionary<string, string> FetchLocalConfigs()
        {
            var localConfigs = new Dictionary<string, string>();

            foreach (var folder in _searchPaths)
            {
                if (Directory.Exists(folder))
                {
                    try
                    {
                        var files = Directory.GetFiles(folder, "*.json");
                        foreach (var file in files)
                        {
                            string content = File.ReadAllText(file);
                            try
                            {
                                JObject json = JObject.Parse(content);
                                foreach (var property in json.Properties())
                                {
                                    if (!localConfigs.ContainsKey(property.Name))
                                    {
                                        localConfigs[property.Name] = property.Value.ToString(Formatting.None);
                                    }
                                }
                            }
                            catch (Exception jsonEx)
                            {
                                _logger.LogWarning(jsonEx, "[LocalConfigProvider] Failed to parse local config file {File}", file);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "[LocalConfigProvider] Failed to read from local folder {Folder}", folder);
                    }
                }
            }

            return localConfigs;
        }

        public void Merge(Dictionary<string, string> target, Dictionary<string, string> source)
        {
            if (source == null)
            {
                return;
            }
            foreach (var kvp in source)
            {
                if (!target.ContainsKey(kvp.Key))
                {
                    _logger.LogInformation("[LocalConfigFallback] Key {Key} missing in remote. Using local fallback.", kvp.Key);
                    target[kvp.Key] = kvp.Value;
                }
            }
        }
    }
}
