using System.Threading;
using System.Threading.Tasks;
using UnityEngine.AddressableAssets;
using UnityEngine;

namespace Scaffold.Addressables.Contracts
{
    public interface IAddressablesGateway
    {
        Task InitializeAsync(CancellationToken cancellationToken = default);
        Task<IAssetGroupHandle<T>> LoadAsync<T>(AssetLabelReference label, CancellationToken cancellationToken = default) where T : UnityEngine.Object;
        Task<IAssetHandle<T>> LoadAsync<T>(AssetReference reference, CancellationToken cancellationToken = default) where T : UnityEngine.Object;
        Task<IAssetHandle<T>> LoadAsync<T>(AssetReferenceT<T> reference, CancellationToken cancellationToken = default) where T : UnityEngine.Object;
        IAssetGroupHandle<T> Load<T>(AssetLabelReference label, CancellationToken cancellationToken = default) where T : UnityEngine.Object;
        IAssetHandle<T> Load<T>(AssetReference reference, CancellationToken cancellationToken = default) where T : UnityEngine.Object;
        IAssetHandle<T> Load<T>(AssetReferenceT<T> reference, CancellationToken cancellationToken = default) where T : UnityEngine.Object;
    }
}

