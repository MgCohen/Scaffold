using System;
using System.Threading;
using System.Threading.Tasks;
using Scaffold.AppFlow;
using UnityEngine;
using VContainer;

namespace Scaffold.AppFlow.Samples.Layers
{
    internal sealed class SampleConfig { public int Value; }

    internal interface ISampleConfigService { SampleConfig Current { get; } }

    internal sealed class SampleConfigService : ISampleConfigService
    {
        public SampleConfigService(ISampleAssetGateway gateway)
        {
            this.gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
        }

        public SampleConfig Current { get; private set; }

        private readonly ISampleAssetGateway gateway;

        public async Task WarmupAsync(CancellationToken ct)
        {
            Debug.Log("[SampleConfigService] loading via gateway…");
            string raw = await gateway.LoadAsync("config", ct);
            Current = new SampleConfig { Value = raw.Length };
            Debug.Log($"[SampleConfigService] ready (value={Current.Value}).");
        }
    }

    internal sealed class SampleConfigsLayer : IScopeLayer, IInitializableLayer, ILayerProgressSource
    {
        public float Progress { get; private set; }

        public event Action<float> ProgressChanged;

        public void Install(IContainerBuilder builder)
        {
            builder.Register<SampleConfigService>(Lifetime.Singleton).As<ISampleConfigService>();
        }

        public async Task InitializeAsync(ILayerInitRunner runner, CancellationToken ct)
        {
            Report(0.25f);
            var svc = (SampleConfigService)runner.Scope.Resolve<ISampleConfigService>();
            await svc.WarmupAsync(ct);
            Report(0.5f);

            await runner.RunDefaultInitAsync(ct);

            Report(1f);
        }

        private void Report(float v)
        {
            Progress = v;
            ProgressChanged?.Invoke(v);
        }
    }
}
