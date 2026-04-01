using GameModule.GameModule;
using GameModule.Response;
using GameModule.ModuleFetchData;
using GameModule.ModuleFetchData.Unity;
using Microsoft.Extensions.DependencyInjection;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;
using GameModule.Signal;
using GameModule.Modules.Ads;
using GameModule.Modules.Gold;
using GameModule.Modules.Level;
using GameModule.ModuleFetchData.Http;
using GameModule.Modules.Global;

/// <summary>
/// Configures the dependency injection container for cloud code execution.
/// </summary>
public partial class ModuleConfig : ICloudCodeSetup
{
    /// <summary>
    /// Registers all scoped routines and dependencies dynamically.
    /// </summary>
    /// <param name="config">The injection container mapping properties securely.</param>
    public void Setup(ICloudCodeConfig config)
    {
        IGameApiClient gameApiClient = GameApiClient.Create();
        config.Dependencies.AddSingleton(gameApiClient);
        PushClient pushClient = PushClient.Create();
        config.Dependencies.AddSingleton(pushClient);

        RegisterScoped<IPlayerData, UnityPlayerData>(config);
        RegisterScoped<IGameState, UnityGameState>(config);
        RegisterScoped<IRemoteConfig, HttpRemoteConfig>(config);

        RegisterScoped<SignalModule>(config);
        RegisterScoped<ModuleRequestHandler>(config);

        RegisterModuleScoped<AdsService>(config);
        RegisterModuleScoped<GoldModule>(config);
        RegisterModuleScoped<LevelService>(config);
        RegisterModuleScoped<GlobalConfigModule>(config);
    }

    private void RegisterScoped<T>(ICloudCodeConfig config) where T : class
    {
        config.Dependencies.AddScoped<T>();
    }

    private void RegisterScoped<TInterface, TImplementation>(ICloudCodeConfig config)
        where TInterface : class
        where TImplementation : class, TInterface
    {
        config.Dependencies.AddScoped<TInterface, TImplementation>();
    }

    private void RegisterModuleScoped<T>(ICloudCodeConfig config) where T : class, IGameModule
    {
        config.Dependencies.AddScoped<IGameModule, T>();
        RegisterScoped<T>(config);
    }
}