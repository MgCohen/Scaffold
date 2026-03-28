using Unity.GraphToolkit.Editor;
using UnityEditor;

namespace Scaffold.GraphFlow.Editor
{
    public static class GraphFlowCreateMenu
    {
        [MenuItem("Assets/Create/GraphFlow/Authoring Graph", false, 81)]
        static void CreateAuthoringGraph()
        {
            GraphDatabase.PromptInProjectBrowserToCreateNewAsset<GraphFlowAuthoringGraph>();
        }
    }
}
