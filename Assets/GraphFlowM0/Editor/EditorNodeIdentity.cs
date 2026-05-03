using Unity.GraphToolkit.Editor;

namespace Scaffold.GraphFlow.M0.Editor
{
    /// <summary>Stable editor node identity — Graph Toolkit stores this on each node model.</summary>
    static class EditorNodeIdentity
    {
        internal static string GetStableGuid(INode node)
        {
            dynamic d = node;
            return d.guid.ToString();
        }
    }
}
