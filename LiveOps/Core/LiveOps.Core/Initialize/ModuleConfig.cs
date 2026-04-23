using System;
using System.Linq;
using System.Reflection;
using LiveOps.Core.DTO.ModuleRequest;
using LiveOps.Core.GameApi;
using LiveOps.Core.Initialize;
using Microsoft.Extensions.DependencyInjection;
using Unity.Services.CloudCode.Core;

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
        new CoreInstaller().Install(config);

        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies()
            .Where(static a =>
            {
                string? name = a.GetName().Name;
                return name != null && name.StartsWith("LiveOps", StringComparison.Ordinal);
            }))
        {
            foreach (Type type in SafeGetTypes(assembly))
            {
                if (type.IsAbstract || type.IsInterface)
                {
                    continue;
                }

                if (type == typeof(CoreInstaller))
                {
                    continue;
                }

                if (!typeof(ICloudCodeInstaller).IsAssignableFrom(type))
                {
                    continue;
                }

                if (Activator.CreateInstance(type) is ICloudCodeInstaller installer)
                {
                    installer.Install(config);
                }
            }
        }
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
