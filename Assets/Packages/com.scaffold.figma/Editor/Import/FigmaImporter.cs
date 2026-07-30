using System.IO;
using Scaffold.Figma.Schema;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace Scaffold.Figma.Editor.Import
{
    [ScriptedImporter(1, "figma.json")]
    public sealed class FigmaImporter : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext ctx)
        {
            var json = File.ReadAllText(ctx.assetPath);
            var doc = JsonUtility.FromJson<FigmaDocument>(json);
            if (doc == null)
            {
                ctx.LogImportError("Figma import failed: JSON deserialized to null.");
                return;
            }

            var rootName = string.IsNullOrEmpty(doc.documentName) ? "FigmaDocument" : doc.documentName;
            var root = new GameObject(rootName);
            var rootRt = root.AddComponent<RectTransform>();
            var bbox = UnionBBox(doc.nodes);
            rootRt.anchorMin = rootRt.anchorMax = new Vector2(0.5f, 0.5f);
            rootRt.pivot = new Vector2(0.5f, 0.5f);
            rootRt.sizeDelta = new Vector2(bbox.width, bbox.height);
            rootRt.anchoredPosition = Vector2.zero;

            var rootFigma = root.AddComponent<FigmaNodeRectSource>();
            rootFigma.Init(bbox);

            if (doc.nodes != null)
            {
                for (var i = 0; i < doc.nodes.Length; i++)
                {
                    var n = doc.nodes[i];
                    if (n != null)
                        NodeMapper.CreateNode(n, rootRt);
                }
            }

            ctx.AddObjectToAsset("root", root);
            ctx.SetMainObject(root);
        }

        static FigmaRect UnionBBox(FigmaNode[] nodes)
        {
            if (nodes == null || nodes.Length == 0)
                return new FigmaRect { x = 0, y = 0, width = 100f, height = 100f };

            var first = true;
            float minX = 0, minY = 0, maxX = 0, maxY = 0;
            foreach (var n in nodes)
            {
                if (n == null) continue;
                var r = n.rect;
                var nx2 = r.x + r.width;
                var ny2 = r.y + r.height;
                if (first)
                {
                    minX = r.x;
                    minY = r.y;
                    maxX = nx2;
                    maxY = ny2;
                    first = false;
                }
                else
                {
                    if (r.x < minX) minX = r.x;
                    if (r.y < minY) minY = r.y;
                    if (nx2 > maxX) maxX = nx2;
                    if (ny2 > maxY) maxY = ny2;
                }
            }

            if (first)
                return new FigmaRect { x = 0, y = 0, width = 100f, height = 100f };

            return new FigmaRect
            {
                x = minX,
                y = minY,
                width = maxX - minX,
                height = maxY - minY
            };
        }
    }
}
