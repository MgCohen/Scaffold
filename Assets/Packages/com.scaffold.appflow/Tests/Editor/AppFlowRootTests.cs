using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Scaffold.AppFlow;
using NUnit.Framework;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Scaffold.AppFlow.Tests.Editor
{
    [TestFixture]
    public sealed class AppFlowRootTests
    {
        [Test]
        public async Task OnStartupFailedAsync_InvokedWhenInitialLayerInitializableThrows()
        {
            var go = new GameObject("BootstrapTest");
            try
            {
                var bootstrap = go.AddComponent<RecordingBootstrap>();

                Exception faulted = null;
                try
                {
                    await bootstrap.ReadyTask;
                }
                catch (Exception ex)
                {
                    faulted = ex;
                }

                Assert.That(faulted, Is.Not.Null, "ReadyTask should have faulted.");
                Assert.That(bootstrap.StartupFailureCalled, Is.True, "OnStartupFailedAsync must be invoked on failure.");
                Assert.That(bootstrap.StartupFailureException, Is.SameAs(faulted));

                IAppFlowErrorHandler handler = bootstrap.Container.Resolve<IAppFlowErrorHandler>();
                IReadOnlyList<AppFlowErrorInfo> recent = handler.Recent;
                Assert.That(recent.Count, Is.EqualTo(2));
                Assert.That(recent[0].Phase, Is.EqualTo(AppFlowErrorPhase.Init));
                Assert.That(recent[1].Phase, Is.EqualTo(AppFlowErrorPhase.Startup));
                Assert.That(recent[0].Exception, Is.SameAs(recent[1].Exception));
            }
            finally
            {
                if (go != null)
                {
                    UnityEngine.Object.DestroyImmediate(go);
                }
            }
        }

        private sealed class RecordingBootstrap : AppFlowRoot
        {
            public bool StartupFailureCalled { get; private set; }
            public Exception StartupFailureException { get; private set; }

            protected override IEnumerable<IScopeLayer> GetInitialLayers()
            {
                yield return new ThrowingInitLayer();
            }

            protected override Task OnStartupFailedAsync(Exception ex, CancellationToken ct)
            {
                StartupFailureCalled = true;
                StartupFailureException = ex;
                return Task.CompletedTask;
            }
        }

        private sealed class ThrowingInitLayer : IScopeLayer
        {
            public void Install(IContainerBuilder builder)
            {
                builder.Register<ThrowingInitializable>(Lifetime.Singleton).As<IAsyncInitializable>();
            }

            private sealed class ThrowingInitializable : IAsyncInitializable
            {
                public Task InitializeAsync(CancellationToken ct)
                {
                    throw new InvalidOperationException("startup boom");
                }
            }
        }
    }
}
