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

    internal sealed class SampleAssetGateway : ISampleAssetGateway, IAsyncInitializable
    {
        public async Task InitializeAsync(CancellationToken ct)
        {
            Debug.Log("[SampleAssetGateway] warming…");
            await Task.Delay(200, ct);
            Debug.Log("[SampleAssetGateway] ready.");
        }

        public Task<string> LoadAsync(string key, CancellationToken ct) => Task.FromResult($"asset:{key}");
    }

    internal sealed class SharedSampleAsset
    {
        public SharedSampleAsset(string payload)
        {
            Payload = payload;
        }

        public string Payload { get; }
    }

    internal sealed class SharedSampleAssetWarmer : IAsyncInitializable
    {
        private readonly ISampleAssetGateway gateway;

        public SharedSampleAssetWarmer(ISampleAssetGateway gateway)
        {
            if (gateway == null) throw new System.ArgumentNullException(nameof(gateway));
            this.gateway = gateway;
        }

        public SharedSampleAsset Asset { get; private set; }

        public async Task InitializeAsync(CancellationToken ct)
        {
            Debug.Log("[SharedSampleAssetWarmer] preloading shared asset…");
            string raw = await gateway.LoadAsync("shared.payload", ct);
            Asset = new SharedSampleAsset(raw);
            Debug.Log($"[SharedSampleAssetWarmer] ready (payload='{Asset.Payload}').");
        }
    }

    internal sealed class SampleAssetsLayer : IScopeLayer
    {
        public void Install(IContainerBuilder builder)
        {
            builder.Register<SampleAssetGateway>(Lifetime.Singleton)
                .As<ISampleAssetGateway>()
                .As<IAsyncInitializable>();

            builder.Register<SharedSampleAssetWarmer>(Lifetime.Singleton)
                .AsSelf()
                .As<IAsyncInitializable>();

            // Resolve-on-demand factory: child layers inject `SharedSampleAsset` directly,
            // and the warmer's IAsyncInitializable wave guarantees it is populated first.
            builder.Register(resolver => resolver.Resolve<SharedSampleAssetWarmer>().Asset, Lifetime.Singleton);
        }
    }
}
