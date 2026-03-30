using NUnit.Framework;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace AAGen.Tests
{
    public class AssetNodeGraphTests
    {
        const string k_TestFolderPath = "Assets/AAGenTests";

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(k_TestFolderPath))
                AssetDatabase.CreateFolder("Assets", "AAGenTests");
        }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(k_TestFolderPath);
        }

        [UnityTest]
        public IEnumerator CollapseToSCCs_WithCycleOfTestNodes_FindsOneSCC()
        {
            // Step 1: Create 3 nodes forming a cycle: A → B → C → A
            var a = CreateTestNode("A");
            var b = CreateTestNode("B");
            var c = CreateTestNode("C");

            a.ConnectTo(b);
            b.ConnectTo(c);
            c.ConnectTo(a);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            
            // Test flakiness due to non-determinism of AssetDatabase refresh timing
            // ToDo: Imperfect fix. Add a Mock AssetDatabase
            System.Threading.Thread.Sleep(50);
            yield return null;
            
            // Step 2: Build the graph
            var graph = new Graph<AssetNode>();
            var nodes = new[] { a, b, c };

            foreach (var testNode in nodes)
            {
                var assetPath = AssetDatabase.GetAssetPath(testNode);
                var node = AssetNode.FromAssetPath(assetPath);
                graph.AddNode(node);

                foreach (var neighbor in testNode.Neighbors)
                {
                    var neighborPath = AssetDatabase.GetAssetPath(neighbor);
                    var neighborNode = AssetNode.FromAssetPath(neighborPath);
                    graph.AddEdge(node, neighborNode);
                }
            }

            // Step 3: Collapse to SCCs
            var sccGraph = graph.CollapseToSCCs();
            var superNodes = sccGraph.GetAllNodes();

            // Expect one super node with 3 members
            bool foundSCC = superNodes.Any(sn => sn.Nodes.Count == 3);
            Assert.IsTrue(foundSCC, "Expected one SCC containing 3 nodes (A, B, C).");

            yield return null;
        }

        TestNode CreateTestNode(string name)
        {
            string assetPath = Path.Combine(k_TestFolderPath, $"{name}.asset");
            var instance = ScriptableObject.CreateInstance<TestNode>();
            AssetDatabase.CreateAsset(instance, assetPath);
            return instance;
        }
    }
}