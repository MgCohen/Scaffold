using System.Threading;
using System.Threading.Tasks;
using Scaffold.Addressables.Contracts;

namespace Scaffold.Addressables.Internal
{
    internal interface IAssetReferenceHandler
    {
        Task<IAssetHandle<T>> AcquireAsync<T>(string key, CancellationToken cancellationToken) where T : UnityEngine.Object;

        Task<IAssetHandle<T>> AcquireAsync<T>(string key, PreloadMode preloadMode, bool isPreload, CancellationToken cancellationToken) where T : UnityEngine.Object;
    }
}
