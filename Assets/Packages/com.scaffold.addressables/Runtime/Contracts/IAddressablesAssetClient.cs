using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine.AddressableAssets;
using UnityEngine;

namespace Scaffold.Addressables.Contracts
{
    public interface IAddressablesAssetClient
    {
        Task SyncCatalogAndContentAsync(CancellationToken cancellationToken);

        Task<T> LoadAssetAsync<T>(string key, CancellationToken cancellationToken) where T : UnityEngine.Object;

        Task<IReadOnlyList<T>> LoadAssetsByLabelAsync<T>(AssetLabelReference label, CancellationToken cancellationToken) where T : UnityEngine.Object;

        Task<IReadOnlyList<string>> ResolveLabelAsync<T>(AssetLabelReference label, CancellationToken cancellationToken) where T : UnityEngine.Object;

        void Release(UnityEngine.Object asset);
    }
}

