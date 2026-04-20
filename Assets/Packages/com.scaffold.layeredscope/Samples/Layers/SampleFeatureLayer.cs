// sample: Feature layer only — ctor-injects SampleAsset and SharedSampleAsset published from SampleAssetsLayer. See Samples/README.md.

using System;
using System.Threading;
using System.Threading.Tasks;
using Scaffold.LayeredScope;
using UnityEngine;
using VContainer;

namespace Scaffold.LayeredScope.Samples.Layers
{
    internal sealed class SampleFeatureService : IAsyncInitializable, IAsyncDisposable
    {
        public SampleFeatureService(SampleAsset asset, SharedSampleAsset sharedAsset, ISampleConfigService config, ILayerResolver layered)
        {
            if (asset == null) throw new ArgumentNullException(nameof(asset));
            if (sharedAsset == null) throw new ArgumentNullException(nameof(sharedAsset));
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (layered == null) throw new ArgumentNullException(nameof(layered));
            this.asset = asset;
            this.sharedAsset = sharedAsset;
            this.config = config;
            this.layered = layered;
        }

        private readonly SampleAsset asset;
        private readonly SharedSampleAsset sharedAsset;
        private readonly ISampleConfigService config;
        private readonly ILayerResolver layered;

        public Task InitializeAsync(CancellationToken ct)
        {
            Debug.Log(
                $"[SampleFeatureService] init asset='{asset.Payload}', shared='{sharedAsset.Payload}', " +
                $"config={config.Current.Value}, top resolves gateway? {layered.TryResolve(out ISampleAssetGateway _)}");
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            Debug.Log("[SampleFeatureService] async dispose.");
            return default;
        }
    }

    internal sealed class SampleFeatureLayer : IScopeLayer
    {
        public void Install(IContainerBuilder builder)
        {
            builder.Register<SampleFeatureService>(Lifetime.Singleton)
                .As<IAsyncInitializable>()
                .As<IAsyncDisposable>();
        }
    }
}
