using System;
using Unity.GraphToolkit.Editor;
using UnityEditor;

namespace Scaffold.GraphFlow.M0.Editor.GToolkit
{
    /// <summary>M0 smoke authoring graph — ExecPlan v2 vertical slice.</summary>
    [Serializable]
    [Graph(AssetExtension)]
    public sealed class MySmokeGraph : Graph
    {
        internal const string AssetExtension = "gfmsmoke";

        const string k_CreateMenuName = "GraphFlow M0 Smoke Graph";

        [MenuItem("Assets/Create/GraphFlow/M0 Smoke Graph")]
        static void CreateAssetFile()
        {
            GraphDatabase.PromptInProjectBrowserToCreateNewAsset<MySmokeGraph>(k_CreateMenuName);
        }
    }
}
