using System;
using System.Collections.Generic;
using System.Linq;

namespace AAGen
{
    /// <summary>
    /// Represents a super node consisting of a set of <see cref="AssetNodes"/>.
    /// </summary>
    /// <remarks>Used for SCCs or grouped clusters in asset dependency graphs.</remarks>
    [Serializable]
    public class SuperNode : IEquatable<SuperNode>
    {
        #region Static Methods
        /// <summary>
        /// Creates a new instance of the <see cref="SuperNode"/> class from a single node.
        /// </summary>
        /// <param name="node">The single node.</param>
        /// <returns>A new instance of the <see cref="SuperNode"/> class.</returns>
        public static SuperNode FromSingle(AssetNode node) => new SuperNode(new[] { node });
        #endregion

        #region Fields
        /// <summary>
        /// A unigue set of <see cref="AssetNode"/> that the super node is comprised of.
        /// </summary>
        private readonly HashSet<AssetNode> _nodes;
        #endregion

        #region Properties
        /// <summary>
        /// Gets a unigue set of <see cref="AssetNode"/> that the super node is comprised of.
        /// </summary>
        public IReadOnlyCollection<AssetNode> Nodes => _nodes;
        #endregion

        #region Methods
        /// <summary>
        /// Creates a new instance of the <see cref="SuperNode"/> class.
        /// </summary>
        /// <param name="nodes">A collection of nodes.</param>
        public SuperNode(IEnumerable<AssetNode> nodes)
        {
            // If the nodes are valid, use them,
            // otherwise use an empty container as the basis for the unique set of nodes.
            _nodes = new HashSet<AssetNode>(nodes ?? Enumerable.Empty<AssetNode>());
        }

        /// <summary>
        /// Determines whether the specified super node is equal to the current.
        /// </summary>
        /// <param name="other">The super node to compare with the current.</param>
        /// <returns>A value indicating that specified super node is equal to the current.</returns>
        public bool Equals(SuperNode other)
        {
            // If the other super node is invalid, then:
            if (other == null)
            {
                // The values are not equivalent.
                return false;
            }

            // Otherwise, the other super node is valid.

            // Determine whether set of nodes are identical.
            return _nodes.SetEquals(other._nodes);
        }

        /// <summary>
        /// Determines whether the specified object is equal to the current object.
        /// </summary>
        /// <param name="obj">The object to compare with the current object.</param>
        /// <returns>A value indicating that specified object is equal to the current object.</returns>
        public override bool Equals(object obj)
        {
            // Ensure that the object is a SuperNode and that they are equivalent.
            return obj is SuperNode other && Equals(other);
        }

        /// <summary>
        /// Serves as the default hash function.
        /// </summary>
        /// <returns>A hash code for the current object.</returns>
        public override int GetHashCode()
        {
            // Order-independent hash based on XOR
            unchecked
            {
                // Initialize the hash with a positive non-zero seed.
                int hash = 17;

                // For each source node, perform the following:
                foreach (var node in _nodes.OrderBy(n => n.Guid.ToString()))
                {
                    // Use the hash value as the seed for this iteration to add variance.
                    // along with a prime number to add distribution qualities to avoid collision,
                    // and the hash code for the source node's asset GUID to select a unique bucket. 
                    hash = hash* 31 + node.GetHashCode();
                }

                // Return the hash.
                return hash;
            }
        }

        /// <summary>
        /// Formats a string that represents the current object.
        /// </summary>
        /// <returns>A string that represents the current object.</returns>
        public override string ToString()
        {
            // Use the nodes to format a string that represents the super node.
            return $"SuperNode[{string.Join(", ", _nodes)}]";
        }

        /// <summary>
        /// Determines whether an <see cref="AssetNode"/> is contained in the super node.
        /// </summary>
        /// <param name="node">The node in question.</param>
        /// <returns>A value indicating whether the node is contained in the super node.</returns>
        public bool Contains(AssetNode node) => _nodes.Contains(node);
        #endregion
    }
}