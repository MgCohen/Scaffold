using System;
using System.Linq;
using System.Reflection;
using LiveOps.GameApi;
using LiveOps.Initialize;
using LiveOps.ModuleFetchData;
using LiveOps.ModuleFetchData.Unity;
using LiveOps.ServerAuth;
using LiveOps.Signal;
using Microsoft.Extensions.DependencyInjection;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;

public class ModuleConfig : ICloudCodeSetup
{

    public void Setup(ICloudCodeConfig config)
    {
        IGameApiClient gameApiClient = GameApiClient.Create();
        config.Dependencies.AddSingleton(gameApiClient);
        PushClient pushClient = PushClient.Create();
        config.Dependencies.AddSingleton(pushClient);

        RegisterScoped<IPlayerData, UnityPlayerData>(config);
        RegisterScoped<IGameState, UnityGameState>(config);
        RegisterScoped<IRemoteConfig, UnityRemoteConfig>(config);
        RegisterScoped<IServerAuth, GameStateServerAuth>(config);
        RegisterScoped<SignalModule>(config);
        RegisterScoped<GameApiDispatcher>(config);

        RegisterLiveOps(config);
    }

    private static void RegisterLiveOps(ICloudCodeConfig config)
    {
        GameApiRegistry gameApiRegistry = new GameApiRegistry();
        ReadOnlySpan<LiveOpsManifestEntry> manifest = LiveOpsManifest.Entries;
        if (manifest.Length == 0)
        {
            throw new InvalidOperationException(
                "LiveOpsManifest.Entries is empty. Build the LiveOps (Deploy) project so " +
                "Scaffold.LiveOps.Bootstrap.Generators can emit handler and module types from the referenced LiveOps.* assemblies.");
        }

        LiveOpsBootstrapper.InstallFromManifest(config, gameApiRegistry, manifest);
        RunGameSetups(config, gameApiRegistry);
        config.Dependencies.AddSingleton(gameApiRegistry);
    }

    private static void RunGameSetups(ICloudCodeConfig config, GameApiRegistry gameApiRegistry)
    {
        foreach (Type t in DiscoverGameSetupTypes())
        {
            var setup = (IGameSetup)Activator.CreateInstance(t)!;
            setup.Configure(config, gameApiRegistry);
        }
    }

    private static System.Collections.Generic.IEnumerable<Type> DiscoverGameSetupTypes()
    {
        return AppDomain.CurrentDomain.GetAssemblies()
            .Where(HasScaffoldLiveOpsAssemblyMetadata)
            .SelectMany(a =>
            {
                try
                {
                    return a.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    return ex.Types.Where(x => x != null).Cast<Type>();
                }
            })
            .Where(t => typeof(IGameSetup).IsAssignableFrom(t) && t is { IsClass: true, IsAbstract: false }
                && t.GetConstructor(Type.EmptyTypes) != null)
            .OrderBy(t => t.FullName, StringComparer.Ordinal);
    }

    private static bool HasScaffoldLiveOpsAssemblyMetadata(Assembly assembly)
    {
        foreach (CustomAttributeData attr in assembly.CustomAttributes)
        {
            if (attr.AttributeType.Name != "AssemblyMetadataAttribute" || attr.ConstructorArguments.Count < 2)
            {
                continue;
            }

            if (attr.ConstructorArguments[0].Value is string k
                && string.Equals(k, "ScaffoldLiveOpsAssembly", StringComparison.Ordinal)
                && attr.ConstructorArguments[1].Value is string v
                && string.Equals(v, "true", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
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
}
