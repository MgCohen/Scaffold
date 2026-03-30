using NUnit.Framework;
using System.Collections.Generic;

namespace AAGen.Tests
{
    public class GraphTests
    {
        Graph<string> m_Graph;

        [SetUp]
        public void Setup()
        {
            m_Graph = new Graph<string>();
            m_Graph.AddEdge("A", "B");
            m_Graph.AddEdge("A", "C");
            m_Graph.AddEdge("B", "D");
            m_Graph.AddEdge("C", "D");
            m_Graph.AddNode("E"); // Isolated node
        }
        
        [Test]
        public void TestAddNodeAndGetAllNodes()
        {
            var nodes = m_Graph.GetAllNodes();
            CollectionAssert.Contains(nodes, "A");
            CollectionAssert.Contains(nodes, "B");
            CollectionAssert.Contains(nodes, "C");
            CollectionAssert.Contains(nodes, "D");
            CollectionAssert.Contains(nodes, "E");
        }

        [Test]
        public void TestGetNeighbors()
        {
            var neighbors = m_Graph.GetNeighbors("A");
            CollectionAssert.AreEquivalent(new[] { "B", "C" }, neighbors);
        }

        [Test]
        public void TestGetTransposedGraph()
        {
            var transposed = m_Graph.GetTransposedGraph();
            CollectionAssert.Contains(transposed.GetNeighbors("B"), "A");
            CollectionAssert.Contains(transposed.GetNeighbors("C"), "A");
            CollectionAssert.Contains(transposed.GetNeighbors("D"), "B");
            CollectionAssert.Contains(transposed.GetNeighbors("D"), "C");
        }

        [Test]
        public void TestFindLeafNodes()
        {
            var leaves = m_Graph.FindLeafNodes("A");
            CollectionAssert.AreEquivalent(new[] { "D" }, leaves);
        }

        [Test]
        public void TestFindPathConeToLeaves()
        {
            var cone = m_Graph.FindPathConeToLeaves("A");
            CollectionAssert.AreEquivalent(new[] { "A", "B", "C", "D" }, cone);
        }

        [Test]
        public void TestFindPathAndLeaves()
        {
            m_Graph.FindPathAndLeaves("A", out var path, out var leaves);
            CollectionAssert.AreEquivalent(new[] { "A", "B", "C", "D" }, path);
            CollectionAssert.AreEquivalent(new[] { "D" }, leaves);
        }

        [Test]
        public void TestDepthFirstSearch()
        {
            var visited = new List<string>();
            m_Graph.DepthFirstSearch("A", visited.Add);
            CollectionAssert.AreEquivalent(new[] { "A", "B", "D", "C" }, visited);
        }

        [Test]
        public void TestDepthFirstSearchIterative()
        {
            var visited = new HashSet<string>();
            var order = new List<string>();
            m_Graph.DepthFirstSearchIterative("A", visited, order.Add);
            CollectionAssert.AreEquivalent(new[] { "A", "B", "D", "C" }, order);
        }
    }
}