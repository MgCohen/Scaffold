using Scaffold.Addressables;
using Scaffold.Addressables.Contracts;
using Scaffold.Addressables.Internal;
using Scaffold.AppFlow;
using VContainer;
using VContainer.Unity;

namespace Scaffold.Addressables.Container
{
    public sealed class AddressablesInstaller : IInstaller
    {
        public void Install(IContainerBuilder builder)
        {
            builder.Register<AddressablesAssetClient>(Lifetime.Singleton)
                .As<IAddressablesAssetClient>();

            builder.Register<AddressablesAssetReferenceHandler>(Lifetime.Singleton)
                .As<IAssetReferenceHandler>();

            builder.Register<AddressablesGateway>(Lifetime.Singleton)
                .As<IAddressablesGateway>()
                .As<IAsyncInitializable>();
        }
    }
}
