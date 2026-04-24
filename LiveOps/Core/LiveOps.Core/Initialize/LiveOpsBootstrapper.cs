using System;
using LiveOps.GameApi;
using LiveOps.GameModule;
using Microsoft.Extensions.DependencyInjection;
using Unity.Services.CloudCode.Core;

namespace LiveOps.Initialize
{
    /// <summary>
    /// Registers <see cref="IGameApiHandler"/> and <see cref="IGameModule" /> from a compile-time
    /// manifest (see <c>Scaffold.LiveOps.Bootstrap.Generators</c> in the deploy project). No runtime assembly scanning.
    /// </summary>
    public static class LiveOpsBootstrapper
    {
        /// <summary>
        /// For each manifest entry: <c>AddScoped(Concrete)</c> once, then
        /// <see cref="GameApiRegistry.RegisterHandlerType" /> for handlers, and
        /// <c>AddScoped</c> for <see cref="IGameModule" /> to resolve to the same concrete instance.
        /// </summary>
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
                registry.RegisterHandlerType(t);
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
