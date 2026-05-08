using System;
using System.Collections.Generic;
using System.Reflection;
using Unity.GraphToolkit.Editor;

namespace Scaffold.GraphFlow.Editor
{
    /// <summary>Stable editor variable identity. Mirrors <see cref="EditorNodeIdentity"/> —
    /// reads the persisted Unity <c>Hash128</c> GUID from the IVariable's backing toolkit
    /// model via the non-public <c>m_Implementation</c> field. Falls back to a
    /// name-prefixed pseudo-id if reflection fails (stable across editor sessions but
    /// fragile under rename — flag if hit at scale).</summary>
    static class EditorVariableIdentity
    {
        static readonly Dictionary<Type, FieldInfo?> s_implFieldByType = new();

        internal static string? GetStableGuid(IVariable variable)
        {
            if (variable == null) return null;

            var type = variable.GetType();
            if (!s_implFieldByType.TryGetValue(type, out var implField))
                s_implFieldByType[type] = implField =
                    type.GetField("m_Implementation", BindingFlags.NonPublic | BindingFlags.Instance);

            if (implField != null)
            {
                try
                {
                    var impl = implField.GetValue(variable);
                    if (impl != null)
                    {
                        var guidProp = impl.GetType().GetProperty("Guid", BindingFlags.Public | BindingFlags.Instance);
                        var hashGuid = guidProp?.GetValue(impl)?.ToString();
                        if (!string.IsNullOrWhiteSpace(hashGuid)) return hashGuid;
                    }
                }
                catch (Exception)
                {
                    // Reflection drift — fall through to the name-based fallback.
                }
            }

            return string.IsNullOrEmpty(variable.Name) ? null : "name:" + variable.Name;
        }
    }
}
