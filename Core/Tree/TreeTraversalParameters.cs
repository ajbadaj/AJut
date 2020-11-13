namespace AJut.Tree
{
    using System;

    /// <summary>
    /// The parameters that govern how the tree traversal will operate
    /// </summary>
    public class TreeTraversalParameters<TTreeNode> where TTreeNode : class
    {
        private GetTreeNodeChildren<TTreeNode> m_getChildrenMethodOverride;
        private GetTreeNodeParent<TTreeNode> m_getParentMethodOverride;

        /// <summary>
        /// The acting root node for the tree, tree node path will be indicated based off of.
        /// </summary>
        public TTreeNode Root { get; internal set; }

        /// <summary>
        /// The travesal flow direction
        /// </summary>
        public eTraversalFlowDirection FlowDirection { get; set; }

        /// <summary>
        /// How should the tree be traversed
        /// </summary>
        public eTraversalStrategy ChildTraversalStrategy { get; set; }

        /// <summary>
        /// What type matching should be done? Used in "FirstChildOfType" type of evaluations.
        /// </summary>
        public TypeEval TypeMatching { get; set; }

        /// <summary>
        /// Predicate that determines if a node should be returned
        /// </summary>
        public Predicate<TTreeNode> Predicate { get; set; }

        /// <summary>
        /// A path that if reached stops iteration (default is null for none)
        /// </summary>
        public TreeNodePath TerminationNodePath { get; set; }

        /// <summary>
        /// After a node is found that passes all predicates/type matching/etc. should we stop evaluating children in that sub-tree?
        /// </summary>
        public bool PruneAfterFirstFind { get; set; }

        /// <summary>
        /// Stop searching the tree after this ammount of evaluations have been made (default = -1, which resolves to All)
        /// </summary>
        public int MaxNumberOfEvaluations { get; set; }

        /// <summary>
        /// Should the tree traversal track re-entrancy? This is moderately expensive, so only turn on for trees that can have circular references.
        /// </summary>
        public bool TrackReentrancy { get; set; }

        /// <summary>
        /// Tracks the limits on how deep (from start) a traversal can go in a tree before it should stop.
        /// </summary>
        public LevelRestriction DepthLimits { get; set; }

        /// <summary>
        /// If TypeMatching is set, then it is required to be checked.
        /// </summary>
        public bool RequiresTypeMatching => this.TypeMatching != null;

        /// <summary>
        /// Indicates if depth limits have been set
        /// </summary>
        public bool EnforcesLevelRestriction => this.DepthLimits?.EnforcesDepthLimits() ?? false;

        /// <summary>
        /// Indicates if there is a max number of evaluations to worry about.
        /// </summary>
        public bool HasEvaluationCap => this.MaxNumberOfEvaluations != -1;

        /// <summary>
        /// Indicates if there is a predicate to worry about.
        /// </summary>
        public bool HasPredicate => this.Predicate != null;

        /// <summary>
        /// An override to the <see cref="TreeTraversal{TTreeNode}"/> registered GetTreeNodeChildren method
        /// </summary>
        public GetTreeNodeChildren<TTreeNode> GetChildrenMethod
        {
            get
            {
                if (m_getChildrenMethodOverride != null)
                {
                    return m_getChildrenMethodOverride;
                }

                return TreeTraversal<TTreeNode>.GetChildrenMethod;
            }
        }

        /// <summary>
        /// An override to the <see cref="TreeTraversal{TTreeNode}"/> registered GetTreeNodeParent method
        /// </summary>
        public GetTreeNodeParent<TTreeNode> GetParentMethod
        {
            get
            {
                if (m_getParentMethodOverride != null)
                {
                    return m_getParentMethodOverride;
                }

                return TreeTraversal<TTreeNode>.GetParentMethod;
            }
        }

        internal void SetGetChildrenGetParentOverrideMethods (GetTreeNodeChildren<TTreeNode> getChildren, GetTreeNodeParent<TTreeNode> getParent)
        {
            m_getChildrenMethodOverride = getChildren;
            m_getParentMethodOverride = getParent;
        }

        /// <summary>
        /// Create parameters to the tree traversal
        /// </summary>
        /// <param name="root">The tree's root</param>
        /// <param name="flowDirection">The direction to traverse</param>
        /// <param name="strategy">The traversal strategy</param>
        /// <param name="getChildrenMethodOverride">The get children method to use (or null to use default)</param>
        /// <param name="getParentMethodOverride">The get parent method to use (or null to use default)</param>
        public TreeTraversalParameters (TTreeNode root, eTraversalFlowDirection flowDirection, eTraversalStrategy strategy, GetTreeNodeChildren<TTreeNode> getChildrenMethodOverride = null, GetTreeNodeParent<TTreeNode> getParentMethodOverride = null)
        {
            this.Root = root;
            this.FlowDirection = flowDirection;
            this.ChildTraversalStrategy = strategy;
            this.TypeMatching = null;
            this.Predicate = null;
            this.PruneAfterFirstFind = false;
            this.MaxNumberOfEvaluations = -1; // All
            this.TrackReentrancy = false;
            this.DepthLimits = null;

            m_getChildrenMethodOverride = getChildrenMethodOverride;
            m_getParentMethodOverride = getParentMethodOverride;
        }
    }
}
