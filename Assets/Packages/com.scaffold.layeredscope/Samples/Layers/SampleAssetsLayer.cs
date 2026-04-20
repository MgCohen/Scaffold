// sample: Single asset layer — gateway warmup plus AssetPublisherBase<T> publishers. See Samples/README.md.

using System;
using System.Threading;
using System.Threading.Tasks;
using Scaffold.LayeredScope;
using UnityEngine;
using VContainer;

namespace Scaffold.LayeredScope.Samples.Layers
{
    internal interface ISampleAssetGateway
    {
        Task<string> LoadAsync(string key, CancellationToken ct);
    }

    internal sealed class SampleAsset { public string Payload; }

    internal sealed class SampleAssetGateway : ISampleAssetGateway, IAsyncInitializable
    {
        public async Task InitializeAsync(CancellationToken ct)
        {
            Debug.Log("[SampleAssetGateway] warming…");
            await Task.Delay(200, ct);
            Debug.Log("[SampleAssetGateway] ready.");
        }

        public Task<string> LoadAsync(string key, CancellationToken ct)
        {
            return Task.FromResult($"asset:{key}");
        }
    }

    internal sealed class SharedSampleAsset
    {
        public SharedSampleAsset(string payload)
        {
            Payload = payload;
        }

        public string Payload { get; }
    }

    internal sealed class SampleSharedPublishedAssetProvider : AssetPublisherBase<SharedSampleAsset>
    {
        public SampleSharedPublishedAssetProvider(ILayerPublisher layerPublisher, ISampleAssetGateway gateway) : base(layerPublisher)
        {
            this.gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
        }

        private readonly ISampleAssetGateway gateway;

        protected override async Task<SharedSampleAsset> LoadAssetAsync(CancellationToken ct)
        {
            string raw = await gateway.LoadAsync("shared.payload", ct).ConfigureAwait(false);
            Debug.Log($"[SampleSharedPublishedAsset] published payload='{raw}'.");
            return new SharedSampleAsset(raw);
        }
    }

    internal sealed class SampleFeaturePayloadPublishedAssetProvider : AssetPublisherBase<SampleAsset>
    {
        public SampleFeaturePayloadPublishedAssetProvider(ILayerPublisher layerPublisher, ISampleAssetGateway gateway) : base(layerPublisher)
        {
            this.gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
        }

        private readonly ISampleAssetGateway gateway;

        protected override async Task<SampleAsset> LoadAssetAsync(CancellationToken ct)
        {
            string raw = await gateway.LoadAsync("feature.payload", ct).ConfigureAwait(false);
            Debug.Log($"[SampleFeaturePayloadPublishedAsset] published payload='{raw}'.");
            return new SampleAsset { Payload = raw };
        }
    }

    internal sealed class SampleAssetsLayer : IScopeLayer
    {
        public void Install(IContainerBuilder builder)
        {
            builder.Register<SampleAssetGateway>(Lifetime.Singleton)
                .As<ISampleAssetGateway>()
                .As<IAsyncInitializable>();

            builder.Register<SampleSharedPublishedAssetProvider>(Lifetime.Singleton)
                .As<IAsyncInitializable>();

            builder.Register<SampleFeaturePayloadPublishedAssetProvider>(Lifetime.Singleton)
                .As<IAsyncInitializable>();
        }
    }
}
