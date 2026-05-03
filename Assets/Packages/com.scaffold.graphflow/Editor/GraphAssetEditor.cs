#nullable enable
using UnityEditor;

namespace Scaffold.GraphFlow.Editor
{
    /// <summary>
    /// Bypasses third-party global inspector overrides (NaughtyAttributes etc.) for any
    /// <see cref="GraphAsset{TRunner}"/> subclass. NaughtyAttributes' reflection walks into
    /// <c>[SerializeReference] List&lt;RuntimeNode&gt;</c> sub-properties and logs
    /// "target object is null" errors when traversing the polymorphic chain — falls back to
    /// Unity's default inspector here, which renders the same fields without the noise.
    /// </summary>
    [CustomEditor(typeof(GraphAsset<>), editorForChildClasses: true)]
    public sealed class GraphAssetEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI() => DrawDefaultInspector();
    }
}
