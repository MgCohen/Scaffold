using NUnit.Framework;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using AAGen;

namespace AAGen.Tests
{
    public class SuperNodeTests
    {
        const string TestFolderPath = "Assets/AAGenTests";

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(TestFolderPath))
                AssetDatabase.CreateFolder("Assets", "AAGenTests");
        }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(TestFolderPath);
        }

        [Test]
        public void SuperNode_Equals_ReturnsTrueForSameAssetNodes()
        {
            var a = CreateAssetNode("a.asset");
            var b = CreateAssetNode("b.asset");

            var node1 = new SuperNode(new[] { a, b });
            var node2 = new SuperNode(new[] { b, a });

            Assert.IsTrue(node1.Equals(node2));
            Assert.AreEqual(node1.GetHashCode(), node2.GetHashCode());
        }

        [Test]
        public void SuperNode_Contains_ReturnsTrueForIncludedNode()
        {
            var a = CreateAssetNode("included.asset");
            var node = new SuperNode(new[] { a });

            Assert.IsTrue(node.Contains(a));
        }

        [Test]
        public void SuperNode_FromSingle_CreatesNodeWithOneAsset()
        {
            var a = CreateAssetNode("single.asset");
            var superNode = SuperNode.FromSingle(a);

            Assert.AreEqual(1, superNode.Nodes.Count);
            Assert.IsTrue(superNode.Contains(a));
        }

        [Test]
        public void SuperNode_ToString_ReturnsExpectedFormat()
        {
            var a = CreateAssetNode("a.asset");
            var b = CreateAssetNode("b.asset");
            var node = new SuperNode(new[] { a, b });

            var output = node.ToString();
            Assert.IsTrue(output.StartsWith("SuperNode["));
            Assert.IsTrue(output.Contains(a.Guid.ToString()));
            Assert.IsTrue(output.Contains(b.Guid.ToString()));
        }

        AssetNode CreateAssetNode(string fileName)
        {
            var instance = ScriptableObject.CreateInstance<TestNode>();
            var path = Path.Combine(TestFolderPath, fileName);
            AssetDatabase.CreateAsset(instance, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var guid = AssetDatabase.AssetPathToGUID(path);
            return AssetNode.FromGuidString(guid);
        }
    }
}