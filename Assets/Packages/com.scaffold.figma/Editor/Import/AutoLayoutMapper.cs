using Scaffold.Figma.Schema;
using UnityEngine;
using UnityEngine.UI;

namespace Scaffold.Figma.Editor.Import
{
    internal static class AutoLayoutMapper
    {
        internal static void Apply(FigmaNode node, GameObject go)
        {
            var mode = node.layoutMode;
            if (string.IsNullOrEmpty(mode) || mode == "NONE")
                return;

            LayoutGroup lg = null;
            if (mode == "HORIZONTAL")
            {
                var h = go.GetComponent<HorizontalLayoutGroup>();
                if (h == null) h = go.AddComponent<HorizontalLayoutGroup>();
                lg = h;
            }
            else if (mode == "VERTICAL")
            {
                var v = go.GetComponent<VerticalLayoutGroup>();
                if (v == null) v = go.AddComponent<VerticalLayoutGroup>();
                lg = v;
            }
            else
                return;

            lg.spacing = node.layoutSpacing;
            var pad = node.layoutPadding;
            if (pad != null)
            {
                lg.padding.left = Mathf.RoundToInt(pad.left);
                lg.padding.right = Mathf.RoundToInt(pad.right);
                lg.padding.top = Mathf.RoundToInt(pad.top);
                lg.padding.bottom = Mathf.RoundToInt(pad.bottom);
            }

            lg.childAlignment = TextAnchor.UpperLeft;
            lg.childForceExpandWidth = lg is HorizontalLayoutGroup;
            lg.childForceExpandHeight = lg is VerticalLayoutGroup;

            var fitter = go.GetComponent<ContentSizeFitter>();
            if (fitter == null) fitter = go.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
        }
    }
}
