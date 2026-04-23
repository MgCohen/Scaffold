using LiveOps.Core.GameApi;
using LiveOps.Core.GameModule;
using Microsoft.Extensions.DependencyInjection;
using Unity.Services.CloudCode.Core;

namespace LiveOps.Core.Initialize
{
    /// <summary>
    /// Base helpers for module installers (scoped services and <see cref="IGameModule"/> registration).
    /// </summary>
    public abstract class GameModuleInstaller : ICloudCodeInstaller
    {
        public abstract void Install(ICloudCodeConfig config);

        protected static void RegisterScoped<T>(ICloudCodeConfig config) where T : class
            => config.Dependencies.AddScoped<T>();

        protected static void RegisterScoped<TInterface, TImpl>(ICloudCodeConfig config)
            where TInterface : class
            where TImpl : class, TInterface
            => config.Dependencies.AddScoped<TInterface, TImpl>();

        protected static void RegisterModule<TModule>(ICloudCodeConfig config)
            where TModule : class, IGameModule
        {
            config.Dependencies.AddScoped<IGameModule, TModule>();
            config.Dependencies.AddScoped<TModule>();
        }

        protected static void RegisterHandler<THandler>(ICloudCodeConfig config)
            where THandler : class, IGameApiHandler
            => config.Dependencies.AddScoped<THandler>();
    }
}
