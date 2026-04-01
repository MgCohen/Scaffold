using Scaffold.CloudCode;
using Unity.Services.CloudCode;
using VContainer;
using VContainer.Unity;

namespace Scaffold.CloudCode.Container
{
    public sealed class CloudCodeInstaller : IInstaller
    {
        public CloudCodeInstaller()
        {
            explicitSettings = null;
        }

        public CloudCodeInstaller(CloudCodeSettings moduleSettings)
        {
            explicitSettings = moduleSettings;
        }

        private readonly CloudCodeSettings explicitSettings;

        public void Install(IContainerBuilder builder)
        {
            if (builder == null)
            {
                throw new System.ArgumentNullException(nameof(builder));
            }

            CloudCodeSettings settings = explicitSettings ?? CloudCodeSettings.CreateDefault();
            builder.RegisterInstance(settings);
            RegisterOptimisticRegistry(builder);
            RegisterModuleService(builder);
        }

        private void RegisterOptimisticRegistry(IContainerBuilder builder)
        {
            builder.Register<CloudCodeOptimisticHandlerRegistry>(Lifetime.Singleton);
        }

        private void RegisterModuleService(IContainerBuilder builder)
        {
            builder.Register<ICloudCodeService>(c => CreateCloudCodeService(c), Lifetime.Singleton);
        }

        private static ICloudCodeService CreateCloudCodeService(IObjectResolver resolver)
        {
            CloudCodeSettings resolvedSettings = resolver.Resolve<CloudCodeSettings>();
            ICloudCodeCallHandler baseline = new CloudCodeSdkCallHandler(global::Unity.Services.CloudCode.CloudCodeService.Instance);
            ICloudCodeCallHandler stack = CloudCodeCallHandlerFactory.CreateDefaultStack(resolvedSettings, baseline);
            stack = new CloudCodeSingleFlightCallHandler(stack);
            CloudCodeOptimisticHandlerRegistry optimisticRegistry = resolver.Resolve<CloudCodeOptimisticHandlerRegistry>();
            return new CloudCodeService(resolvedSettings, stack, optimisticRegistry);
        }
    }
}
