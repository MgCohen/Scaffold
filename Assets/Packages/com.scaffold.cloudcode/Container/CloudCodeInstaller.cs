using Scaffold.CloudCode;
using SdkCloudCode = Unity.Services.CloudCode;
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
            builder.Register<CloudCodeErrorHandler>(Lifetime.Singleton);
        }

        private void RegisterModuleService(IContainerBuilder builder)
        {
            builder.RegisterInstance(SdkCloudCode.CloudCodeService.Instance)
                .As<SdkCloudCode.ICloudCodeService>();
            builder.Register<CloudCodeSdkCallHandler>(Lifetime.Singleton);
            builder.Register<ICloudCodeService, CloudCodeService>(Lifetime.Singleton);
        }
    }
}
