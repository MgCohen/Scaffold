using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Scaffold.Addressables.Contracts;
using UnityEngine.AddressableAssets;
using VContainer;

namespace Scaffold.Addressables
{
    public abstract class AssetGroupProvider<TAsset> : IAssetGroupProvider<TAsset>, IAssetPreloader, IAssetRegistrar where TAsset : UnityEngine.Object
    {
        protected AssetGroupProvider(IAddressablesGateway gateway)
        {
            this.gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
        }

        protected abstract AssetLabelReference LabelKey { get; }

        private readonly IAddressablesGateway gateway;
        private readonly List<TAsset> loadedAssets = new List<TAsset>();

        public async Task PreloadAsync(CancellationToken cancellationToken)
        {
            IAssetGroupHandle<TAsset> group = await LoadCoreAsync(cancellationToken);
            await group.WhenReady;
            loadedAssets.Clear();
            for (int i = 0; i < group.Assets.Count; i++)
            {
                loadedAssets.Add(group.Assets[i]);
            }
        }

        public bool TryGet(out IReadOnlyList<TAsset> assets)
        {
            assets = loadedAssets;
            return loadedAssets.Count > 0;
        }

        public virtual void Register(IContainerBuilder builder)
        {
            if (builder == null || loadedAssets.Count == 0)
            {
                return;
            }

            builder.RegisterInstance<IReadOnlyList<TAsset>>(loadedAssets);
        }

        protected virtual Task<IAssetGroupHandle<TAsset>> LoadCoreAsync(CancellationToken cancellationToken)
        {
            return gateway.LoadAsync<TAsset>(LabelKey, cancellationToken);
        }
    }
}
