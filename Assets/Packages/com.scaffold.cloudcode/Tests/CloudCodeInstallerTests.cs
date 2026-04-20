using NUnit.Framework;
using Scaffold.CloudCode;
using Scaffold.CloudCode.Container;
using VContainer;

namespace Scaffold.CloudCode.Tests
{
    public sealed class CloudCodeInstallerTests
    {
        [Test]
        public void Install_BuildsContainer_ResolvesScaffoldCloudCodeService()
        {
            var builder = new ContainerBuilder();
            new CloudCodeInstaller().Install(builder);
            using (IObjectResolver container = builder.Build())
            {
                Assert.DoesNotThrow(() => container.Resolve<ICloudCodeService>());
            }
        }
    }
}
