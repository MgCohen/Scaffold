using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

namespace Scaffold.SceneFlow.Contracts
{
    public interface IAddressablesSceneOperations
    {
        AsyncOperationHandle<SceneInstance> LoadSceneAsync(AssetReference sceneReference, LoadSceneMode loadSceneMode, bool activateOnLoad = true, int priority = 100);

        AsyncOperationHandle<SceneInstance> UnloadSceneAsync(AsyncOperationHandle<SceneInstance> sceneLoadHandle, bool autoReleaseHandle = true);
    }
}
