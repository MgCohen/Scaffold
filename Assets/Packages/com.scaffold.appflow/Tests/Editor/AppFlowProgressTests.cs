using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Scaffold.AppFlow;
using Scaffold.AppFlow.Internal;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VContainer;
using VContainer.Unity;

namespace Scaffold.AppFlow.Tests.Editor
{
    [TestFixture]
    public sealed class AppFlowProgressTests
    {
        private LifetimeScope rootScope;

        [SetUp]
        public void SetUp()
        {
            rootScope = LifetimeScope.Create(ConfigureTestRoot, "AppFlowProgressTestRoot");
            if (rootScope.Container == null)
            {
                rootScope.Build();
            }

            Assert.That(rootScope.Container, Is.Not.Null);
        }

        [TearDown]
        public void TearDown()
        {
            if (rootScope != null)
            {
                UnityEngine.Object.DestroyImmediate(rootScope.gameObject);
                rootScope = null;
            }
        }

        private void ConfigureTestRoot(IContainerBuilder builder)
        {
            var proxy = new LayerResolverProxy();
            builder.RegisterInstance<LayerResolverProxy, ILayerResolver>(proxy);
            builder.Register<AppFlowErrorHandler>(Lifetime.Singleton).As<IAppFlowErrorHandler>().AsSelf();
            builder.Register<AppFlowProgress>(Lifetime.Singleton).As<IAppFlowProgress>().AsSelf();
        }

        [Test]
        public async Task StartupSession_PublishesTotalAndCompletesLayers()
        {
            var errorHandler = rootScope.Container.Resolve<AppFlowErrorHandler>();
            var progress = rootScope.Container.Resolve<AppFlowProgress>();
            var host = new AppFlowHost(rootScope, null, errorHandler, progress);
            var layers = new IScopeLayer[] { new EmptyLayer(), new EmptyLayer() };
            host.BeginSession("Startup", layers.Length);
            try
            {
                await host.InstallAllAsync(layers, CancellationToken.None);
            }
            finally
            {
                host.EndSession(null);
            }

            AppFlowSession s = progress.Current;
            Assert.That(s.Name, Is.EqualTo("Startup"));
            Assert.That(s.TotalLayers, Is.EqualTo(2));
            Assert.That(s.CompletedLayers, Is.EqualTo(2));
            Assert.That(s.IsComplete, Is.True);
            Assert.That(s.Outcome?.Succeeded, Is.True);
        }

        [Test]
        public async Task Failure_AttachesLastErrorToLayerViaOnError()
        {
            var errorHandler = rootScope.Container.Resolve<AppFlowErrorHandler>();
            var progress = rootScope.Container.Resolve<AppFlowProgress>();
            var host = new AppFlowHost(rootScope, null, errorHandler, progress);
            host.BeginSession("X", 1);
            Exception fault = null;
            try
            {
                LogAssert.Expect(LogType.Error, new Regex(@"\[AppFlow\]"));
                LogAssert.Expect(LogType.Error, new Regex("Destroy may not be called from edit mode"));
                await host.InstallAllAsync(new IScopeLayer[] { new ThrowingInitLayer() }, CancellationToken.None);
            }
            catch (InvalidOperationException ex)
            {
                fault = ex;
            }
            finally
            {
                host.EndSession(fault);
            }

            AppFlowSession s = progress.Current;
            Assert.That(s.FailedLayers, Is.EqualTo(1));
            Assert.That(s.Entries.Count, Is.EqualTo(1));
            Assert.That(s.Entries[0].Status, Is.EqualTo(LayerStatus.Failed));
            Assert.That(s.Entries[0].LastError, Is.Not.Null);
            Assert.That(s.Entries[0].LastError.Value.Phase, Is.EqualTo(AppFlowErrorPhase.Init));
        }

        [Test]
        public async Task AdHocPush_OpensSessionAndCloses()
        {
            var errorHandler = rootScope.Container.Resolve<AppFlowErrorHandler>();
            var progress = rootScope.Container.Resolve<AppFlowProgress>();
            var host = new AppFlowHost(rootScope, null, errorHandler, progress);
            await host.PushAsync(new EmptyLayer(), CancellationToken.None);
            IReadOnlyList<AppFlowSession> history = progress.History;
            Assert.That(history.Count, Is.EqualTo(1));
            Assert.That(history[0].Name.StartsWith("Push:", StringComparison.Ordinal), Is.True);
        }

        [Test]
        public void HostSetSubProgress_ClampsToZeroOne()
        {
            var errorHandler = rootScope.Container.Resolve<AppFlowErrorHandler>();
            var progress = rootScope.Container.Resolve<AppFlowProgress>();
            progress.HostBeginSession("clamp", 1);
            int i = progress.HostAddLayer("L");
            progress.HostSetSubProgress(i, 5f);
            Assert.That(progress.Current.Entries[0].SubProgress, Is.EqualTo(1f));
            progress.HostSetSubProgress(i, -3f);
            Assert.That(progress.Current.Entries[0].SubProgress, Is.EqualTo(0f));
            progress.HostEndSession(AppFlowOutcome.CreateSuccess());
        }

        [Test]
        public async Task LayerProgressSource_UpdatesSubProgress()
        {
            var errorHandler = rootScope.Container.Resolve<AppFlowErrorHandler>();
            var progress = rootScope.Container.Resolve<AppFlowProgress>();
            var host = new AppFlowHost(rootScope, null, errorHandler, progress);
            await host.PushAsync(new ProgressReportingLayer(), CancellationToken.None);
            AppFlowSession s = progress.Current;
            Assert.That(s.Entries.Count, Is.EqualTo(1));
            Assert.That(s.Entries[0].SubProgress, Is.EqualTo(1f).Within(0.001f));
        }

        [Test]
        public void Root_ExposesProgressAndErrorsBeforeFirstLayer()
        {
            var go = new GameObject("RootExposeTest");
            try
            {
                var root = go.AddComponent<EmptyBootstrap>();
                if (root.Container == null)
                {
                    root.Build();
                }

                Assert.That(root.Progress, Is.Not.Null);
                Assert.That(root.Errors, Is.Not.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public async Task OverriddenInitializeAsync_NotCallingDefault_LogsWarning()
        {
            LogAssert.Expect(LogType.Warning, new Regex(@"\[AppFlow\].*did not call RunDefaultInitAsync"));
            var errorHandler = rootScope.Container.Resolve<AppFlowErrorHandler>();
            var progress = rootScope.Container.Resolve<AppFlowProgress>();
            var host = new AppFlowHost(rootScope, null, errorHandler, progress);
            host.BeginSession("Warn", 1);
            try
            {
                await host.InstallAllAsync(new IScopeLayer[] { new SkippedDefaultLayer() }, CancellationToken.None);
            }
            finally
            {
                host.EndSession(null);
            }
        }

        private sealed class EmptyBootstrap : AppFlowRoot
        {
            protected override IEnumerable<IScopeLayer> GetInitialLayers()
            {
                yield break;
            }
        }

        private sealed class SkippedDefaultLayer : IScopeLayer, IInitializableLayer
        {
            public void Install(IContainerBuilder builder)
            {
                builder.Register<NoOpInit>(Lifetime.Singleton).As<IAsyncInitializable>();
            }

            public Task InitializeAsync(ILayerInitRunner runner, CancellationToken ct)
            {
                return Task.CompletedTask;
            }

            private sealed class NoOpInit : IAsyncInitializable
            {
                public Task InitializeAsync(CancellationToken ct)
                {
                    return Task.CompletedTask;
                }
            }
        }

        private sealed class EmptyLayer : IScopeLayer
        {
            public void Install(IContainerBuilder builder) { }
        }

        private sealed class ThrowingInitLayer : IScopeLayer
        {
            public void Install(IContainerBuilder builder)
            {
                builder.Register<ThrowingInit>(Lifetime.Singleton).As<IAsyncInitializable>();
            }

            private sealed class ThrowingInit : IAsyncInitializable
            {
                public Task InitializeAsync(CancellationToken ct)
                {
                    throw new InvalidOperationException("init boom");
                }
            }
        }

        private sealed class ProgressReportingLayer : IScopeLayer, IInitializableLayer, ILayerProgressSource
        {
            public float Progress { get; private set; }

            public event Action<float> ProgressChanged;

            public void Install(IContainerBuilder builder)
            {
                builder.Register<DoneInit>(Lifetime.Singleton).As<IAsyncInitializable>();
            }

            public async Task InitializeAsync(ILayerInitRunner runner, CancellationToken ct)
            {
                Progress = 0.5f;
                ProgressChanged?.Invoke(0.5f);
                await runner.RunDefaultInitAsync(ct);
            }

            private sealed class DoneInit : IAsyncInitializable
            {
                public Task InitializeAsync(CancellationToken ct)
                {
                    return Task.CompletedTask;
                }
            }
        }
    }
}
