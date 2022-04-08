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
