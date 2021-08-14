namespace AJut.Tree
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// How is tree iteration deciding where to go next
    /// </summary>
    public enum eTraversalFlowDirection
    {
        /// <summary>
        /// Through parent nodes
        /// </summary>
        ThroughParents,

        /// <summary>
        /// Through children
        /// </summary>
        ThroughChildren,

        /// <summary>
        /// Moves up the tree, but like a reverse of ThroughChildren - not just through parents.
        /// </summary>
        ReversedThroughChildren,
    }

    /// <summary>
    /// The strategy used to traverse the tree.
    /// Default is ThroughChildrenBreadthFirst
    /// All the options after ThroughParents/ThroughChildren are modifiers for ThroughChildren, and will be ignored for ThroughParents
    /// </summary>
    public enum eTraversalStrategy
    {
        /// <summary>
        /// A depth first search of children - 1000
        /// </summary>
        DepthFirst,

        /// <summary>
        /// A breadth first search of children - 0100
        /// </summary>
        BreadthFirst,

        Default = DepthFirst,
    };

    public delegate IEnumerable<TTreeNode> GetTreeNodeChildren<TTreeNode> (TTreeNode parent);

    public delegate TTreeNode GetTreeNodeParent<TTreeNode> (TTreeNode child);

    /// <summary>
    /// Helper with tracking user expectations of how far down the tree is allowed to be travelled
    /// </summary>
    public class LevelRestriction
    {
        /// <summary>
        /// The tree iteration depth that a node must happen on
        /// </summary>
        public int RequiredLevel { get; set; } = -1;

        /// <summary>
        /// Minimum: A tree iteration depth range
        /// </summary>
        public int LevelSearchMin { get; set; } = -1;

        /// <summary>
        /// Maximum: A tree iteration depth range
        /// </summary>
        public int LevelSearchMax { get; set; } = -1;

        /// <summary>
        /// Set this if you would like to allow the algorithm to exit the starting depth. The main case for this would be if you
        /// started a depth-first search at an arbitrary point down the tree, ie 3 levels down, but you want the search to continue
        /// even when the end of the sub-tree is found (default = false)
        /// </summary>
        public bool AllowsExitingStartDepth { get; set; } = false;

        public LevelRestriction () { }
        public LevelRestriction (int requiredLevel)
        {
            this.RequiredLevel = requiredLevel;
            this.LevelSearchMin = requiredLevel;
            this.LevelSearchMax = requiredLevel;
        }
        public LevelRestriction (int levelMin, int levelMax)
        {
            this.RequiredLevel = -1;
            this.LevelSearchMin = levelMin;
            this.LevelSearchMax = levelMax;
        }

        public bool Passes (int testLevel, bool updateRestrictions = false)
        {
            if (this.RequiredLevel == -1)
            {
                if (this.LevelSearchMin == -1 && this.LevelSearchMax == -1)
                {
                    return true;
                }

                if (this.LevelSearchMin <= testLevel && testLevel <= this.LevelSearchMax)
                {
                    if (updateRestrictions)
                    {
                        this.RequiredLevel = testLevel;
                    }
                    return true;
                }
                return false;
            }
            else
            {
                return this.RequiredLevel == testLevel;
            }
        }

        public bool EnforcesDepthLimits ()
        {
            return this.RequiredLevel != -1 && this.LevelSearchMin != -1 && this.LevelSearchMax != -1 && this.AllowsExitingStartDepth == false;
        }
    }

    /// <summary>
    /// Utility class for storing and evaluating type equivelence expectations
    /// </summary>
    public class TypeEval
    {
        /// <summary>
        /// The type we're testing against
        /// </summary>
        public Type SourceType { get; set; }

        /// <summary>
        /// Do we allow base types of the SourceType
        /// </summary>
        public bool AllowBaseTypes { get; set; }

        public TypeEval (Type type)
        {
            SourceType = type;
            AllowBaseTypes = false;
        }
        public TypeEval (Type type, bool allowsAncestors)
        {
            SourceType = type;
            AllowBaseTypes = allowsAncestors;
        }
        public bool Evaluate (Type other)
        {
            return this.AllowBaseTypes ? this.SourceType.IsAssignableFrom(other) : this.SourceType == other;
        }
    }

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
