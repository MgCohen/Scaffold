using NUnit.Framework;
using Scaffold.Figma.Editor.Import;
using Scaffold.Figma.Schema;
using UnityEngine;

namespace Scaffold.Figma.Tests.Editor
{
    public sealed class MapperTests
    {
        [Test]
        public void ConstraintMapper_LeftTop_Pin_MatchesExpectedAnchoredPosition()
        {
            var parentGo = new GameObject("Parent");
            var parentRt = parentGo.AddComponent<RectTransform>();
            parentGo.AddComponent<FigmaNodeRectSource>().Init(new FigmaRect { x = 0, y = 0, width = 400, height = 600 });

            parentRt.sizeDelta = new Vector2(400, 600);

            var node = new FigmaNode
            {
                rect = new FigmaRect { x = 16, y = 8, width = 200, height = 32 },
                constraints = new FigmaConstraints { horizontal = "LEFT", vertical = "TOP" }
            };

            var childGo = new GameObject("Child");
            var childRt = childGo.AddComponent<RectTransform>();
            childRt.SetParent(parentRt, false);

            ConstraintMapper.Apply(node, childRt, parentRt);

            Assert.AreEqual(new Vector2(0f, 1f), childRt.anchorMin);
            Assert.AreEqual(new Vector2(0f, 1f), childRt.anchorMax);
            Assert.That(childRt.anchoredPosition.x, Is.EqualTo(16f).Within(0.05f));
            Assert.That(childRt.anchoredPosition.y, Is.EqualTo(-8f).Within(0.05f));
            Assert.AreEqual(new Vector2(200f, 32f), childRt.sizeDelta);

            Object.DestroyImmediate(parentGo);
        }
    }
}
