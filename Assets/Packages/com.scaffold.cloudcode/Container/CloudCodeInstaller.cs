using Scaffold.CloudCode;
using VContainer;
using VContainer.Unity;

namespace Scaffold.CloudCode.Container
{
    public sealed class CloudCodeInstaller : IInstaller
    {
        public void Install(IContainerBuilder builder)
        {
            builder.Register<ICloudCodeModuleService, CloudCodeModuleService>(Lifetime.Singleton);
        }
    }
}
