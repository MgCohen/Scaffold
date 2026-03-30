using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using System.IO;
using AAGen;

namespace AAGen.Tests
{
    public class AssetNodeTests
    {
        private const string TestFolderPath = "Assets/AAGenTests"; //<--- ToDo: Add to test constants

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
        public void FromAssetPath_ReturnsValidAssetNode()
        {
            var asset = CreateDummyAsset("dummy1.asset");
            var path = AssetDatabase.GetAssetPath(asset);
            var node = AssetNode.FromAssetPath(path);

            Assert.IsNotNull(node);
            Assert.AreEqual(path, node.AssetPath);
            Assert.AreEqual(Path.GetFileName(path), node.FileName);
        }

        [Test]
        public void FromGuidString_ReturnsCorrectNode()
        {
            var asset = CreateDummyAsset("dummy2.asset");
            var path = AssetDatabase.GetAssetPath(asset);
            var guid = AssetDatabase.AssetPathToGUID(path);
            var node = AssetNode.FromGuidString(guid);

            Assert.IsNotNull(node);
            Assert.AreEqual(path, node.AssetPath);
        }

        [Test]
        public void AssetNode_Equals_ReturnsTrueForSameGuid()
        {
            var asset = CreateDummyAsset("dummy3.asset");
            var path = AssetDatabase.GetAssetPath(asset);
            var guid = AssetDatabase.AssetPathToGUID(path);
            var a = AssetNode.FromGuidString(guid);
            var b = AssetNode.FromGuidString(guid);

            Assert.IsTrue(a.Equals(b));
            Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
        }

        [Test]
        public void AssetNode_ToString_ReturnsGuidOnly()
        {
            var asset = CreateDummyAsset("dummy4.asset");
            var path = AssetDatabase.GetAssetPath(asset);
            var node = AssetNode.FromAssetPath(path);

            Assert.AreEqual(node.Guid.ToString(), node.ToString());
        }

        ScriptableObject CreateDummyAsset(string fileName)
        {
            var instance = ScriptableObject.CreateInstance<TestNode>();
            var path = Path.Combine(TestFolderPath, fileName);
            AssetDatabase.CreateAsset(instance, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return instance;
        }
    }
}