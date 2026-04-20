using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Scaffold.LayeredScope;
using Scaffold.LayeredScope.Internal;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VContainer;
using VContainer.Unity;

namespace Scaffold.LayeredScope.Tests.Editor
{
    [TestFixture]
    public sealed class ApplicationHostTests
    {
        private LifetimeScope rootScope;

        [SetUp]
        public void SetUp()
        {
            rootScope = LifetimeScope.Create(builder =>
            {
                var proxy = new LayerResolverProxy();
                builder.RegisterInstance<LayerResolverProxy, ILayerResolver>(proxy);
            }, "LayeredScopeTestRoot");
        }

        [TearDown]
        public void TearDown()
        {
            if (rootScope != null)
            {
                rootScope.Dispose();
                rootScope = null;
            }
        }

        [Test]
        public async Task PushAsync_PrepareThrows_RaisesLayerFailedPrepare()
        {
            var host = new ApplicationHost(rootScope);
            var captured = new List<(LayerOperation op, IScopeLayer layer, Exception ex)>();
            host.LayerFailed += (op, layer, ex) => captured.Add((op, layer, ex));

            var failingLayer = new ThrowingPrepareLayer();
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("PrepareAsync failed for 'ThrowingPrepareLayer'"));

            try
            {
                await host.PushAsync(failingLayer, CancellationToken.None);
                Assert.Fail("Expected PushAsync to throw.");
            }
            catch (InvalidOperationException)
            {
            }

            Assert.That(captured, Has.Count.EqualTo(1));
            Assert.That(captured[0].op, Is.EqualTo(LayerOperation.Prepare));
            Assert.That(captured[0].layer, Is.SameAs(failingLayer));
            Assert.That(captured[0].ex, Is.InstanceOf<InvalidOperationException>());
        }

        [Test]
        public async Task PushAsync_InitializableThrows_RaisesLayerFailedInit()
        {
            var host = new ApplicationHost(rootScope);
            var captured = new List<(LayerOperation op, IScopeLayer layer, Exception ex)>();
            host.LayerFailed += (op, layer, ex) => captured.Add((op, layer, ex));

            var failingLayer = new ThrowingInitLayer();
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("Push failed for 'ThrowingInitLayer'"));

            try
            {
                await host.PushAsync(failingLayer, CancellationToken.None);
                Assert.Fail("Expected PushAsync to throw.");
            }
            catch (InvalidOperationException)
            {
            }

            Assert.That(captured, Has.Count.EqualTo(1));
            Assert.That(captured[0].op, Is.EqualTo(LayerOperation.Init));
            Assert.That(captured[0].layer, Is.SameAs(failingLayer));
        }

        [Test]
        public async Task PopAsync_DisposeThrows_RaisesLayerFailedDispose()
        {
            var host = new ApplicationHost(rootScope);
            var captured = new List<(LayerOperation op, IScopeLayer layer, Exception ex)>();
            host.LayerFailed += (op, layer, ex) => captured.Add((op, layer, ex));

            var layer = new ThrowingDisposeLayer();
            await host.PushAsync(layer, CancellationToken.None);

            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("DisposeAsync failed for 'ThrowingDisposeLayer'"));

            try
            {
                await host.PopAsync(CancellationToken.None);
                Assert.Fail("Expected PopAsync to throw.");
            }
            catch (InvalidOperationException)
            {
            }

            Assert.That(captured, Has.Count.EqualTo(1));
            Assert.That(captured[0].op, Is.EqualTo(LayerOperation.Dispose));
            Assert.That(captured[0].layer, Is.SameAs(layer));
        }

        [Test]
        public async Task ParentLayer_RegistersInstance_InjectableInChildLayer()
        {
            var host = new ApplicationHost(rootScope);
            var parentLayer = new ParentSharedAssetLayer();
            var childLayer = new ChildConsumerLayer();

            await host.PushAsync(parentLayer, CancellationToken.None);
            await host.PushAsync(childLayer, CancellationToken.None);

            Assert.That(childLayer.Captured, Is.Not.Null, "Child layer's consumer should have been initialized.");
            Assert.That(childLayer.Captured.Asset, Is.Not.Null, "Parent-registered asset must be injected into child consumer.");
            Assert.That(childLayer.Captured.Asset.Tag, Is.EqualTo("from-parent"));
        }

        // -- helpers --

        private sealed class ThrowingPrepareLayer : IAsyncScopeLayer
        {
            public Task PrepareAsync(IObjectResolver parent, CancellationToken ct)
            {
                throw new InvalidOperationException("prepare boom");
            }

            public void Install(IContainerBuilder builder) { }
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
                    throw new InvalidOperationException("init boom");
                }
            }
        }

        private sealed class ThrowingDisposeLayer : IScopeLayer
        {
            public void Install(IContainerBuilder builder)
            {
                builder.Register<ThrowingDisposable>(Lifetime.Singleton).As<IAsyncDisposable>();
            }

            private sealed class ThrowingDisposable : IAsyncDisposable
            {
                public ValueTask DisposeAsync()
                {
                    throw new InvalidOperationException("dispose boom");
                }
            }
        }

        private sealed class SharedAsset
        {
            public SharedAsset(string tag) { Tag = tag; }
            public string Tag { get; }
        }

        private sealed class SharedAssetWarmer : IAsyncInitializable
        {
            public SharedAsset Asset { get; private set; }

            public Task InitializeAsync(CancellationToken ct)
            {
                Asset = new SharedAsset("from-parent");
                return Task.CompletedTask;
            }
        }

        private sealed class ParentSharedAssetLayer : IScopeLayer
        {
            public void Install(IContainerBuilder builder)
            {
                builder.Register<SharedAssetWarmer>(Lifetime.Singleton)
                    .AsSelf()
                    .As<IAsyncInitializable>();
                builder.Register(resolver => resolver.Resolve<SharedAssetWarmer>().Asset, Lifetime.Singleton);
            }
        }

        private sealed class ChildConsumerLayer : IScopeLayer
        {
            public ChildConsumer Captured { get; private set; }

            public void Install(IContainerBuilder builder)
            {
                builder.Register<ChildConsumer>(Lifetime.Singleton)
                    .AsSelf()
                    .As<IAsyncInitializable>();
                builder.RegisterBuildCallback(container =>
                {
                    Captured = container.Resolve<ChildConsumer>();
                });
            }

            public sealed class ChildConsumer : IAsyncInitializable
            {
                public ChildConsumer(SharedAsset asset)
                {
                    if (asset == null) throw new ArgumentNullException(nameof(asset));
                    Asset = asset;
                }

                public SharedAsset Asset { get; }

                public Task InitializeAsync(CancellationToken ct) => Task.CompletedTask;
            }
        }
    }
}
