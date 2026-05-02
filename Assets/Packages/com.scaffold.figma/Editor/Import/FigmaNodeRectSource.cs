using Scaffold.Figma.Schema;
using UnityEngine;

namespace Scaffold.Figma.Editor.Import
{
    /// <summary>
    /// Stores the node's Figma-space rect on the GameObject so children can compute parent-relative layout.
    /// </summary>
    internal sealed class FigmaNodeRectSource : MonoBehaviour
    {
        internal FigmaRect FigmaRect { get; private set; }

        internal void Init(FigmaRect r) => FigmaRect = r;
    }
}
