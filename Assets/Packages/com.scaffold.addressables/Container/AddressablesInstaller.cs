using Scaffold.Addressables;
using Scaffold.Addressables.Contracts;
using Scaffold.LayeredScope;
using VContainer;
using VContainer.Unity;

namespace Scaffold.Addressables.Container
{
    public sealed class AddressablesInstaller : IInstaller
    {
        public void Install(IContainerBuilder builder)
        {
            IAddressablesAssetClient assetClient = new AddressablesAssetClient();
            IAssetReferenceHandler assetReferenceHandler = new AddressablesAssetReferenceHandler(assetClient);
            RegisterGateway(builder, assetClient, assetReferenceHandler);
        }

        private void RegisterGateway(IContainerBuilder builder, IAddressablesAssetClient assetClient, IAssetReferenceHandler assetReferenceHandler)
        {
            // Singleton: layered startup uses one gateway across the stack; Scoped would allocate one gateway per scope and run catalog sync multiple times.
            // Register concrete type once; map interfaces explicitly — Register<TInterface,TImpl> + AsImplementedInterfaces duplicates IAddressablesGateway.
            builder.Register<AddressablesGateway>(Lifetime.Singleton)
                .WithParameter<IAddressablesAssetClient>(assetClient)
                .WithParameter<IAssetReferenceHandler>(assetReferenceHandler)
                .As<IAddressablesGateway>()
                .As<IAsyncInitializable>();
        }
    }
}
