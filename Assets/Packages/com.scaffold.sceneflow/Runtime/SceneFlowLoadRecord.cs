using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;

namespace Scaffold.SceneFlow
{
    internal sealed class SceneFlowLoadRecord
    {
        public SceneFlowLoadRecord(AsyncOperationHandle<SceneInstance> sceneLoadHandle, bool manageBootstrapShell)
        {
            SceneLoadHandle = sceneLoadHandle;
            ManageBootstrapShell = manageBootstrapShell;
        }

        public AsyncOperationHandle<SceneInstance> SceneLoadHandle { get; }

        public bool ManageBootstrapShell { get; }
    }
}
