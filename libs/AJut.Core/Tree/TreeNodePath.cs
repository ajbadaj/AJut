namespace AJut.Tree
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Diagnostics;
    using System.Linq;

    /// <summary>
    /// A series of indicies, which, if followed from the same root, will result in a target node of a tree.
    /// </summary>
    public class TreeNodePath : ReadOnlyCollection<int>
    {
        private List<int> m_actualIndexList;

        // Publicly creatable construction paths

        /// <summary>
        /// Construct a <see cref="TreeNodePath"/>
        /// </summary>
        /// <param name="indicies">The path indicies that lead to the node in question</param>
        public TreeNodePath (params int[] indicies)
            : this(new List<int>(indicies))
        {
        }

        /// <summary>
        /// Construct a <see cref="TreeNodePath"/>
        /// </summary>
        /// <param name="indexEnumerable">The path indicies that lead to the node in question</param>
        public TreeNodePath (IEnumerable<int> indexEnumerable)
            : this(new List<int>(indexEnumerable))
        {
        }

        /// <summary>
        /// Construct a <see cref="TreeNodePath"/>
        /// </summary>
        /// <param name="indexList">The path indicies that lead to the node in question</param>
        public TreeNodePath (List<int> indexList) : base(indexList)
        {
            m_actualIndexList = indexList;
        }

        // Build up paths
        internal TreeNodePath () : this(new List<int>())
        {
        }
        internal TreeNodePath (TreeNodePath other) : this(new List<int>(other.m_actualIndexList))
        {
        }

        /// <summary>
        /// Checks if node paths are the same
        /// </summary>
        /// <param name="obj">The object to test, if it's an object will return base.Equals(obj)</param>
        /// <returns>Indicator if the node path is equal to another TreeNodePath or other object</returns>
        public override bool Equals (object obj)
        {
            if (obj is TreeNodePath path)
            {
                if (this.Count != path.Count)
                {
                    return false;
                }

                for (int index = 0; index < this.Count; ++index)
                {
                    if (this[index] != path[index])
                    {
                        return false;
                    }
                }

                return true;
            }

            return false;
        }

        public override int GetHashCode () => m_actualIndexList.Sum().GetHashCode();

        public override string ToString ()
        {
            return "TreeNodePath => " + String.Join(" ", m_actualIndexList.Select(_ => $"[{_}]"));
        }

        /// <summary>
        /// Extend the path by adding a new child node index to the end
        /// </summary>
        public void AddToPath (int childNodeIndex)
        {
            m_actualIndexList.Add(childNodeIndex);
        }

        /// <summary>
        /// Duplicates this path and extends the duplicate with a new child node index
        /// </summary>
        public TreeNodePath CopyAndAddToPath (int childNodeIndex)
        {
            TreeNodePath result = new TreeNodePath(this);
            result.AddToPath(childNodeIndex);
            return result;
        }

        /// <summary>
        /// Duplicates this path and takes off the last element
        /// </summary>
        public TreeNodePath CopyAndRemoveEnd ()
        {
            TreeNodePath result = new TreeNodePath(this);
            if (this.Count != 0)
            {
                result.m_actualIndexList.RemoveAt(this.Count - 1);
            }
            return result;
        }

        /// <summary>
        /// Duplciates this path, and replaces the end index with another child index (sibling path creation)
        /// </summary>
        public TreeNodePath CopyAndSwapEndForSibling (int siblingIndex)
        {
            TreeNodePath path = this.CopyAndRemoveEnd();
            path.AddToPath(siblingIndex);
            return path;
        }
    }
}
