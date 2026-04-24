using System;
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
        config.Dependencies.AddSingleton(gameApiRegistry);
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
