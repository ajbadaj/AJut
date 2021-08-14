namespace AJut.Tree
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;

    /// <summary>
    /// A series of indicies, which, if followed from the same root, will result in a target node of a tree.
    /// </summary>
    public class TreeNodePath : ReadOnlyCollection<int>
    {
        private List<int> m_actualIndexList;

        // Publicly creatable construction paths
        public TreeNodePath (params int[] indicies)
            : this(new List<int>(indicies))
        {
        }

        public TreeNodePath (IEnumerable<int> indexEnumerable)
            : this(new List<int>(indexEnumerable))
        {
        }

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

        public override int GetHashCode ()
        {
            return base.GetHashCode();
        }

        public override string ToString ()
        {
            return "TreeNodePath => " + String.Join(" ", m_actualIndexList.Select(_ => $"[{_}]"));
        }

        public void AddToPath (int value)
        {
            m_actualIndexList.Add(value);
        }

        public TreeNodePath CopyAndAddToPath (int value)
        {
            TreeNodePath result = new TreeNodePath(this);
            result.AddToPath(value);
            return result;
        }

        public TreeNodePath CopyAndRemoveEnd ()
        {
            TreeNodePath result = new TreeNodePath(this);
            if (this.Count != 0)
            {
                result.m_actualIndexList.RemoveAt(this.Count - 1);
            }
            return result;
        }

        public TreeNodePath CopyAndSwapEndForSibling (int siblingIndex)
        {
            TreeNodePath path = this.CopyAndRemoveEnd();
            path.AddToPath(siblingIndex);
            return path;
        }

        public TreeNodePath CopyAndIncrementRecursively (int index, Func<int, int> maxForIndex, out int finalIndex)
        {
            // Copy
            TreeNodePath copy = new TreeNodePath(this);

            // Increment
            finalIndex = copy.IncrementRecursively(index, maxForIndex);

            if (finalIndex == -1)
            {
                return copy;
            }

            /* Zero out the other node path indicies, ie:
             * -----------------------------------------------
             * Path: [3][4][2] where max is 6/4/2 respectively and incremented at the last node path index results in:
             *       [4][0][0]
             *       
             *       [3][4][2] with max 6/10/20 incremented at the first node would still result in:
             *       [4][0][0] because why would you keep the [4][2]? presumably always start at the beginning.
             */
            int zeroOutIndex = finalIndex + 1;
            while (zeroOutIndex < m_actualIndexList.Count)
            {
                copy.m_actualIndexList[zeroOutIndex++] = 0;
            }

            return copy;
        }

        /// <summary>
        /// Increments the path at the index, if the item at the index can't be incrememnted, then it moves up the path recursively.
        /// </summary>
        /// <param name="index">The path index to increment</param>
        /// <param name="maxForIndex">A function that gets the max value for the path index</param>
        /// <returns>The index that was incremented, or -1 if none</returns>
        private int IncrementRecursively (int index, Func<int, int> maxForIndex)
        {
            if (index < 0)
            {
                return -1;
            }

            int max = maxForIndex(index);
            if (m_actualIndexList[index] + 1 >= max)
            {
                return this.IncrementRecursively(index - 1, maxForIndex);
            }

            ++m_actualIndexList[index];
            return index;
        }
    }
}
