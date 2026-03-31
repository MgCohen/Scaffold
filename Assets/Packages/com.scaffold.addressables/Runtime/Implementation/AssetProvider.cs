using System;
using System.Threading;
using System.Threading.Tasks;
using Scaffold.Addressables.Contracts;
using UnityEngine.AddressableAssets;
using VContainer;

namespace Scaffold.Addressables
{
    public abstract class AssetProvider<TAsset> : IAssetProvider<TAsset>, IAssetPreloader, IAssetRegistrar where TAsset : UnityEngine.Object
    {
        protected AssetProvider(IAddressablesGateway gateway)
        {
            this.gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
        }

        protected abstract AssetReference AssetKey { get; }
        protected TAsset LoadedAsset { get; private set; }
        
        private readonly IAddressablesGateway gateway;

        public async Task PreloadAsync(CancellationToken cancellationToken)
        {
            IAssetHandle<TAsset> handle = await LoadCoreAsync(cancellationToken);
            LoadedAsset = handle.Asset;
        }

        public bool TryGet(out TAsset asset)
        {
            asset = LoadedAsset;
            return asset != null;
        }

        public virtual void Register(IContainerBuilder builder)
        {
            if (builder == null || LoadedAsset == null)
            {
                return;
            }

            builder.RegisterInstance(LoadedAsset);
        }

        protected virtual Task<IAssetHandle<TAsset>> LoadCoreAsync(CancellationToken cancellationToken)
        {
            return gateway.LoadAsync<TAsset>(AssetKey, cancellationToken);
        }
    }
}
