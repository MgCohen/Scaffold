using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Scaffold.Addressables.Contracts;
using Scaffold.AppFlow;
using Scaffold.Events.Contracts;
using Scaffold.Navigation;
using Scaffold.Navigation.Container;
using Scaffold.Navigation.Contracts;
using UnityEngine;
using UnityEngine.AddressableAssets;
using Object = UnityEngine.Object;
using VContainer;

namespace Scaffold.Navigation.Tests
{
    public sealed class NavigationInstallerAndInjectionTests
    {
        [Test]
        public void SettingsParam_RegistersSettings()
        {
            var holder = new GameObject("nav-holder").transform;
            var settings = ScriptableObject.CreateInstance<NavigationSettings>();
            var resolver = new DeferredLayerResolver();
            var container = BuildNavigationContainer(resolver, holder, settings);
            resolver.Bind(container);

            Assert.That(container.Resolve<NavigationSettings>(), Is.SameAs(settings));
            Object.DestroyImmediate(holder.gameObject);
            Object.DestroyImmediate(settings);
        }

        [Test]
        public void NoSettingsParam_DoesNotRegisterSettings()
        {
            var holder = new GameObject("nav-holder").transform;
            var resolver = new DeferredLayerResolver();
            var builder = new ContainerBuilder();
            builder.RegisterInstance<ILayerResolver>(resolver);
            new NavigationInstaller(holder).Install(builder);
            var container = builder.Build();
            resolver.Bind(container);

            Assert.That(container.TryResolve<NavigationSettings>(out _), Is.False);
            Object.DestroyImmediate(holder.gameObject);
        }

        [Test]
        public void SingleInstance_OpenHandlerAndInjector_AreSameInstance()
        {
            var holder = new GameObject("nav-holder").transform;
            var settings = ScriptableObject.CreateInstance<NavigationSettings>();
            var resolver = new DeferredLayerResolver();
            var container = BuildNavigationContainer(resolver, holder, settings);
            resolver.Bind(container);

            var open = container.Resolve<INavigationOpenHandler>();
            var injector = container.Resolve<IViewControllerDependencyInjector>();
            Assert.That(open, Is.SameAs(injector));

            Object.DestroyImmediate(holder.gameObject);
            Object.DestroyImmediate(settings);
        }

        [Test]
        public void Inject_ThrowsOnNullController()
        {
            var holder = new GameObject("nav-holder").transform;
            var settings = ScriptableObject.CreateInstance<NavigationSettings>();
            var resolver = new DeferredLayerResolver();
            var container = BuildNavigationContainer(resolver, holder, settings);
            resolver.Bind(container);

            var injector = container.Resolve<IViewControllerDependencyInjector>();
            Assert.Throws<ArgumentNullException>(() => injector.Inject(null));

            Object.DestroyImmediate(holder.gameObject);
            Object.DestroyImmediate(settings);
        }

        [Test]
        public void PrepareDependencies_InvokesInjector()
        {
            var holder = new GameObject("nav-holder").transform;
            var settings = ScriptableObject.CreateInstance<NavigationSettings>();
            var resolver = new DeferredLayerResolver();
            var container = BuildNavigationContainer(resolver, holder, settings);
            resolver.Bind(container);

            var navigation = container.Resolve<INavigation>();
            var controller = new TestViewController();
            Assert.DoesNotThrow(() => navigation.PrepareDependencies(controller));

            Object.DestroyImmediate(holder.gameObject);
            Object.DestroyImmediate(settings);
        }

        [Test]
        public void ManualSettingsRegistration_WorksWithoutInstallerParam()
        {
            var holder = new GameObject("nav-holder").transform;
            var settings = ScriptableObject.CreateInstance<NavigationSettings>();
            var resolver = new DeferredLayerResolver();
            var builder = new ContainerBuilder();
            builder.RegisterInstance<ILayerResolver>(resolver);
            builder.RegisterInstance<IEventBus>(new FakeEventBus());
            builder.RegisterInstance<IEnumerable<INavigationMiddleware>>(Array.Empty<INavigationMiddleware>());
            builder.RegisterInstance<IAddressablesGateway>(new FakeAddressablesGateway());
            builder.RegisterInstance(settings);
            new NavigationInstaller(holder).Install(builder);
            var container = builder.Build();
            resolver.Bind(container);

            Assert.That(container.Resolve<NavigationSettings>(), Is.SameAs(settings));
            Assert.That(container.Resolve<INavigation>(), Is.Not.Null);

            Object.DestroyImmediate(holder.gameObject);
            Object.DestroyImmediate(settings);
        }

        private static IObjectResolver BuildNavigationContainer(
            DeferredLayerResolver resolver,
            Transform holder,
            NavigationSettings settings)
        {
            var builder = new ContainerBuilder();
            builder.RegisterInstance<ILayerResolver>(resolver);
            builder.RegisterInstance<IEventBus>(new FakeEventBus());
            builder.RegisterInstance<IEnumerable<INavigationMiddleware>>(Array.Empty<INavigationMiddleware>());
            builder.RegisterInstance<IAddressablesGateway>(new FakeAddressablesGateway());
            new NavigationInstaller(holder, settings).Install(builder);
            return builder.Build();
        }

        private sealed class DeferredLayerResolver : ILayerResolver
        {
            private IObjectResolver top;

            public IObjectResolver Top => top ?? throw new InvalidOperationException("ILayerResolver.Top is not bound yet.");

            public void Bind(IObjectResolver resolver)
            {
                top = resolver;
            }

            public bool TryResolve<T>(out T value) => Top.TryResolve(out value);

            public T Resolve<T>() => Top.Resolve<T>();
        }

        private sealed class FakeEventBus : IEventBus
        {
            public void AddListener<T>(Action<T> evt) where T : ContextEvent
            {
            }

            public void RemoveListener<T>(Action<T> evt) where T : ContextEvent
            {
            }

            public void AddListener(Type type, Action<ContextEvent> evt)
            {
            }

            public void RemoveListener(Type type, Action<ContextEvent> evt)
            {
            }

            public void Raise(ContextEvent evt)
            {
            }

            public void Clear()
            {
            }
        }

        private sealed class FakeAddressablesGateway : IAddressablesGateway
        {
            public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

            public Task<IAssetGroupHandle<T>> LoadAsync<T>(AssetLabelReference label, CancellationToken cancellationToken = default) where T : Object =>
                Task.FromException<IAssetGroupHandle<T>>(new NotImplementedException());

            public Task<IAssetHandle<T>> LoadAsync<T>(AssetReference reference, CancellationToken cancellationToken = default) where T : Object =>
                Task.FromException<IAssetHandle<T>>(new NotImplementedException());

            public Task<IAssetHandle<T>> LoadAsync<T>(AssetReferenceT<T> reference, CancellationToken cancellationToken = default) where T : Object =>
                Task.FromException<IAssetHandle<T>>(new NotImplementedException());

            public IAssetGroupHandle<T> Load<T>(AssetLabelReference label, CancellationToken cancellationToken = default) where T : Object =>
                throw new NotImplementedException();

            public IAssetHandle<T> Load<T>(AssetReference reference, CancellationToken cancellationToken = default) where T : Object =>
                throw new NotImplementedException();

            public IAssetHandle<T> Load<T>(AssetReferenceT<T> reference, CancellationToken cancellationToken = default) where T : Object =>
                throw new NotImplementedException();
        }

        private sealed class TestViewController : IViewController
        {
            public void Bind(INavigation navigation)
            {
            }

            public void Close()
            {
            }
        }
    }
}
