using System;

namespace Scaffold.SceneFlow
{
    public readonly struct SceneFlowLoadResult
    {
        public SceneFlowLoadResult(Guid loadId, string sceneName, bool manageBootstrapShell)
        {
            LoadId = loadId;
            SceneName = sceneName ?? string.Empty;
            ManageBootstrapShell = manageBootstrapShell;
        }

        public Guid LoadId { get; }

        public string SceneName { get; }

        public bool ManageBootstrapShell { get; }
    }
}
