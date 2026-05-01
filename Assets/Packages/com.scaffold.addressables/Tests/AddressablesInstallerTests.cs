using System.Linq;
using NUnit.Framework;
using Scaffold.Addressables.Container;
using Scaffold.Addressables.Contracts;
using Scaffold.AppFlow;
using VContainer;

namespace Scaffold.Addressables.Tests
{
    public sealed class AddressablesInstallerTests
    {
        [Test]
        public void ResolvesGatewayAsAsyncInitializable_SameSingleton()
        {
            var builder = new ContainerBuilder();
            new AddressablesInstaller().Install(builder);
            var container = builder.Build();

            var gateway = container.Resolve<IAddressablesGateway>();
            var init = container.Resolve<IAsyncInitializable>();
            Assert.That(gateway, Is.SameAs(init));
        }

        [Test]
        public void ResolvesAddressablesAssetClientSingleton()
        {
            var builder = new ContainerBuilder();
            new AddressablesInstaller().Install(builder);
            var container = builder.Build();

            var a = container.Resolve<IAddressablesAssetClient>();
            var b = container.Resolve<IAddressablesAssetClient>();
            Assert.That(a, Is.SameAs(b));
        }

        [Test]
        public void AssetReferenceHandler_IsInternalToAddressablesAssembly()
        {
            var asm = typeof(IAddressablesGateway).Assembly;
            Assert.That(asm.GetType("Scaffold.Addressables.Contracts.IAssetReferenceHandler"), Is.Null);

            var handlerType = asm.GetTypes().FirstOrDefault(t =>
                t.Name == "IAssetReferenceHandler" && t.Namespace == "Scaffold.Addressables.Internal");
            Assert.That(handlerType, Is.Not.Null);
            Assert.That(handlerType.IsPublic, Is.False);
        }
    }
}
