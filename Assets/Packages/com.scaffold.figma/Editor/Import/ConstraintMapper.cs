using Scaffold.Figma.Schema;
using UnityEngine;

namespace Scaffold.Figma.Editor.Import
{
    internal static class ConstraintMapper
    {
        internal static void Apply(FigmaNode node, RectTransform rt, RectTransform parentRt)
        {
            var c = node.constraints;
            var hz = c != null && !string.IsNullOrEmpty(c.horizontal) ? c.horizontal : "CENTER";
            var vt = c != null && !string.IsNullOrEmpty(c.vertical) ? c.vertical : "CENTER";

            GetAnchorsPivots(hz, vt, out var anchorMin, out var anchorMax, out var pivot);

            var local = RectInParentUnitySpace(node, parentRt);
            var w = node.rect.width;
            var h = node.rect.height;
            var left = local.x;
            var bottom = local.y;

            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;

            if (anchorMin == anchorMax)
            {
                var pRect = parentRt != null ? parentRt.rect : new Rect(0, 0, 0, 0);
                var anchorWorld = new Vector2(
                    Mathf.Lerp(pRect.xMin, pRect.xMax, anchorMin.x),
                    Mathf.Lerp(pRect.yMin, pRect.yMax, anchorMin.y));
                var pivotParent = new Vector2(left + pivot.x * w, bottom + pivot.y * h);
                rt.anchoredPosition = pivotParent - anchorWorld;
                rt.sizeDelta = new Vector2(w, h);
                return;
            }

            var parentW = ParentWidthLocal(parentRt);
            var parentH = ParentHeightLocal(parentRt);
            float right = parentW - (left + w);
            float top = parentH - (bottom + h);

            var centerX = left + w * 0.5f + (right - left) * (pivot.x - 0.5f);
            var centerY = bottom + h * 0.5f + (top - bottom) * (pivot.y - 0.5f);
            rt.anchoredPosition = new Vector2(centerX, centerY);
            rt.sizeDelta = new Vector2(-(left + right), -(top + bottom));
        }

        static float ParentWidthLocal(RectTransform parentRt)
        {
            var src = parentRt != null ? parentRt.GetComponent<FigmaNodeRectSource>() : null;
            if (src != null)
                return src.FigmaRect.width;
            return parentRt != null ? parentRt.rect.width : 0f;
        }

        static float ParentHeightLocal(RectTransform parentRt)
        {
            var src = parentRt != null ? parentRt.GetComponent<FigmaNodeRectSource>() : null;
            if (src != null)
                return src.FigmaRect.height;
            return parentRt != null ? parentRt.rect.height : 0f;
        }

        /// <summary>
        /// Bottom-left corner of node's rect in parent local space (Unity y-up).
        /// </summary>
        static Vector2 RectInParentUnitySpace(FigmaNode node, RectTransform parentRt)
        {
            float lx = node.rect.x;
            float lyFromParentTop = node.rect.y;
            if (parentRt != null)
            {
                var p = parentRt.GetComponent<FigmaNodeRectSource>();
                if (p != null)
                {
                    lx -= p.FigmaRect.x;
                    lyFromParentTop -= p.FigmaRect.y;
                }
            }

            var ph = parentRt != null ? parentRt.GetComponent<FigmaNodeRectSource>()?.FigmaRect.height ?? 0f : 0f;
            var unityY = ph - lyFromParentTop - node.rect.height;
            return new Vector2(lx, unityY);
        }

        static void GetAnchorsPivots(string hz, string vt, out Vector2 anchorMin, out Vector2 anchorMax, out Vector2 pivot)
        {
            float axMin;
            float axMax;
            float px;
            switch (hz)
            {
                case "LEFT":
                    axMin = axMax = 0f;
                    px = 0f;
                    break;
                case "RIGHT":
                    axMin = axMax = 1f;
                    px = 1f;
                    break;
                case "LEFT_RIGHT":
                    axMin = 0f;
                    axMax = 1f;
                    px = 0.5f;
                    break;
                case "SCALE":
                    axMin = 0f;
                    axMax = 1f;
                    px = 0.5f;
                    break;
                default:
                    axMin = axMax = 0.5f;
                    px = 0.5f;
                    break;
            }

            float ayMin;
            float ayMax;
            float py;
            switch (vt)
            {
                case "TOP":
                    ayMin = ayMax = 1f;
                    py = 1f;
                    break;
                case "BOTTOM":
                    ayMin = ayMax = 0f;
                    py = 0f;
                    break;
                case "TOP_BOTTOM":
                    ayMin = 0f;
                    ayMax = 1f;
                    py = 0.5f;
                    break;
                case "SCALE":
                    ayMin = 0f;
                    ayMax = 1f;
                    py = 0.5f;
                    break;
                default:
                    ayMin = ayMax = 0.5f;
                    py = 0.5f;
                    break;
            }

            anchorMin = new Vector2(axMin, ayMin);
            anchorMax = new Vector2(axMax, ayMax);

            if (hz == "LEFT_RIGHT" || hz == "SCALE")
                px = 0.5f;
            if (vt == "TOP_BOTTOM" || vt == "SCALE")
                py = 0.5f;
            pivot = new Vector2(px, py);
        }
    }
}
