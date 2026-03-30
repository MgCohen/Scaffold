using UnityEngine;
using Scaffold.Addressables.Contracts;

namespace Scaffold.Addressables
{
    internal sealed class AddressablesLoadedEntry
    {
        public AddressablesLoadedEntry(Object asset, PreloadMode policy)
        {
            GuardAsset(asset);
            Asset = asset;
            Policy = policy;
            RefCount = 0;
        }

        public Object Asset { get; }
        public PreloadMode Policy { get; set; }
        public int RefCount { get; set; }

        private void GuardAsset(Object asset)
        {
            if (asset == null)
            {
                throw new System.ArgumentNullException(nameof(asset));
            }
        }
    }
}

