using NUnit.Framework;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using AAGen;

namespace AAGen.Tests
{
    public class DependencyGraphTests
    {
        const string k_TestFolderPath = "Assets/AAGenTests";

        [SetUp]
        public void Setup()
        {
            if (!AssetDatabase.IsValidFolder(k_TestFolderPath))
                AssetDatabase.CreateFolder("Assets", "AAGenTests");
        }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(k_TestFolderPath);
        }

        [Test]
        public void AddNode_UsingPath_AddsNodeToGraph()
        {
            var asset = CreateTestNode("node1.asset");
            var path = AssetDatabase.GetAssetPath(asset);

            var graph = new DependencyGraph();
            graph.AddNode(path);

            var node = AssetNode.FromAssetPath(path);
            var allNodes = graph.GetAllNodes();

            Assert.Contains(node, allNodes);
        }

        [Test]
        public void AddEdge_UsingPath_CreatesDirectedEdge()
        {
            var a = CreateTestNode("a.asset");
            var b = CreateTestNode("b.asset");

            var pathA = AssetDatabase.GetAssetPath(a);
            var pathB = AssetDatabase.GetAssetPath(b);

            var graph = new DependencyGraph();
            graph.AddEdge(pathA, pathB);

            var nodeA = AssetNode.FromAssetPath(pathA);
            var nodeB = AssetNode.FromAssetPath(pathB);

            var neighbors = graph.GetNeighbors(nodeA);
            CollectionAssert.Contains(neighbors, nodeB);
        }

        [Test]
        public void CountOutgoingEdges_ReturnsCorrectCount()
        {
            var a = CreateTestNode("a.asset");
            var b = CreateTestNode("b.asset");
            var c = CreateTestNode("c.asset");

            var pathA = AssetDatabase.GetAssetPath(a);
            var pathB = AssetDatabase.GetAssetPath(b);
            var pathC = AssetDatabase.GetAssetPath(c);

            var graph = new DependencyGraph();
            graph.AddEdge(pathA, pathB);
            graph.AddEdge(pathA, pathC);

            var nodeA = AssetNode.FromAssetPath(pathA);
            int count = graph.CountOutgoingEdges(nodeA);

            Assert.AreEqual(2, count);
        }

        [Test]
        public void SerializeAndDeserialize_PreservesGraphStructure()
        {
            var a = CreateTestNode("a.asset");
            var b = CreateTestNode("b.asset");

            var pathA = AssetDatabase.GetAssetPath(a);
            var pathB = AssetDatabase.GetAssetPath(b);

            var graph = new DependencyGraph();
            graph.AddEdge(pathA, pathB);

            var serialized = graph.Serialize();
            var deserialized = DependencyGraph.Deserialize(serialized);

            var nodeA = AssetNode.FromAssetPath(pathA);
            var nodeB = AssetNode.FromAssetPath(pathB);

            var neighbors = deserialized.GetNeighbors(nodeA);
            CollectionAssert.Contains(neighbors, nodeB);
        }

        ScriptableObject CreateTestNode(string fileName)
        {
            var instance = ScriptableObject.CreateInstance<TestNode>();
            var path = Path.Combine(k_TestFolderPath, fileName);
            AssetDatabase.CreateAsset(instance, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return instance;
        }
    }
}