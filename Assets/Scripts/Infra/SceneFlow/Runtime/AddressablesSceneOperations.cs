using Scaffold.SceneFlow.Contracts;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

namespace Scaffold.SceneFlow
{
    public sealed class AddressablesSceneOperations : IAddressablesSceneOperations
    {
        public AsyncOperationHandle<SceneInstance> LoadSceneAsync(AssetReference sceneReference, LoadSceneMode loadSceneMode, bool activateOnLoad = true, int priority = 100)
        {
            return Addressables.LoadSceneAsync(sceneReference, loadSceneMode, activateOnLoad, priority);
        }

        public AsyncOperationHandle<SceneInstance> UnloadSceneAsync(AsyncOperationHandle<SceneInstance> sceneLoadHandle, bool autoReleaseHandle = true)
        {
            return Addressables.UnloadSceneAsync(sceneLoadHandle, autoReleaseHandle);
        }
    }
}
