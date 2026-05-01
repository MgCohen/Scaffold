using System.Collections.Generic;
using UnityEngine;

namespace Scaffold.Addressables.Contracts
{
    public interface IAssetGroupProvider<TAsset> : IAssetProvider where TAsset : UnityEngine.Object
    {
        bool TryGet(out IReadOnlyList<TAsset> assets);
    }
}
