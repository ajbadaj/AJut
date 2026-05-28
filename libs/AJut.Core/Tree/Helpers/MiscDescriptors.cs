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
    /// Orthogonal to <see cref="eTraversalFlowDirection"/> - controls only the order siblings are
    /// visited at each level when traversing through children. Forward is the normal source order;
    /// Reversed walks siblings highest-source-index-first at every level (a layers-panel view).
    /// Node paths still address forward source indices.
    /// </summary>
    public enum eSiblingOrder
    {
        Forward,
        Reversed,
    }

    /// <summary>
    /// The strategy used to traverse the tree.
    /// Default is ThroughChildrenBreadthFirst
    /// All the options after ThroughParents/ThroughChildren are modifiers for ThroughChildren, and will be ignored for ThroughParents
    /// </summary>
    public enum eTraversalStrategy
    {
        /// <summary>
        /// A depth first search of children
        /// </summary>
        DepthFirst,

        /// <summary>
        /// A breadth first search of children
        /// </summary>
        BreadthFirst,

        /// <summary>
        /// Default is <see cref="DepthFirst"/>
        /// </summary>
        Default = DepthFirst,
    };

    /// <summary>
    /// A function that retrieves an enumerable set of children given a parent tree node
    /// </summary>
    public delegate IEnumerable<TTreeNode> GetTreeNodeChildren<TTreeNode> (TTreeNode parent);

    /// <summary>
    /// A function that retrieves a parent given a child tree node
    /// </summary>
    public delegate TTreeNode GetTreeNodeParent<TTreeNode> (TTreeNode child);
}
