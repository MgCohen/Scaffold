using System.Threading;
using System.Threading.Tasks;
using Scaffold.LayeredScope;
using UnityEngine;
using VContainer;

namespace Scaffold.LayeredScope.Samples.Layers
{
    internal sealed class SampleConfig { public int Value; }

    internal interface ISampleConfigService { SampleConfig Current { get; } }

    internal sealed class SampleConfigService : ISampleConfigService, IAsyncInitializable
    {
        public SampleConfigService(ISampleAssetGateway gateway)
        {
            if (gateway == null) throw new System.ArgumentNullException(nameof(gateway));
            this.gateway = gateway;
        }

        public SampleConfig Current { get; private set; }

        private readonly ISampleAssetGateway gateway;

        public async Task InitializeAsync(CancellationToken ct)
        {
            Debug.Log("[SampleConfigService] loading via gateway…");
            string raw = await gateway.LoadAsync("config", ct);
            Current = new SampleConfig { Value = raw.Length };
            Debug.Log($"[SampleConfigService] ready (value={Current.Value}).");
        }
    }

    internal sealed class SampleConfigsLayer : IScopeLayer
    {
        public void Install(IContainerBuilder builder)
        {
            builder.Register<SampleConfigService>(Lifetime.Singleton)
                .As<ISampleConfigService>()
                .As<IAsyncInitializable>();
        }
    }
}
