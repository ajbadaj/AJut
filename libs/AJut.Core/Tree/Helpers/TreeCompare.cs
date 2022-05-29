namespace AJut.Tree
{
    using System;

    /// <summary>
    /// Utility class that aids in making <see cref="TreeNodePath"/> comparisons
    /// </summary>
    public static class TreeCompare
    {
        const int AreTheSame = 0;
        const int LeftBeforeRight = -1;
        const int RightBeforeLeft = 1;

        public static int CompareTreeNodePaths (TreeNodePath left, TreeNodePath right, eTraversalFlowDirection flowDirection = eTraversalFlowDirection.ThroughChildren, eTraversalStrategy traversalStrategy = eTraversalStrategy.Default)
        {
            switch (flowDirection)
            {
                case eTraversalFlowDirection.ThroughParents:
                    return CompareTreeNodePaths_ThroughParents(left, right);
                case eTraversalFlowDirection.ThroughChildren:
                    if (traversalStrategy == eTraversalStrategy.BreadthFirst)
                    {
                        return CompareTreeNodePaths_BreadthFirst(left, right);
                    }
                    return CompareTreeNodePaths_DepthFirst(left, right);

                case eTraversalFlowDirection.ReversedThroughChildren:
                    if (traversalStrategy == eTraversalStrategy.BreadthFirst)
                    {
                        return CompareTreeNodePaths_ReverseBreadthFirst(left, right);
                    }
                    return CompareTreeNodePaths_ReverseDepthFirst(left, right);
            }

            throw new InvalidOperationException("Traversal flow and/or strategy was invalid, flowDirection was casted to something invalid");
        }

        private static int CompareTreeNodePaths_ThroughParents (TreeNodePath left, TreeNodePath right)
        {
            throw new NotSupportedException("I'm just not sure about parent traversal right now...");
        }

        private static int CompareTreeNodePaths_DepthFirst (TreeNodePath left, TreeNodePath right)
        {
            for (int index = 0; index < left.Count; ++index)
            {
                // Looks like the left tree path goes deeper than the right, in that case, we can stop evaluating
                //  because the left will definitely happen first
                if (index >= right.Count)
                {
                    return LeftBeforeRight;
                }

                int diff = right[index] - left[index];
                if (diff < 0)
                {
                    return RightBeforeLeft;
                }
                if (diff > 0)
                {
                    return LeftBeforeRight;
                }
            }

            // If they matched for the entirety of the iteration up to this point, even if the right
            //  tree goes deeper, we visit a node before we evaluate it's children, so the left will 
            //  happen before the right
            if (right.Count > left.Count)
            {
                return LeftBeforeRight;
            }

            // Otherwise, they have the same number of elements and they were equal the entire time
            return AreTheSame;
        }

        private static int CompareTreeNodePaths_BreadthFirst (TreeNodePath left, TreeNodePath right)
        {
            // We travel the entire breadth before moving on to the next depth, therefore we can deduce
            //  some things if the path lengths are uneven.
            if (left.Count < right.Count)
            {
                return LeftBeforeRight;
            }
            else if (left.Count > right.Count)
            {
                return RightBeforeLeft;
            }

            // The path lengths are even
            int numElements = left.Count;
            for (int index = 0; index < numElements; ++index)
            {
                int diff = right[index] - left[index];
                if (diff < 0)
                {
                    return RightBeforeLeft;
                }
                if (diff > 0)
                {
                    return LeftBeforeRight;
                }
            }

            // Otherwise, they have the same number of elements and they were equal the entire time
            return AreTheSame;
        }

        private static int CompareTreeNodePaths_ReverseDepthFirst (TreeNodePath left, TreeNodePath right)
        {
            throw new NotImplementedException("Reverse methods not implemented yet");
        }

        private static int CompareTreeNodePaths_ReverseBreadthFirst (TreeNodePath left, TreeNodePath right)
        {
            throw new NotImplementedException("Reverse methods not implemented yet");
        }
    }
}
