using System.Threading;
using System.Threading.Tasks;
using UnityEngine.AddressableAssets;
using Scaffold.SceneFlow;

namespace Scaffold.SceneFlow.Contracts
{
    /// <summary>
    /// Loads and unloads Addressable scenes in <see cref="UnityEngine.SceneManagement.LoadSceneMode.Additive"/> while the Bootstrap scene remains loaded.
    /// </summary>
    public interface ISceneFlowService
    {
        Task<SceneFlowLoadResult> LoadAdditiveAsync(AssetReference sceneReference, SceneFlowLoadOptions options, CancellationToken cancellationToken = default);

        Task UnloadAsync(SceneFlowLoadResult result, CancellationToken cancellationToken = default);
    }
}
