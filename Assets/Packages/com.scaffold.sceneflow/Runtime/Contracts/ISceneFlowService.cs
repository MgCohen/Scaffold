using System.Threading;
using System.Threading.Tasks;
using UnityEngine.AddressableAssets;
using Scaffold.SceneFlow;

namespace Scaffold.SceneFlow.Contracts
{
    public interface ISceneFlowService
    {
        Task<SceneFlowLoadResult> LoadAdditiveAsync(AssetReference sceneReference, SceneFlowLoadOptions options, CancellationToken cancellationToken = default);

        Task UnloadAsync(SceneFlowLoadResult result, CancellationToken cancellationToken = default);
    }
}
