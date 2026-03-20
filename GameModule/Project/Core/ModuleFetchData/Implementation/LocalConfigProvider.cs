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

        public LocalConfigProvider(ILogger logger)
        {
            _logger = logger;
        }

        public Dictionary<string, string> FetchLocalConfigs()
        {
            var localConfigs = new Dictionary<string, string>();

            // Only search in folders named 'Configs' to avoid picking up system/build files
            string[] searchPaths = {
                "Configs",
                "../Configs",
                "../../Configs"
            };

            foreach (var folder in searchPaths)
            {
                if (Directory.Exists(folder))
                {
                    try
                    {
                        var files = Directory.GetFiles(folder, "*.json");
                        if (files.Length > 0)
                        {
                            _logger.LogInformation("[LocalConfigProvider] Found {Count} config files in: {Path}", files.Length, Path.GetFullPath(folder));
                        }

                        foreach (var file in files)
                        {
                            string fileName = Path.GetFileName(file);
                            // Avoid common system/build JSON files that are often in /app or build folders
                            if (fileName.Contains(".deps.") || 
                                fileName.Contains(".assets.") || 
                                fileName.Contains(".packagespec.") || 
                                fileName.Contains(".runtimeconfig.") ||
                                fileName.StartsWith("project."))
                            {
                                continue;
                            }

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
                        _logger.LogWarning(ex, "[LocalConfigProvider] Error reading from folder {Folder}", folder);
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
