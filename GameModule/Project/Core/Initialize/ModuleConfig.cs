using GameModule.Authentication;
using GameModule.GameModule;
using GameModule.Initialize;
using GameModule.Response;
using GameModule.Sample;
using GameModule.ModuleFetchData;
using GameModule.ModuleFetchData.Unity;
using Microsoft.Extensions.DependencyInjection;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;
using GameModule.Signal;
using GameModule.Modules.Ads;
using GameModule.Modules.Gold;
using GameModule.Modules.Level;
using GameModule.Modules.Tutorial;
using GameModule.ModuleFetchData.Http;

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
        ModuleServices.GameApiClient = gameApiClient;

        config.Dependencies.AddSingleton(gameApiClient);
        PushClient pushClient = PushClient.Create();
        config.Dependencies.AddSingleton(pushClient);

        RegisterScoped<IPlayerData, UnityPlayerData>(config);
        RegisterScoped<IGameState, UnityGameState>(config);
        //RegisterScoped<IRemoteConfig, UnityRemoteConfig>(config);
        RegisterScoped<IRemoteConfig, HttpRemoteConfig>(config);

        RegisterScoped<AuthenticationModule>(config);
        RegisterScoped<UnityConfigFetcher>(config);

        RegisterScoped<SignalModule>(config);
        RegisterScoped<ModuleRequestHandler>(config);

        RegisterModuleScoped<SimpleModule>(config);
        RegisterModuleScoped<ReactiveModule>(config);
        RegisterModuleScoped<CounterModule>(config);

        RegisterModuleScoped<AdsModule>(config);
        RegisterModuleScoped<AdsConfigModule>(config);
        RegisterModuleScoped<GoldModule>(config);
        RegisterModuleScoped<GoldConfigModule>(config);
        RegisterModuleScoped<LevelModule>(config);
        RegisterModuleScoped<LevelConfigModule>(config);
        RegisterModuleScoped<TutorialModule>(config);
        RegisterModuleScoped<TutorialConfigModule>(config);
    }


    private void RegisterSingleton<T>(ICloudCodeConfig config) where T : class
    {
        config.Dependencies.AddSingleton<T>();
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