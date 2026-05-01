using UnityEditor;

namespace Scaffold.LiveOps.Editor
{
    /// <summary>Sample: menu entry that opens <see cref="LiveOpsBackendWindow"/>.</summary>
    internal static class LiveOpsBackendWindowMenu
    {
        private const string kMenuPath = "Scaffold/LiveOps/Backend Window";
        private const string kWindowTitle = "LiveOps Backend";

        [MenuItem(kMenuPath)]
        private static void CreateWindow()
        {
            _ = EditorWindow.GetWindow<LiveOpsBackendWindow>(kWindowTitle);
        }
    }
}
