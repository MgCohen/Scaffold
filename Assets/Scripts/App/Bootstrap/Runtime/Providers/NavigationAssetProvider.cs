using Scaffold.Addressables;
using Scaffold.Addressables.Contracts;
using Scaffold.Navigation;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Scaffold.Bootstrap.Providers
{
    public class NavigationAssetProvider : AssetProvider<NavigationSettings>
    {
        public NavigationAssetProvider(IAddressablesGateway gateway) : base(gateway)
        {

        }

        protected override AssetReference AssetKey => new AssetReference("Navigation Settings");
    }
}
