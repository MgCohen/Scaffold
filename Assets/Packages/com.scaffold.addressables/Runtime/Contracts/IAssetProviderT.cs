using UnityEngine;

namespace Scaffold.Addressables.Contracts
{
    public interface IAssetProvider<TAsset> : IAssetProvider where TAsset : UnityEngine.Object
    {
        bool TryGet(out TAsset asset);
    }
}
