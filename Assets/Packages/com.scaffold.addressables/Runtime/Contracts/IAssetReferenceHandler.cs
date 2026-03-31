using System.Threading;
using System.Threading.Tasks;
namespace Scaffold.Addressables.Contracts
{
    public interface IAssetReferenceHandler
    {
        Task<IAssetHandle<T>> AcquireAsync<T>(string key, CancellationToken cancellationToken) where T : UnityEngine.Object;

        Task<IAssetHandle<T>> AcquireAsync<T>(string key, PreloadMode preloadMode, bool isPreload, CancellationToken cancellationToken) where T : UnityEngine.Object;
    }
}
