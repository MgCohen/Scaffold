using Scaffold.Addressables;
using Scaffold.Addressables.Contracts;
using UnityEngine.AddressableAssets;

namespace Scaffold.Navigation.Providers
{
    public class NavigationAssetProvider : AssetProvider<NavigationSettings>
    {
        public NavigationAssetProvider(IAddressablesGateway gateway) : base(gateway)
        {
        }

        protected override AssetReference AssetKey => new AssetReference("Navigation Settings");
    }
}
