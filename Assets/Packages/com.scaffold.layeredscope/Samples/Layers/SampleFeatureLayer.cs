using System;
using System.Threading;
using System.Threading.Tasks;
using Scaffold.LayeredScope;
using UnityEngine;
using VContainer;

namespace Scaffold.LayeredScope.Samples.Layers
{
    internal sealed class SampleAsset { public string Payload; }

    internal sealed class SampleFeatureService : IAsyncInitializable, IAsyncDisposable
    {
        private readonly SampleAsset asset;
        private readonly SharedSampleAsset sharedAsset;
        private readonly ISampleConfigService config;
        private readonly ILayerResolver layered;

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

    internal sealed class SampleFeatureLayer : IAsyncScopeLayer
    {
        private SampleAsset prepared;

        public async Task PrepareAsync(IObjectResolver parent, CancellationToken ct)
        {
            var gateway = parent.Resolve<ISampleAssetGateway>();
            string raw = await gateway.LoadAsync("feature.payload", ct);
            prepared = new SampleAsset { Payload = raw };
        }

        public void Install(IContainerBuilder builder)
        {
            builder.RegisterInstance(prepared);
            builder.Register<SampleFeatureService>(Lifetime.Singleton)
                .As<IAsyncInitializable>()
                .As<IAsyncDisposable>();
        }
    }
}
