using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AAGen
{
    [CreateAssetMenu(menuName = "Create TestNode", fileName = "TestNode")]
    public class TestNode : ScriptableObject
    {
        public List<TestNode> Neighbors = new List<TestNode>();

#if UNITY_EDITOR
        [ContextMenu("Validate Node")]
        void Validate()
        {
            // Check for self-reference
            if (Neighbors.Contains(this))
                Debug.LogError($"{name} has a self-reference.");
        }
#endif

        public void ConnectTo(TestNode node)
        {
            if (node == null)
            {
                Debug.LogError($"node is null!");
                return;
            }

            if (Neighbors.Contains(node))
            {
                Debug.LogError($"Redundant edge: {name} is already connected to {node.name}");
                return;
            }

            Neighbors.Add(node);
        }
    }
}