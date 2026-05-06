using System;
using System.Reflection;
using Unity.GraphToolkit.Editor;

namespace Scaffold.GraphFlow.Editor
{
    /// <summary>Stable editor node identity — reads each <see cref="Node"/>&apos;s persisted Unity <c>Hash128</c> GUID from the backing toolkit model.</summary>
    /// <remarks>
    /// Graph Toolkit <c>0.4.x</c> does not expose a public GUID on <see cref="Node"/>; identity lives on the internal
    /// model base type (<c>Unity.GraphToolkit.Editor.Model.Guid</c>), reachable via non-public field <c>m_Implementation</c>.
    /// Reading <c>Guid</c> assigns a new stable value while the serialized <c>Hash128</c> is still default (new nodes).
    /// </remarks>
    static class EditorNodeIdentity
    {
        static readonly FieldInfo? s_NodeImplementation =
            typeof(Node).GetField("m_Implementation", BindingFlags.NonPublic | BindingFlags.Instance);

        internal static string? GetStableGuid(INode node)
        {
            if (node is not Node toolkitNode)
                return null;

            object? impl;
            try
            {
                impl = s_NodeImplementation?.GetValue(toolkitNode);
            }
            catch (Exception)
            {
                return null;
            }

            if (impl == null)
                return null;

            PropertyInfo? guidProp;
            try
            {
                guidProp = impl.GetType().GetProperty("Guid", BindingFlags.Public | BindingFlags.Instance);
            }
            catch (Exception)
            {
                return null;
            }

            if (guidProp == null)
                return null;

            object? hashGuid;
            try
            {
                hashGuid = guidProp.GetValue(impl);
            }
            catch (Exception)
            {
                return null;
            }

            var s = hashGuid?.ToString();
            return string.IsNullOrWhiteSpace(s) ? null : s;
        }
    }
}
