using System.IO;
using NUnit.Framework;
using Scaffold.Figma.Schema;
using UnityEngine;

namespace Scaffold.Figma.Tests.Editor
{
    public sealed class SchemaTests
    {
        static string FixturePath => Path.Combine(Application.dataPath, "Packages/com.scaffold.figma/Tests/Fixtures/sample.figma.json");

        [Test]
        public void SampleJson_Deserializes_WithExpectedShape()
        {
            var json = File.ReadAllText(FixturePath);
            var doc = JsonUtility.FromJson<FigmaDocument>(json);
            Assert.NotNull(doc);
            Assert.AreEqual("Fixture Screen", doc.documentName);
            Assert.AreEqual(1, doc.nodes.Length);
            Assert.AreEqual(5, CountNodes(doc.nodes[0]));

            var screen = doc.nodes[0];
            Assert.AreEqual("Screen", screen.name);
            Assert.AreEqual("FRAME", screen.type);

            var header = screen.children[0];
            Assert.AreEqual("HeaderRow", header.name);
            Assert.NotNull(header.constraints);
            Assert.AreEqual("LEFT_RIGHT", header.constraints.horizontal);
            Assert.AreEqual("TOP", header.constraints.vertical);

            var title = header.children[0];
            Assert.AreEqual("TEXT", title.type);
            Assert.AreEqual("Hello Figma", title.text);
            Assert.NotNull(title.style);
            Assert.AreEqual(18f, title.style.fontSize, 0.001f);
            Assert.NotNull(title.style.color);
            Assert.AreEqual(0.1f, title.style.color.r, 0.001f);
        }

        static int CountNodes(FigmaNode n)
        {
            if (n == null)
                return 0;
            var c = 1;
            if (n.children == null)
                return c;
            for (var i = 0; i < n.children.Length; i++)
                c += CountNodes(n.children[i]);
            return c;
        }
    }
}
