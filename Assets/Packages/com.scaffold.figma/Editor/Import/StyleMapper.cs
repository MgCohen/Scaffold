using Scaffold.Figma.Schema;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Scaffold.Figma.Editor.Import
{
    internal static class StyleMapper
    {
        const string WhiteSpriteResource = "Scaffold.Figma.WhitePixel";

        internal static void Apply(FigmaNode node, GameObject go)
        {
            ApplyFills(node, go);
            ApplyText(node, go);
        }

        static void ApplyFills(FigmaNode node, GameObject go)
        {
            if (node.fills == null || node.fills.Length == 0)
                return;

            for (var i = 0; i < node.fills.Length; i++)
            {
                var f = node.fills[i];
                if (f == null || f.type != "SOLID")
                    continue;

                var img = go.GetComponent<Image>();
                if (img == null) img = go.AddComponent<Image>();

                var sprite = Resources.Load<Sprite>(WhiteSpriteResource);
                if (sprite != null)
                    img.sprite = sprite;

                img.color = new Color(f.r, f.g, f.b, f.a);
                img.type = Image.Type.Simple;
                img.raycastTarget = false;
                return;
            }
        }

        static void ApplyText(FigmaNode node, GameObject go)
        {
            if (string.IsNullOrEmpty(node.text) || node.style == null)
                return;

            var tmp = go.GetComponent<TextMeshProUGUI>();
            if (tmp == null) tmp = go.AddComponent<TextMeshProUGUI>();

            tmp.text = node.text;
            tmp.fontSize = node.style.fontSize;
            tmp.raycastTarget = false;

            if (TMP_Settings.defaultFontAsset != null)
                tmp.font = TMP_Settings.defaultFontAsset;

            if (node.style.color != null)
            {
                var c = node.style.color;
                tmp.color = new Color(c.r, c.g, c.b, c.a);
            }

            tmp.alignment = MapAlignment(node.style.alignH, node.style.alignV);
        }

        static TextAlignmentOptions MapAlignment(string alignH, string alignV)
        {
            var h = string.IsNullOrEmpty(alignH) ? "LEFT" : alignH;
            var v = string.IsNullOrEmpty(alignV) ? "TOP" : alignV;

            if (h == "CENTER" && v == "CENTER") return TextAlignmentOptions.Center;
            if (h == "CENTER" && v == "TOP") return TextAlignmentOptions.Top;
            if (h == "CENTER" && v == "BOTTOM") return TextAlignmentOptions.Bottom;
            if (h == "RIGHT" && v == "TOP") return TextAlignmentOptions.TopRight;
            if (h == "RIGHT" && v == "CENTER") return TextAlignmentOptions.Right;
            if (h == "RIGHT" && v == "BOTTOM") return TextAlignmentOptions.BottomRight;
            if (h == "LEFT" && v == "BOTTOM") return TextAlignmentOptions.BottomLeft;
            if (h == "LEFT" && v == "CENTER") return TextAlignmentOptions.Left;

            return TextAlignmentOptions.TopLeft;
        }
    }
}
