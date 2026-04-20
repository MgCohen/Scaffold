using System;
using System.Reflection;
using GameModule.GameApi;
using GameModule.GameModule;
using GameModule.Response;
using GameModule.ModuleFetchData;
using GameModule.ModuleFetchData.Unity;
using GameModuleDTO.ModuleRequests;
using Microsoft.Extensions.DependencyInjection;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;
using GameModule.Signal;
using GameModule.Modules.Ads;
using GameModule.Modules.Gold;
using GameModule.Modules.Level;
using GameModule.ModuleFetchData.Http;
using GameModule.Modules.Global;
using GameModule.Modules.DirectPush;

/// <summary>
/// Configures the dependency injection container for cloud code execution.
/// </summary>
public class ModuleConfig : ICloudCodeSetup
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

        Assembly gameApiAssembly = typeof(GameApiDispatcher).Assembly;
        GameApiRegistry gameApiRegistry = new GameApiRegistry(gameApiAssembly);
        config.Dependencies.AddSingleton(gameApiRegistry);
        RegisterScoped<GameApiDispatcher>(config);

        foreach (Type type in gameApiAssembly.GetTypes())
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

        RegisterScoped<ModuleRequestHandler>(config);

        RegisterModuleScoped<AdsService>(config);
        RegisterModuleScoped<GoldModule>(config);
        RegisterModuleScoped<LevelService>(config);
        RegisterModuleScoped<GlobalConfigModule>(config);
        RegisterScoped<DirectPushService>(config);
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

    /// <summary>
    /// Optional explicit GameApi handler registration (e.g. handlers outside the scanned assembly or tests).
    /// </summary>
    public static void RegisterGameApiHandler<TReq, TRes, THandler>(ICloudCodeConfig config, GameApiRegistry registry)
        where TReq : ModuleRequest<TRes>
        where TRes : ModuleResponse
        where THandler : class, IGameApiHandler<TReq, TRes>
    {
        config.Dependencies.AddScoped<THandler>();
        registry.Register(typeof(THandler));
    }
}