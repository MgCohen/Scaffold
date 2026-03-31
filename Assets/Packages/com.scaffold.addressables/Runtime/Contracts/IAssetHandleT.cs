using UnityEngine;

namespace Scaffold.Addressables.Contracts
{
    public interface IAssetHandle<out T> : IAssetHandle where T : UnityEngine.Object
    {
        T Asset { get; }
    }
}
