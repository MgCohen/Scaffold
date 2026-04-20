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
            rootScope = LifetimeScope.Create(ConfigureTestRoot, "LayeredScopeTestRoot");
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
            await ExpectThrowsInvalidOpAsync(() => host.PushAsync(failingLayer, CancellationToken.None));
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
            await ExpectThrowsInvalidOpAsync(() => host.PushAsync(failingLayer, CancellationToken.None));
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
            await ExpectThrowsInvalidOpAsync(() => host.PopAsync(CancellationToken.None));
            Assert.That(captured, Has.Count.EqualTo(1));
            Assert.That(captured[0].op, Is.EqualTo(LayerOperation.Dispose));
            Assert.That(captured[0].layer, Is.SameAs(layer));
        }

        [Test]
        public async Task ParentLayer_PublisherProvider_InjectableInChildLayer()
        {
            var host = new ApplicationHost(rootScope);
            var parentLayer = new PublishingProviderLayer();
            var childLayer = new ChildConsumerLayer();
            await host.PushAsync(parentLayer, CancellationToken.None);
            await host.PushAsync(childLayer, CancellationToken.None);
            Assert.That(childLayer.Captured, Is.Not.Null, "Child layer's consumer should have been initialized.");
            Assert.That(childLayer.Captured.Asset, Is.Not.Null, "Parent-published asset must be injected into child consumer.");
            Assert.That(childLayer.Captured.Asset.Tag, Is.EqualTo("from-parent"));
        }

        [Test]
        public async Task ProviderPublishedAsset_NotResolvableInPublishingLayer()
        {
            var host = new ApplicationHost(rootScope);
            await host.PushAsync(new PublishingProviderLayer(), CancellationToken.None);
            Assert.That(host.Top.TryResolve<SharedAsset>(out _), Is.False);
            var childLayer = new ChildConsumerLayer();
            await host.PushAsync(childLayer, CancellationToken.None);
            Assert.That(host.Top.TryResolve<SharedAsset>(out SharedAsset asset), Is.True);
            Assert.That(asset.Tag, Is.EqualTo("from-parent"));
        }

        [Test]
        public async Task ProviderPublishMany_ExposesIReadOnlyListAndIndividualItems()
        {
            var host = new ApplicationHost(rootScope);
            await host.PushAsync(new PublishManyProviderLayer(), CancellationToken.None);
            var consumerLayer = new PublishManyConsumerLayer();
            await host.PushAsync(consumerLayer, CancellationToken.None);
            Assert.That(consumerLayer.Captured.Items, Is.Not.Null);
            Assert.That(consumerLayer.Captured.Items.Count, Is.EqualTo(2));
            Assert.That(consumerLayer.Captured.LastItem.Id, Is.EqualTo("b"));
        }

        [Test]
        public async Task ChildLayerPublisher_DoesNotLeakIntoParent()
        {
            var host = new ApplicationHost(rootScope);
            await host.PushAsync(new EmptyLayer(), CancellationToken.None);
            IObjectResolver parentResolver = host.Top;
            await host.PushAsync(new PublishingProviderLayer(), CancellationToken.None);
            Assert.That(parentResolver.TryResolve<SharedAsset>(out _), Is.False);
            Assert.That(host.Top.TryResolve<SharedAsset>(out _), Is.True);
        }

        private void ConfigureTestRoot(IContainerBuilder builder)
        {
            var proxy = new LayerResolverProxy();
            builder.RegisterInstance<LayerResolverProxy, ILayerResolver>(proxy);
        }

        private async Task ExpectThrowsInvalidOpAsync(Func<Task> action)
        {
            try
            {
                await action();
                Assert.Fail("Expected InvalidOperationException.");
            }
            catch (InvalidOperationException)
            {
            }
        }

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

        private sealed class EmptyLayer : IScopeLayer
        {
            public void Install(IContainerBuilder builder) { }
        }

        private sealed class SharedAsset
        {
            public SharedAsset(string tag) { Tag = tag; }
            public string Tag { get; }
        }

        private sealed class PublishingProviderLayer : IScopeLayer
        {
            public void Install(IContainerBuilder builder)
            {
                builder.Register<PublishOnInit>(Lifetime.Singleton).As<IAsyncInitializable>();
            }

            private sealed class PublishOnInit : IAsyncInitializable
            {
                public PublishOnInit(ILayerPublisher layerPublisher)
                {
                    this.layerPublisher = layerPublisher;
                }

                private readonly ILayerPublisher layerPublisher;

                public Task InitializeAsync(CancellationToken ct)
                {
                    layerPublisher.Publish(new SharedAsset("from-parent"));
                    return Task.CompletedTask;
                }
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

                public Task InitializeAsync(CancellationToken ct)
                {
                    return Task.CompletedTask;
                }
            }
        }

        private sealed class Item
        {
            public Item(string id) { Id = id; }
            public string Id { get; }
        }

        private sealed class PublishManyProviderLayer : IScopeLayer
        {
            public void Install(IContainerBuilder builder)
            {
                builder.Register<PublishManyOnInit>(Lifetime.Singleton).As<IAsyncInitializable>();
            }

            private sealed class PublishManyOnInit : IAsyncInitializable
            {
                public PublishManyOnInit(ILayerPublisher layerPublisher)
                {
                    this.layerPublisher = layerPublisher;
                }

                private readonly ILayerPublisher layerPublisher;

                public Task InitializeAsync(CancellationToken ct)
                {
                    IReadOnlyList<Item> list = new List<Item> { new Item("a"), new Item("b") };
                    layerPublisher.PublishMany(list);
                    return Task.CompletedTask;
                }
            }
        }

        private sealed class PublishManyConsumerLayer : IScopeLayer
        {
            public Consumer Captured { get; private set; }

            public void Install(IContainerBuilder builder)
            {
                builder.Register<Consumer>(Lifetime.Singleton)
                    .AsSelf()
                    .As<IAsyncInitializable>();
                builder.RegisterBuildCallback(c => Captured = c.Resolve<Consumer>());
            }

            public sealed class Consumer : IAsyncInitializable
            {
                public Consumer(IReadOnlyList<Item> items, Item item)
                {
                    Items = items;
                    LastItem = item;
                }

                public IReadOnlyList<Item> Items { get; }
                public Item LastItem { get; }

                public Task InitializeAsync(CancellationToken ct)
                {
                    return Task.CompletedTask;
                }
            }
        }
    }
}
