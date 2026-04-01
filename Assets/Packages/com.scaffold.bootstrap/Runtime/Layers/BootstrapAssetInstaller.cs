using Scaffold.Bootstrap.Providers;
using Scaffold.Addressables.Container;
using Scaffold.Addressables.Contracts;
using Scaffold.Scope;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using VContainer;

namespace Scaffold.Bootstrap.Layers
{
    internal sealed class BootstrapAssetInstaller : LayerInstallerBase
    {
        protected override void Install(IContainerBuilder builder)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            var addressablesInstaller = new AddressablesInstaller();
            Install(builder, addressablesInstaller);

            RegisterProvider<NavigationAssetProvider>(builder);
        }

        private void RegisterProvider<T>(IContainerBuilder builder) where T : IAssetPreloader
        {
            builder.Register<T>(Lifetime.Singleton).AsImplementedInterfaces().AsSelf();
        }

        protected override async Task OnCompletedAsync(IObjectResolver resolver, CancellationToken cancellationToken)
        {
            IEnumerable<IAssetPreloader> preloaders = resolver.Resolve<IEnumerable<IAssetPreloader>>();
            foreach (IAssetPreloader preloader in preloaders)
            {
                await preloader.PreloadAsync(cancellationToken);
            }
        }

        protected override void ConfigureChildBuilder(LayerInstallerBase child, IObjectResolver resolver, IContainerBuilder childBuilder)
        {
            if (childBuilder == null)
            {
                return;
            }

            var registrars = resolver.Resolve<IEnumerable<IAssetRegistrar>>();
            foreach (var registrar in registrars)
            {
                registrar.Register(childBuilder);
            }
        }
    }
}
