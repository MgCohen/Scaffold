using System;
using LiveOps.GameApi;
using LiveOps.GameModule;
using Microsoft.Extensions.DependencyInjection;
using Unity.Services.CloudCode.Core;

namespace LiveOps.Initialize
{

    public static class LiveOpsBootstrapper
    {

        public static void InstallFromManifest(ICloudCodeConfig config, GameApiRegistry registry, ReadOnlySpan<LiveOpsManifestEntry> entries)
        {
            if (config is null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            if (registry is null)
            {
                throw new ArgumentNullException(nameof(registry));
            }

            CheckEntries(config, registry, entries);
        }

        private static void CheckEntries(ICloudCodeConfig config, GameApiRegistry registry, ReadOnlySpan<LiveOpsManifestEntry> entries)
        {
            for (int i = 0; i < entries.Length; i++)
            {
                LiveOpsManifestEntry entry = entries[i];
                if (!entry.IsGameApiHandler && !entry.IsGameModule)
                {
                    continue;
                }

                System.Type t = entry.Type;
                if (t.IsAbstract || t.IsInterface)
                {
                    continue;
                }

                Register(config, registry, entry, t);
            }
        }

        private static void Register(ICloudCodeConfig config, GameApiRegistry registry, LiveOpsManifestEntry entry, Type t)
        {
            config.Dependencies.AddScoped(t);
            if (entry.IsGameApiHandler)
            {
                if (entry.RequestType != null && entry.ResponseType != null)
                {
                    registry.Register(t, entry.RequestType, entry.ResponseType);
                }
                else
                {
                    registry.RegisterHandlerType(t);
                }
            }

            if (entry.IsGameModule)
            {
                config.Dependencies.AddScoped(
                    typeof(IGameModule),
                    sp => (IGameModule)sp.GetRequiredService(t));
            }
        }
    }
}
