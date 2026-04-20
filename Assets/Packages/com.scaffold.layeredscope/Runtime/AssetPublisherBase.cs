using System;
using System.Threading;
using System.Threading.Tasks;

namespace Scaffold.LayeredScope
{
    // sample: IAsyncInitializable base — subclasses implement LoadAssetAsync; published assets resolve only in descendant layers.
    public abstract class AssetPublisherBase<TAsset> : IAsyncInitializable where TAsset : class
    {
        public AssetPublisherBase(ILayerPublisher layerPublisher)
        {
            this.layerPublisher = layerPublisher ?? throw new ArgumentNullException(nameof(layerPublisher));
        }

        private readonly ILayerPublisher layerPublisher;

        public async Task InitializeAsync(CancellationToken ct)
        {
            TAsset asset = await LoadAssetAsync(ct).ConfigureAwait(false);
            if (asset == null)
            {
                throw new InvalidOperationException($"{GetType().Name}: {nameof(LoadAssetAsync)} returned null.");
            }

            Publish(layerPublisher, asset);
        }

        // sample: Implement loading (Addressables, Resources, gateway, etc.). Must not return null.
        protected abstract Task<TAsset> LoadAssetAsync(CancellationToken ct);

        // sample: Override to call layerPublisher.Publish<TInterface,TImpl> or other registration.
        protected virtual void Publish(ILayerPublisher layerPublisher, TAsset asset)
        {
            layerPublisher.Publish(asset);
        }
    }
}
