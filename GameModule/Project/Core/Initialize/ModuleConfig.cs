using GameModule.Authentication;
using GameModule.GameModule;
using GameModule.Initialize;
using GameModule.ModuleFetchData;
using GameModule.Response;
using Microsoft.Extensions.DependencyInjection;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;

public partial class ModuleConfig : ICloudCodeSetup
{
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