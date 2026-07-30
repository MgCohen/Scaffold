using Scaffold.Figma.Schema;
using UnityEngine;
using UnityEngine.UI;

namespace Scaffold.Figma.Editor.Import
{
    internal static class NodeMapper
    {
        internal static GameObject CreateNode(FigmaNode node, RectTransform parentRt)
        {
            var go = new GameObject(string.IsNullOrEmpty(node.name) ? node.type ?? "Node" : node.name);
            var rt = go.AddComponent<RectTransform>();
            go.transform.SetParent(parentRt, worldPositionStays: false);

            var figmaRect = go.AddComponent<FigmaNodeRectSource>();
            figmaRect.Init(node.rect);

            ConstraintMapper.Apply(node, rt, parentRt);

            if (parentRt != null && ParentHasLayoutGroup(parentRt))
            {
                var le = go.GetComponent<LayoutElement>();
                if (le == null) le = go.AddComponent<LayoutElement>();
                le.preferredWidth = node.rect.width;
                le.preferredHeight = node.rect.height;
                le.minWidth = node.rect.width;
                le.minHeight = node.rect.height;

                if (parentRt.GetComponent<VerticalLayoutGroup>() != null &&
                    node.constraints != null && node.constraints.vertical == "TOP_BOTTOM")
                    le.flexibleHeight = 1f;
                if (parentRt.GetComponent<VerticalLayoutGroup>() != null &&
                    node.constraints != null && node.constraints.horizontal == "LEFT_RIGHT")
                    le.flexibleWidth = 1f;
                if (parentRt.GetComponent<HorizontalLayoutGroup>() != null &&
                    node.constraints != null && node.constraints.horizontal == "LEFT_RIGHT")
                    le.flexibleWidth = 1f;
            }

            AutoLayoutMapper.Apply(node, go);
            StyleMapper.Apply(node, go);

            if (node.children != null)
            {
                for (var i = 0; i < node.children.Length; i++)
                {
                    var ch = node.children[i];
                    if (ch != null)
                        CreateNode(ch, rt);
                }
            }

            return go;
        }

        static bool ParentHasLayoutGroup(RectTransform parentRt) =>
            parentRt != null &&
            (parentRt.GetComponent<HorizontalLayoutGroup>() != null ||
             parentRt.GetComponent<VerticalLayoutGroup>() != null);
    }
}
