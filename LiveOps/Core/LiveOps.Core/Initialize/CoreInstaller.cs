using System;
using System.Linq;
using System.Reflection;
using LiveOps.Core.GameApi;
using LiveOps.Core.ModuleFetchData;
using LiveOps.Core.ModuleFetchData.Http;
using LiveOps.Core.ModuleFetchData.Unity;
using LiveOps.Core.Response;
using LiveOps.Core.Signal;
using Microsoft.Extensions.DependencyInjection;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;

namespace LiveOps.Core.Initialize
{
    /// <summary>
    /// Registers core caches, GameApi, and handlers discovered in LiveOps.Core / LiveOps.Modules assemblies.
    /// </summary>
    public sealed class CoreInstaller : ICloudCodeInstaller
    {
        public void Install(ICloudCodeConfig config)
        {
            TryLoadAssembly("LiveOps.Modules");

            IGameApiClient gameApiClient = GameApiClient.Create();
            config.Dependencies.AddSingleton(gameApiClient);
            PushClient pushClient = PushClient.Create();
            config.Dependencies.AddSingleton(pushClient);

            RegisterScoped<IPlayerData, UnityPlayerData>(config);
            RegisterScoped<IGameState, UnityGameState>(config);
            RegisterScoped<IRemoteConfig, HttpRemoteConfig>(config);

            RegisterScoped<SignalModule>(config);

            Assembly[] liveOpsAssemblies = GetLiveOpsCoreAndModulesAssemblies();
            GameApiRegistry gameApiRegistry = new GameApiRegistry(liveOpsAssemblies);
            config.Dependencies.AddSingleton(gameApiRegistry);
            RegisterScoped<GameApiDispatcher>(config);

            foreach (Assembly assembly in liveOpsAssemblies)
            {
                foreach (Type type in SafeGetTypes(assembly))
                {
                    if (type.IsAbstract || type.IsInterface)
                    {
                        continue;
                    }

                    if (!typeof(IGameApiHandler).IsAssignableFrom(type))
                    {
                        continue;
                    }

                    config.Dependencies.AddScoped(type);
                }
            }

            RegisterScoped<ModuleRequestHandler>(config);
        }

        private static Assembly[] GetLiveOpsCoreAndModulesAssemblies()
        {
            Assembly core = typeof(GameApiDispatcher).Assembly;

            Assembly? modules = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(static a => a.GetName().Name == "LiveOps.Modules");

            if (modules == null)
            {
                try
                {
                    modules = Assembly.Load(new AssemblyName("LiveOps.Modules"));
                }
                catch
                {
                    // Modules assembly optional for minimal deployments
                }
            }

            return modules != null
                ? new[] { core, modules }
                : new[] { core };
        }

        private static void TryLoadAssembly(string simpleName)
        {
            if (AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == simpleName))
            {
                return;
            }

            try
            {
                Assembly.Load(new AssemblyName(simpleName));
            }
            catch
            {
            }
        }

        private static void RegisterScoped<T>(ICloudCodeConfig config) where T : class
        {
            config.Dependencies.AddScoped<T>();
        }

        private static void RegisterScoped<TInterface, TImplementation>(ICloudCodeConfig config)
            where TInterface : class
            where TImplementation : class, TInterface
        {
            config.Dependencies.AddScoped<TInterface, TImplementation>();
        }

        private static Type[] SafeGetTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                return ex.Types.Where(t => t != null).Cast<Type>().ToArray();
            }
        }
    }
}
