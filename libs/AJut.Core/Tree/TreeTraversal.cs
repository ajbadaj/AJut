namespace AJut.Tree
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /* Intended Usage:
     *      Tree data structures are actually, usually, described as a tree's node. At best a tree is just a root level tree node.
     *      Because of that, iteration and traversal of a tree are a bit tricky. These utilities are intended to help simplify, and
     *      unify the code needed to iterate and a traverse a tree. The TreeTraversalUtilties basically are just a way to call
     *      CreateIterator and make a TreeIter, but also contains functions for getting nodes directly, as well as wrappers for common
     *      iterator scenarios so that you don't have to take lots of time filling out the iterator's parameters.
     *      
     *      When doing so, you have two options. Let's say we have a BasicTreeNode class which has been registerd with TreeTraversalUtilities
     *      so that it's GetChildrenMethod & GetParentMethod are pre-setup. The two options are:
     *          1. TreeatraversalUtilities<BasicTreeNode>.CreateIterator(root, ...);
     *          2. var tree = TreeTraverser<BasicTreeNode>(root);
     *              tree.CreateIterator(...);
     *              
     *      The later is just an instance wrapping the former. The advantages of creating and returning a TreeTraverser are:
     *          1. Unified root reference, you don't have to keep passing it into every utility
     *          2. Storage at the instance level of GetChildren/GetParent which can be vital if you have a type that isn't statically able to 
     *              determine get children/get parent, or if like DependencyObject, you have a node which is part of two different trees.
     *              
     *      Note, at the end of each TreeTraversalUtilities function you have the ability to specify an override for GetChildren/GetParent, so
     *      using the TreeTraverser class is not the only ay to do that.
     */

    /// <summary>
    /// A set of utilities to help manage the complexities of tree traversal. User's only step to using is to call Setup (preferably in the node type's static constructor).
    /// </summary>
    /// <typeparam name="TTreeNode">The user defined tree node type</typeparam>
    public static class TreeTraversal<TTreeNode> where TTreeNode : class
    {
        // -------------------------------------------------
        // ----------[ Public Support Components ]----------
        // -------------------------------------------------

        public static GetTreeNodeParent<TTreeNode> GetParentMethod { get; private set; }
        public static GetTreeNodeChildren<TTreeNode> GetChildrenMethod { get; private set; }

        internal const eTraversalFlowDirection DefaultTraversalFlowDirection = eTraversalFlowDirection.ThroughChildren;
        internal const eTraversalStrategy DefaultTraversalStrategy = eTraversalStrategy.Default;

        public static void SetupDefaults (GetTreeNodeChildren<TTreeNode> getChildren, GetTreeNodeParent<TTreeNode> getParent = null)
        {
            GetChildrenMethod = getChildren ?? throw new ArgumentNullException("getChildren", "The default getChildren method cannot be null");
            if (getParent != null)
            {
                GetParentMethod = getParent;
            }
        }

        // -------------------------------------------------
        // ----------------[ Setup  ]-----------------------
        // -------------------------------------------------

        static TreeTraversal ()
        {
            // Default return null so we don't except, null parent is handled like end of the line, and will return iterator end
            GetParentMethod = _ => null;
        }

        // -------------------------------------------------
        // ---------------[ Utilities  ]--------------------
        // -------------------------------------------------

        /// <summary>
        /// Using the specified <see cref="GetTreeNodeParent{TTreeNode}"/> function, go up the parent tree until parent
        /// returns null, return the item before that.
        /// </summary>
        /// <param name="start">The starting place to look for the root element</param>
        /// <param name="getParentOverride">An override to the default registered GetParentMethod</param>
        /// <returns>The first item found whose parent is returned as null</returns>
        public static TTreeNode FindRoot (TTreeNode start, GetTreeNodeParent<TTreeNode> getParentOverride = null)
        {
            TTreeNode lastParent = start != null ? (getParentOverride ?? GetParentMethod)(start) : null;
            if (lastParent == null)
            {
                return start;
            }

            TTreeNode parent = null;
            for (; ; )
            {
                parent = lastParent != null ? (getParentOverride ?? GetParentMethod)(lastParent) : null;
                if (parent != null)
                {
                    lastParent = parent;
                }
                else
                {
                    return lastParent;
                }
            }
        }

        /// <summary>
        /// Creates a basic tree iterator
        /// </summary>
        /// <param name="start">The starting point (also the acting root of the tree)</param>
        /// <param name="strategy">The traversal strategy to use</param>
        /// <returns>The iterator that was created</returns>
        public static TreeIter<TTreeNode> CreateIterator (TTreeNode start, eTraversalFlowDirection flowDirection = DefaultTraversalFlowDirection, eTraversalStrategy strategy = DefaultTraversalStrategy, GetTreeNodeChildren<TTreeNode> getChildrenMethodOverride = null, GetTreeNodeParent<TTreeNode> getParentMethodOverride = null)
        {
            return CreateIterator(null, start, flowDirection, strategy, getChildrenMethodOverride, getParentMethodOverride);
        }

        /// <summary>
        /// Creates a basic tree iterator
        /// </summary>
        /// <param name="root">The tree root, will search from start up if not specified - if this is undesired, pass in start for root (or call overloaded function without root which uses start for root)</param>
        /// <param name="start">The starting point</param>
        /// <param name="strategy">The traversal strategy to use</param>
        /// <param name="flowDirection">How the traversal flows through the tree</param>
        /// <param name="getChildrenMethodOverride">The override to the default GetTreeNodeChildren method specified in <see cref="TreeTraversal{TTreeNode}"/>.SetupDefaults, or null to use the default</param>
        /// <param name="getParentMethodOverride">The override to the default GetTreeNodeParents method specified in <see cref="TreeTraversal{TTreeNode}"/>.SetupDefaults, or null to use the default</param>
        /// <returns>The iterator that was created</returns>
        public static TreeIter<TTreeNode> CreateIterator (TTreeNode root, TTreeNode start, eTraversalFlowDirection flowDirection = DefaultTraversalFlowDirection, eTraversalStrategy strategy = DefaultTraversalStrategy, GetTreeNodeChildren<TTreeNode> getChildrenMethodOverride = null, GetTreeNodeParent<TTreeNode> getParentMethodOverride = null)
        {
            return CreateIterator(start,
                new TreeTraversalParameters<TTreeNode>(root ?? FindRoot(start, getParentMethodOverride), flowDirection, strategy, getChildrenMethodOverride, getParentMethodOverride)
            );
        }

        /// <summary>
        /// Creates a customizable tree iterator
        /// </summary>
        /// <param name="start">The starting point</param>
        /// <param name="traversalParameters">The custom parameters used to control the traversal</param>
        /// <returns>The iterator that was created</returns>
        public static TreeIter<TTreeNode> CreateIterator (TTreeNode start, TreeTraversalParameters<TTreeNode> traversalParameters)
        {
            if (start == null)
            {
                return TreeIter<TTreeNode>.End;
            }

            return new TreeIter<TTreeNode>(start, traversalParameters);
        }

        public static IEnumerable<TTreeNode> All (TTreeNode start, eTraversalFlowDirection flowDirection = DefaultTraversalFlowDirection, eTraversalStrategy strategy = DefaultTraversalStrategy, bool includeSelf = true, GetTreeNodeChildren<TTreeNode> getChildrenMethodOverride = null, GetTreeNodeParent<TTreeNode> getParentMethodOverride = null)
        {
            return All(FindRoot(start), start, flowDirection, strategy, includeSelf, getChildrenMethodOverride, getParentMethodOverride);
        }
        public static IEnumerable<TTreeNode> All (TTreeNode root, TTreeNode start, eTraversalFlowDirection flowDirection = DefaultTraversalFlowDirection, eTraversalStrategy strategy = DefaultTraversalStrategy, bool includeSelf = true, GetTreeNodeChildren<TTreeNode> getChildrenMethodOverride = null, GetTreeNodeParent<TTreeNode> getParentMethodOverride = null)
        {
            TreeIter<TTreeNode> iter = CreateIterator(root, start, flowDirection, strategy, getChildrenMethodOverride, getParentMethodOverride);
            if (!includeSelf)
            {
                ++iter;
            }

            while (iter != TreeIter<TTreeNode>.End)
            {
                yield return iter.Node;
                ++iter;
            }
        }

        /// <summary>
        /// Creates an iterator for iterating over all nodes which pass a predicate
        /// </summary>
        /// <param name="start">The starting point</param>
        /// <param name="predicate">The predicate</param>
        /// <param name="flowDirection">The direction the traversal flows through the tree</param>
        /// <param name="strategy">The traversal strategy</param>
        /// <param name="includeSelf">Should the start node be included</param>
        /// <param name="depthLimits">Depth limits</param>
        /// <param name="getChildrenMethodOverride">The override to the default GetTreeNodeChildren method specified in <see cref="TreeTraversal{TTreeNode}"/>.SetupDefaults, or null to use the default</param>
        /// <param name="getParentMethodOverride">The override to the default GetTreeNodeParents method specified in <see cref="TreeTraversal{TTreeNode}"/>.SetupDefaults, or null to use the default</param>
        /// <returns>The iterator created</returns>
        public static TreeIter<TTreeNode> IterateOverAllNodesWhichPass (TTreeNode start, Predicate<TTreeNode> predicate, eTraversalFlowDirection flowDirection = DefaultTraversalFlowDirection, eTraversalStrategy strategy = DefaultTraversalStrategy, bool includeSelf = true, LevelRestriction depthLimits = null, GetTreeNodeChildren<TTreeNode> getChildrenMethodOverride = null, GetTreeNodeParent<TTreeNode> getParentMethodOverride = null)
        {
            return IterateOverAllNodesWhichPass(FindRoot(start, getParentMethodOverride), start, predicate, flowDirection, strategy, includeSelf, depthLimits, getChildrenMethodOverride, getParentMethodOverride);
        }

        /// <summary>
        /// Creates an iterator for iterating over all nodes which pass a predicate
        /// </summary>
        /// <param name="root">The tree's root node</param>
        /// <param name="start">The starting point</param>
        /// <param name="predicate">The predicate</param>
        /// <param name="flowDirection">The direction the traversal flows through the tree</param>
        /// <param name="strategy">The traversal strategy</param>
        /// <param name="includeSelf">Should the start node be included</param>
        /// <param name="depthLimits">Depth limits</param>
        /// <param name="getChildrenMethodOverride">The override to the default GetTreeNodeChildren method specified in <see cref="TreeTraversal{TTreeNode}"/>.SetupDefaults, or null to use the default</param>
        /// <param name="getParentMethodOverride">The override to the default GetTreeNodeParents method specified in <see cref="TreeTraversal{TTreeNode}"/>.SetupDefaults, or null to use the default</param>
        /// <returns>The iterator created</returns>
        public static TreeIter<TTreeNode> IterateOverAllNodesWhichPass (TTreeNode root, TTreeNode start, Predicate<TTreeNode> predicate, eTraversalFlowDirection flowDirection = DefaultTraversalFlowDirection, eTraversalStrategy strategy = DefaultTraversalStrategy, bool includeSelf = true, LevelRestriction depthLimits = null, GetTreeNodeChildren<TTreeNode> getChildrenMethodOverride = null, GetTreeNodeParent<TTreeNode> getParentMethodOverride = null)
        {
            var iter = CreateIterator(start, new TreeTraversalParameters<TTreeNode>(root ?? FindRoot(start, getParentMethodOverride), flowDirection, strategy, getChildrenMethodOverride, getParentMethodOverride)
            {
                Predicate = predicate,
                DepthLimits = depthLimits
            });

            return FinalizeAndReturnIter(iter, includeSelf, predicate);
        }

        /// <summary>
        /// Iterate over all the nodes of a particaulr type
        /// </summary>
        /// <typeparam name="T">Tree node type</typeparam>
        /// <param name="start">The starting point</param>
        /// <param name="predicate">The predicate</param>
        /// <param name="flowDirection">The direction the traversal flows through the tree</param>
        /// <param name="strategy">The traversal strategy</param>
        /// <param name="includeSelf">Should the start node be included</param>
        /// <param name="canTypeBeAncestor">Can the type be derivitive of the passed in type, or must it be an exact match (default is true)</param>
        /// <param name="depthLimits">Depth limits</param>
        /// <param name="getChildrenMethodOverride">The override to the default GetTreeNodeChildren method specified in <see cref="TreeTraversal{TTreeNode}"/>.SetupDefaults, or null to use the default</param>
        /// <param name="getParentMethodOverride">The override to the default GetTreeNodeParents method specified in <see cref="TreeTraversal{TTreeNode}"/>.SetupDefaults, or null to use the default</param>
        /// <returns></returns>
        public static TreeIter<TTreeNode> IterateOverNodesOfType<T> (TTreeNode start, Predicate<T> predicate = null, eTraversalFlowDirection flowDirection = DefaultTraversalFlowDirection, eTraversalStrategy strategy = DefaultTraversalStrategy, bool includeSelf = true, bool canTypeBeAncestor = true, LevelRestriction depthLimits = null, GetTreeNodeChildren<TTreeNode> getChildrenMethodOverride = null, GetTreeNodeParent<TTreeNode> getParentMethodOverride = null)
            where T : class
        {
            return IterateOverNodesOfType<T>(start, start, predicate, flowDirection, strategy, includeSelf, canTypeBeAncestor, depthLimits, getChildrenMethodOverride, getParentMethodOverride);
        }

        /// <summary>
        /// Iterate over all the nodes of a particaulr type
        /// </summary>
        /// <typeparam name="T">Tree node type</typeparam>
        /// <param name="root">The tree's root node</param>
        /// <param name="start">The starting point</param>
        /// <param name="predicate">The predicate</param>
        /// <param name="flowDirection">The direction the traversal flows through the tree</param>
        /// <param name="strategy">The traversal strategy</param>
        /// <param name="includeSelf">Should the start node be included</param>
        /// <param name="canTypeBeAncestor">Can the type be derivitive of the passed in type, or must it be an exact match (default is true)</param>
        /// <param name="depthLimits">Depth limits</param>
        /// <param name="getChildrenMethodOverride">The override to the default GetTreeNodeChildren method specified in <see cref="TreeTraversal{TTreeNode}"/>.SetupDefaults, or null to use the default</param>
        /// <param name="getParentMethodOverride">The override to the default GetTreeNodeParents method specified in <see cref="TreeTraversal{TTreeNode}"/>.SetupDefaults, or null to use the default</param>
        /// <returns></returns>
        public static TreeIter<TTreeNode> IterateOverNodesOfType<T> (TTreeNode root, TTreeNode start, Predicate<T> predicate = null, eTraversalFlowDirection flowDirection = DefaultTraversalFlowDirection, eTraversalStrategy strategy = DefaultTraversalStrategy, bool includeSelf = true, bool canTypeBeAncestor = true, LevelRestriction depthLimits = null, GetTreeNodeChildren<TTreeNode> getChildrenMethodOverride = null, GetTreeNodeParent<TTreeNode> getParentMethodOverride = null)
            where T : class
        {
            var traversalParams = new TreeTraversalParameters<TTreeNode>(root ?? FindRoot(start, getParentMethodOverride), flowDirection, strategy, getChildrenMethodOverride, getParentMethodOverride)
            {
                TypeMatching = new TypeEval(typeof(T), canTypeBeAncestor),
                PruneAfterFirstFind = false,
                DepthLimits = depthLimits,
            };

            if (predicate != null)
            {
                traversalParams.Predicate = (_obj) => predicate(_obj as T);
            }

            var iter = CreateIterator(start, traversalParams);
            return FinalizeAndReturnIter(iter, includeSelf, traversalParams.Predicate);
        }

        public static TreeIter<TTreeNode> IteratorAt (TTreeNode root, TreeNodePath nodePath, Predicate<TTreeNode> predicate = null, eTraversalFlowDirection flowDirection = DefaultTraversalFlowDirection, eTraversalStrategy strategy = DefaultTraversalStrategy, bool includeSelf = true, LevelRestriction depthLimits = null, GetTreeNodeChildren<TTreeNode> getChildrenMethodOverride = null, GetTreeNodeParent<TTreeNode> getParentMethodOverride = null)
        {
            var traversalParams = new TreeTraversalParameters<TTreeNode>(root, flowDirection, strategy, getChildrenMethodOverride, getParentMethodOverride)
            {
                Predicate = predicate,
                DepthLimits = depthLimits,
            };

            var iter = TreeIter<TTreeNode>.CreateIteratorAtNodePath(root, nodePath, traversalParams);
            return FinalizeAndReturnIter(iter, includeSelf, traversalParams.Predicate, start: false);
        }

        public static TTreeNode GetFirstChildWhichPasses (TTreeNode start, Predicate<TTreeNode> predicate, eTraversalFlowDirection flowDirection = DefaultTraversalFlowDirection, eTraversalStrategy strategy = DefaultTraversalStrategy, bool includeSelf = true, LevelRestriction depthLimits = null, GetTreeNodeChildren<TTreeNode> getChildrenMethodOverride = null, GetTreeNodeParent<TTreeNode> getParentMethodOverride = null)
        {
            return GetFirstChildWhichPasses(null, start, predicate, flowDirection, strategy, includeSelf, depthLimits, getChildrenMethodOverride, getParentMethodOverride);
        }
        public static TTreeNode GetFirstChildWhichPasses (TTreeNode root, TTreeNode start, Predicate<TTreeNode> predicate, eTraversalFlowDirection flowDirection = DefaultTraversalFlowDirection, eTraversalStrategy strategy = DefaultTraversalStrategy, bool includeSelf = true, LevelRestriction depthLimits = null, GetTreeNodeChildren<TTreeNode> getChildrenMethodOverride = null, GetTreeNodeParent<TTreeNode> getParentMethodOverride = null)
        {
            var iter = IterateOverAllNodesWhichPass(root, start, predicate, flowDirection, strategy, includeSelf, depthLimits, getChildrenMethodOverride, getParentMethodOverride);
            if (iter != TreeIter<TTreeNode>.End)
            {
                return iter.Node;
            }

            return null;
        }

        public static T GetFirstChildOfType<T> (TTreeNode start, Predicate<T> predicate = null, eTraversalFlowDirection flowDirection = DefaultTraversalFlowDirection, eTraversalStrategy strategy = DefaultTraversalStrategy, bool includeSelf = true, bool canTypeBeAncestor = true, LevelRestriction depthLimits = null, GetTreeNodeChildren<TTreeNode> getChildrenMethodOverride = null, GetTreeNodeParent<TTreeNode> getParentMethodOverride = null)
            where T : class
        {
            return GetFirstChildOfType<T>(null, start, predicate, flowDirection, strategy, includeSelf, canTypeBeAncestor, depthLimits, getChildrenMethodOverride, getParentMethodOverride);
        }

        public static T GetFirstChildOfType<T> (TTreeNode root, TTreeNode start, Predicate<T> predicate = null, eTraversalFlowDirection flowDirection = DefaultTraversalFlowDirection, eTraversalStrategy strategy = DefaultTraversalStrategy, bool includeSelf = true, bool canTypeBeAncestor = true, LevelRestriction depthLimits = null, GetTreeNodeChildren<TTreeNode> getChildrenMethodOverride = null, GetTreeNodeParent<TTreeNode> getParentMethodOverride = null)
            where T : class
        {
            var iter = IterateOverNodesOfType(root, start, predicate, flowDirection, strategy, includeSelf, canTypeBeAncestor, depthLimits, getChildrenMethodOverride, getParentMethodOverride);
            if (iter != TreeIter<TTreeNode>.End)
            {
                return iter.Node as T;
            }

            return null;
        }


        public static TTreeNode GetFirstParentWhichPasses (TTreeNode start, Predicate<TTreeNode> predicate, bool includeSelf = true, LevelRestriction depthLimits = null, GetTreeNodeChildren<TTreeNode> getChildrenMethodOverride = null, GetTreeNodeParent<TTreeNode> getParentMethodOverride = null)
        {
            return GetFirstParentWhichPasses(null, start, predicate, includeSelf, depthLimits, getChildrenMethodOverride, getParentMethodOverride);
        }
        public static TTreeNode GetFirstParentWhichPasses (TTreeNode root, TTreeNode start, Predicate<TTreeNode> predicate, bool includeSelf = true, LevelRestriction depthLimits = null, GetTreeNodeChildren<TTreeNode> getChildrenMethodOverride = null, GetTreeNodeParent<TTreeNode> getParentMethodOverride = null)
        {
            var iter = IterateOverAllNodesWhichPass(root, start, predicate, eTraversalFlowDirection.ThroughParents, eTraversalStrategy.Default, includeSelf, depthLimits, getChildrenMethodOverride, getParentMethodOverride);
            if (iter != TreeIter<TTreeNode>.End)
            {
                return iter.Node;
            }

            return null;
        }

        public static T GetFirstParentOfType<T> (TTreeNode start, Predicate<T> predicate = null, bool includeSelf = true, bool canTypeBeAncestor = true, LevelRestriction depthLimits = null, GetTreeNodeChildren<TTreeNode> getChildrenMethodOverride = null, GetTreeNodeParent<TTreeNode> getParentMethodOverride = null)
            where T : class, TTreeNode
        {
            return GetFirstParentOfType<T>(null, start, predicate, includeSelf, canTypeBeAncestor, depthLimits, getChildrenMethodOverride, getParentMethodOverride);
        }

        public static T GetFirstParentOfType<T> (TTreeNode root, TTreeNode start, Predicate<T> predicate = null, bool includeSelf = true, bool canTypeBeAncestor = true, LevelRestriction depthLimits = null, GetTreeNodeChildren<TTreeNode> getChildrenMethodOverride = null, GetTreeNodeParent<TTreeNode> getParentMethodOverride = null)
            where T : class, TTreeNode
        {
            var iter = IterateOverNodesOfType<T>(root, start, predicate, eTraversalFlowDirection.ThroughParents, eTraversalStrategy.Default, includeSelf, canTypeBeAncestor, depthLimits, getChildrenMethodOverride, getParentMethodOverride);
            if (iter != TreeIter<TTreeNode>.End)
            {
                return (T)iter.Node;
            }

            return null;
        }

        public static TTreeNode NodeAt (TTreeNode start, TreeNodePath nodePath, GetTreeNodeChildren<TTreeNode> getChildrenMethodOverride = null, GetTreeNodeParent<TTreeNode> getParentMethodOverride = null)
        {
            GetTreeNodeChildren<TTreeNode> getChildren = getChildrenMethodOverride ?? GetChildrenMethod;
            TTreeNode eval = start;
            foreach (int ind in nodePath)
            {
                eval = getChildren(eval).ElementAtOrDefault(ind);
                if (eval == null)
                {
                    return null;
                }
            }

            return eval;
        }


        public static TTreeNode ParentNodeFromPath (TTreeNode root, TreeNodePath nodePath, GetTreeNodeChildren<TTreeNode> getChildrenMethodOverride = null, GetTreeNodeParent<TTreeNode> getParentMethodOverride = null)
        {
            GetTreeNodeChildren<TTreeNode> getChildren = getChildrenMethodOverride ?? GetChildrenMethod;
            TTreeNode eval = root;
            for (int pathIndex = 0; pathIndex < nodePath.Count - 1; ++pathIndex)
            {
                eval = getChildren(eval).ElementAtOrDefault(nodePath[pathIndex]);
                if (eval == null)
                {
                    return null;
                }
            }

            return eval;
        }

        public static TTreeNode FindNextSiblingOrCousin (TTreeNode start, GetTreeNodeChildren<TTreeNode> getChildrenMethodOverride = null, GetTreeNodeParent<TTreeNode> getParentMethodOverride = null)
        {
            return FindNextSiblingOrCousin(null, start, getChildrenMethodOverride, getParentMethodOverride);
        }
        public static TTreeNode FindNextSiblingOrCousin (TTreeNode root, TTreeNode start, GetTreeNodeChildren<TTreeNode> getChildrenMethodOverride = null, GetTreeNodeParent<TTreeNode> getParentMethodOverride = null)
        {
            root = root ?? FindRoot(start, getParentMethodOverride);
            var getChildren = getChildrenMethodOverride ?? GetChildrenMethod;
            var getParent = getParentMethodOverride ?? GetParentMethod;
            return _DoFindNextSiblingOrCousin(start);

            TTreeNode _DoFindNextSiblingOrCousin (TTreeNode eval)
            {
                if (eval == root)
                {
                    return null;
                }

                var parent = getParent(eval);
                var children = getChildren(parent).SkipWhile(c => c != eval).Skip(1);
                if (children.Any())
                {
                    return children.First();
                }
                else
                {
                    return _DoFindNextSiblingOrCousin(parent);
                }
            }
        }

        public static int CountAllDescendants (TTreeNode node, GetTreeNodeChildren<TTreeNode> getChildrenMethodOverride = null)
        {
            GetTreeNodeChildren<TTreeNode> getChildren = getChildrenMethodOverride ?? TreeTraversal<TTreeNode>.GetChildrenMethod;
            return _RecursiveCountDescendants(node);

            int _RecursiveCountDescendants (TTreeNode _eval)
            {
                // Get or create list version of children
                int _sum = 0;
                foreach (var _c in getChildren(_eval))
                {
                    _sum += 1 + _RecursiveCountDescendants(_c);
                }
                return _sum;
            }
        }

        // -------------------------------------------------
        // -----------[ Private Utilities  ]----------------
        // -------------------------------------------------

        private static TreeIter<TTreeNode> FinalizeAndReturnIter(TreeIter<TTreeNode> iter, bool includeSelf, Predicate<TTreeNode> predicate, bool start = true)
        {
            if (iter == TreeIter<TTreeNode>.End)
            {
                return iter;
            }

            if (includeSelf && predicate != null && predicate(iter.Node))
            {
                return iter;
            }

            if (start)
            {
                return ++iter;
            }
            else
            {
                return iter;
            }
        }
    }

    /// <summary>
    /// A tree data structure that encapsualtes the <see cref="TreeTraversal{TTreeNode}"/> utilities
    /// and localizes them to an instance - allowing you to store a root to operate on rather than continually
    /// passing it to the TreeTraverser utilities, as well as local overrides to the GetChildren/GetParent which
    /// can not be done any other way.
    /// </summary>
    /// <typeparam name="TTreeNode">The tree element node type</typeparam>
    public class TreeTraverser<TTreeNode> where TTreeNode : class
    {
        /// <summary>
        /// The root node of the tree
        /// </summary>
        public TTreeNode Root { get; private set; }

        /// <summary>
        /// A local override to the default TreeTraverser registered GetChildren
        /// </summary>
        public GetTreeNodeChildren<TTreeNode> GetChildrenOverride { get; set; }

        /// <summary>
        /// A local override to the default TreeTraverser registered GetParent
        /// </summary>
        public GetTreeNodeParent<TTreeNode> GetParentOverride { get; set; }

        /// <summary>
        /// Tree constructor
        /// </summary>
        /// <param name="root">The tree root node</param>
        /// <param name="getChildrenOverride">A local override to the default TreeTraverser registered GetChildren</param>
        /// <param name="getParentOverride">A local override to the default TreeTraverser registered GetParent</param>
        public TreeTraverser(TTreeNode root, GetTreeNodeChildren<TTreeNode> getChildrenOverride = null, GetTreeNodeParent<TTreeNode> getParentOverride = null)
        {
            this.Root = root;
            this.GetChildrenOverride = getChildrenOverride;
            this.GetParentOverride = getParentOverride;
        }


        // -----------------------------------------------------------------------------------------------------------------------------------------------------
        // =====================================[ Public Instance Functions of TreeTraverser Utilities ] =======================================================
        // -----------------------------------------------------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Creates a basic tree iterator
        /// </summary>
        /// <param name="startNode">The node of the tree to start at (or null to start at the root)</param>
        /// <param name="flowDirection">The direction to flow through the tree</param>
        /// <param name="strategy">The traversal strategy to use</param>
        /// <returns>The iterator that was created</returns>
        public TreeIter<TTreeNode> CreateIterator(TTreeNode startNode = null, eTraversalFlowDirection flowDirection = TreeTraversal<TTreeNode>.DefaultTraversalFlowDirection, eTraversalStrategy strategy = TreeTraversal<TTreeNode>.DefaultTraversalStrategy)
        {
            return TreeTraversal<TTreeNode>.CreateIterator(this.Root, startNode, flowDirection, strategy, this.GetChildrenOverride, this.GetParentOverride);
        }

        /// <summary>
        /// Creates a customizable tree iterator
        /// </summary>
        /// <param name="traversalParameters">The custom parameters used to control the traversal</param>
        /// <returns>The iterator that was created</returns>
        public TreeIter<TTreeNode> CreateIterator(TreeTraversalParameters<TTreeNode> traversalParameters)
        {
            return this.CreateIterator(this.Root, traversalParameters);
        }

        /// <summary>
        /// Creates a customizable tree iterator
        /// </summary>
        /// <param name="startNode">The node of the tree to start at, or null to start at the root</param>
        /// <param name="traversalParameters">The custom parameters used to control the traversal</param>
        /// <returns>The iterator that was created</returns>
        public TreeIter<TTreeNode> CreateIterator(TTreeNode startNode, TreeTraversalParameters<TTreeNode> traversalParameters)
        {
            traversalParameters.Root = this.Root;
            traversalParameters.SetGetChildrenGetParentOverrideMethods(this.GetChildrenOverride, this.GetParentOverride);
            return TreeTraversal<TTreeNode>.CreateIterator(startNode ?? this.Root, traversalParameters);
        }

        /// <summary>
        /// Returns an enumerable of all nodes in the tree
        /// </summary>
        /// <param name="flowDirection">The traversal direction</param>
        /// <param name="strategy">The traversal strategy (BFS or DFS)</param>
        /// <param name="includeRoot">Should the root node be included</param>
        /// <returns></returns>
        public IEnumerable<TTreeNode> All(eTraversalFlowDirection flowDirection = TreeTraversal<TTreeNode>.DefaultTraversalFlowDirection, eTraversalStrategy strategy = TreeTraversal<TTreeNode>.DefaultTraversalStrategy, bool includeRoot = true)
        {
            return this.All(this.Root, flowDirection, strategy, includeRoot);
        }

        /// <summary>
        /// Returns an enumerable of all nodes in the tree
        /// </summary>
        /// <param name="startNode">The node on the tree to start at</param>
        /// <param name="flowDirection">The traversal direction</param>
        /// <param name="strategy">The traversal strategy (BFS or DFS)</param>
        /// <param name="includeRoot">Should the root node be included</param>
        /// <returns></returns>
        public IEnumerable<TTreeNode> All(TTreeNode startNode, eTraversalFlowDirection flowDirection = TreeTraversal<TTreeNode>.DefaultTraversalFlowDirection, eTraversalStrategy strategy = TreeTraversal<TTreeNode>.DefaultTraversalStrategy, bool includeRoot = true)
        {
            return TreeTraversal<TTreeNode>.All(startNode ?? this.Root, flowDirection, strategy, includeRoot, this.GetChildrenOverride, this.GetParentOverride);
        }

        // ----------------------------------------- TODO HERE -------------------------------------------- //
        // ----------------------------------------- TODO HERE -------------------------------------------- //
        // ----------------------------------------- TODO HERE -------------------------------------------- //
        // ----------------------------------------- TODO HERE -------------------------------------------- //
        // ----------------------------------------- TODO HERE -------------------------------------------- //
        // ----------------------------------------- TODO HERE -------------------------------------------- //

        /// <summary>
        /// Creates an iterator for iterating over all nodes which pass a predicate
        /// </summary>
        /// <param name="predicate">The predicate</param>
        /// <param name="flowDirection">The traversal direction</param>
        /// <param name="strategy">The traversal strategy</param>
        /// <param name="includeRoot">Should the root node be included</param>
        /// <param name="depthLimits">Depth limits</param>
        /// <returns>The iterator created</returns>
        public TreeIter<TTreeNode> IterateOverAllNodesWhichPass(Predicate<TTreeNode> predicate, eTraversalFlowDirection flowDirection = TreeTraversal<TTreeNode>.DefaultTraversalFlowDirection, eTraversalStrategy strategy = TreeTraversal<TTreeNode>.DefaultTraversalStrategy, bool includeRoot = true, LevelRestriction depthLimits = null)
        {
            return TreeTraversal<TTreeNode>.IterateOverAllNodesWhichPass(this.Root, this.Root, predicate, flowDirection, strategy, includeRoot, depthLimits, this.GetChildrenOverride, this.GetParentOverride);
        }

        /// <summary>
        /// Creates an iterator for iterating over all nodes which pass a predicate
        /// </summary>
        /// <param name="start">The starting point, if other than the root</param>
        /// <param name="predicate">The predicate</param>
        /// <param name="flowDirection">The traversal direction</param>
        /// <param name="strategy">The traversal strategy</param>
        /// <param name="includeRoot">Should the root node be included</param>
        /// <param name="depthLimits">Depth limits</param>
        /// <returns>The iterator created</returns>
        public TreeIter<TTreeNode> IterateOverAllNodesWhichPass(TTreeNode start, Predicate<TTreeNode> predicate, eTraversalFlowDirection flowDirection = TreeTraversal<TTreeNode>.DefaultTraversalFlowDirection, eTraversalStrategy strategy = TreeTraversal<TTreeNode>.DefaultTraversalStrategy, bool includeRoot = true, LevelRestriction depthLimits = null)
        {
            return TreeTraversal<TTreeNode>.IterateOverAllNodesWhichPass(this.Root, start, predicate, flowDirection, strategy, includeRoot, depthLimits, this.GetChildrenOverride, this.GetParentOverride);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T">The element type to search for</typeparam>
        /// <param name="predicate"></param>
        /// <param name="flowDirection">The traversal direction</param>
        /// <param name="strategy">The traversal strategy</param>
        /// <param name="includeRoot">Should the root node be included</param>
        /// <param name="canTypeBeAncestor">Can the search include derivitive types, or must it be an exact search</param>
        /// <param name="depthLimits">Depth limits</param>
        /// <returns></returns>
        public TreeIter<TTreeNode> IterateOverNodesOfType<T>(Predicate<T> predicate = null, eTraversalFlowDirection flowDirection = TreeTraversal<TTreeNode>.DefaultTraversalFlowDirection, eTraversalStrategy strategy = TreeTraversal<TTreeNode>.DefaultTraversalStrategy, bool includeRoot = true, bool canTypeBeAncestor = true, LevelRestriction depthLimits = null)
            where T : class, TTreeNode
        {
            return TreeTraversal<TTreeNode>.IterateOverNodesOfType(this.Root, this.Root, predicate, flowDirection, strategy, includeRoot, canTypeBeAncestor, depthLimits, this.GetChildrenOverride, this.GetParentOverride);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T">The element type to search for</typeparam>
        /// <param name="start">The starting point, if other than the root</param>
        /// <param name="predicate"></param>
        /// <param name="flowDirection">The traversal direction</param>
        /// <param name="strategy">The traversal strategy</param>
        /// <param name="includeRoot">Should the root node be included</param>
        /// <param name="canTypeBeAncestor">Can the search include derivitive types, or must it be an exact search</param>
        /// <param name="depthLimits">Depth limits</param>
        /// <returns></returns>
        public TreeIter<TTreeNode> IterateOverNodesOfType<T>(TTreeNode start, Predicate<T> predicate = null, eTraversalFlowDirection flowDirection = TreeTraversal<TTreeNode>.DefaultTraversalFlowDirection, eTraversalStrategy strategy = TreeTraversal<TTreeNode>.DefaultTraversalStrategy, bool includeRoot = true, bool canTypeBeAncestor = true, LevelRestriction depthLimits = null)
            where T : class, TTreeNode
        {
            return TreeTraversal<TTreeNode>.IterateOverNodesOfType(this.Root, start, predicate, flowDirection, strategy, includeRoot, canTypeBeAncestor, depthLimits, this.GetChildrenOverride, this.GetParentOverride);
        }

        public TreeIter<TTreeNode> IteratorAt(TreeNodePath nodePath, Predicate<TTreeNode> predicate = null, eTraversalFlowDirection flowDirection = TreeTraversal<TTreeNode>.DefaultTraversalFlowDirection, eTraversalStrategy strategy = TreeTraversal<TTreeNode>.DefaultTraversalStrategy, bool includeRoot = true, LevelRestriction depthLimits = null)
        {
            return TreeTraversal<TTreeNode>.IteratorAt(this.Root, nodePath, predicate, flowDirection, strategy, includeRoot, depthLimits, this.GetChildrenOverride, this.GetParentOverride);
        }

        public TTreeNode GetFirstChildWhichPasses(Predicate<TTreeNode> predicate, eTraversalFlowDirection flowDirection = TreeTraversal<TTreeNode>.DefaultTraversalFlowDirection, eTraversalStrategy strategy = TreeTraversal<TTreeNode>.DefaultTraversalStrategy, bool includeRoot = true, LevelRestriction depthLimits = null)
        {
            return TreeTraversal<TTreeNode>.GetFirstChildWhichPasses(this.Root, this.Root, predicate, flowDirection, strategy, includeRoot, depthLimits, this.GetChildrenOverride, this.GetParentOverride);
        }

        public TTreeNode GetFirstChildWhichPasses(TTreeNode start, Predicate<TTreeNode> predicate, eTraversalFlowDirection flowDirection = TreeTraversal<TTreeNode>.DefaultTraversalFlowDirection, eTraversalStrategy strategy = TreeTraversal<TTreeNode>.DefaultTraversalStrategy, bool includeRoot = true, LevelRestriction depthLimits = null)
        {
            return TreeTraversal<TTreeNode>.GetFirstChildWhichPasses(this.Root, start, predicate, flowDirection, strategy, includeRoot, depthLimits, this.GetChildrenOverride, this.GetParentOverride);
        }

        public T GetFirstChildOfType<T>(Predicate<T> predicate = null, bool includeRoot = true, eTraversalFlowDirection flowDirection = TreeTraversal<TTreeNode>.DefaultTraversalFlowDirection, eTraversalStrategy strategy = TreeTraversal<TTreeNode>.DefaultTraversalStrategy, bool canTypeBeAncestor = true, LevelRestriction depthLimits = null)
            where T : class, TTreeNode
        {
            return TreeTraversal<TTreeNode>.GetFirstChildOfType(this.Root, this.Root, predicate, flowDirection, strategy, includeRoot, canTypeBeAncestor, depthLimits, this.GetChildrenOverride, this.GetParentOverride);
        }

        public T GetFirstChildOfType<T>(TTreeNode start, Predicate<T> predicate = null, bool includeRoot = true, eTraversalFlowDirection flowDirection = TreeTraversal<TTreeNode>.DefaultTraversalFlowDirection, eTraversalStrategy strategy = TreeTraversal<TTreeNode>.DefaultTraversalStrategy, bool canTypeBeAncestor = true, LevelRestriction depthLimits = null)
            where T : class, TTreeNode
        {
            return TreeTraversal<TTreeNode>.GetFirstChildOfType(this.Root, start, predicate, flowDirection, strategy, includeRoot, canTypeBeAncestor, depthLimits, this.GetChildrenOverride, this.GetParentOverride);
        }

        public TTreeNode GetFirstParentWhichPasses(TTreeNode start, Predicate<TTreeNode> predicate, bool includeRoot = true, LevelRestriction depthLimits = null)
        {
            return TreeTraversal<TTreeNode>.GetFirstParentWhichPasses(this.Root, start, predicate, includeRoot, depthLimits, this.GetChildrenOverride, this.GetParentOverride);
        }

        public T GetFirstParentOfType<T>(TTreeNode start, Predicate<T> predicate = null, bool includeRoot = true, bool canTypeBeAncestor = true, LevelRestriction depthLimits = null)
            where T : class, TTreeNode
        {
            return TreeTraversal<TTreeNode>.GetFirstParentOfType(this.Root, start, predicate, includeRoot, canTypeBeAncestor, depthLimits, this.GetChildrenOverride, this.GetParentOverride);
        }

        public TTreeNode NodeAt(TreeNodePath nodePath)
        {
            return TreeTraversal<TTreeNode>.NodeAt(this.Root, nodePath, this.GetChildrenOverride, this.GetParentOverride);
        }
    }
}