using GameModule.Authentication;
using GameModule.GameModule;
using GameModule.Initialize;
using GameModule.ModuleFetchData;
using GameModule.Response;
using Microsoft.Extensions.DependencyInjection;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;
using GameModule.Signal;

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

        RegisterScoped<PlayerData>(config);
        RegisterScoped<GameState>(config);
        RegisterScoped<RemoteConfig>(config);

        RegisterScoped<AuthenticationModule>(config);
        RegisterScoped<ConfigFetcher>(config);

        RegisterScoped<SignalModule>(config);
        RegisterScoped<ModuleRequestHandler>(config);
    }

    private void RegisterSingleton<T>(ICloudCodeConfig config) where T : class
    {
        config.Dependencies.AddSingleton<T>();
    }

    private void RegisterScoped<T>(ICloudCodeConfig config) where T : class
    {
        config.Dependencies.AddScoped<T>();
    }

    private void RegisterModuleScoped<T>(ICloudCodeConfig config) where T : class, IGameModule
    {
        config.Dependencies.AddScoped<IGameModule, T>();
        RegisterScoped<T>(config);
    }
}
