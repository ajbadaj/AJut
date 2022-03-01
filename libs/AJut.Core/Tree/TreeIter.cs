namespace AJut.Tree
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;

    /// <summary>
    /// An iterator that progresses through a tree
    /// </summary>
    /// <typeparam name="TTreeNode">The tree node type</typeparam>
    public class TreeIter<TTreeNode> : IEquatable<TreeIter<TTreeNode>>
        where TTreeNode : class
    {
        //=================================================
        // ------------- Static Props ---------------------
        //=================================================

        /// <summary>
        /// What the iterator moves to when it's gone beyond the end of the tree, or encounters an issue with moving forward.
        /// </summary>
        [DebuggerDisplay("TreeIter<{typeof(TTreeNode).Name}>.End")]
        public static TreeIter<TTreeNode> End { get; private set; }

        //===================================================
        // ------------- Internal Props ---------------------
        //===================================================
        internal TreeIterState<TTreeNode> State { get; set; }

        //=================================================
        // ------------- Public Props ---------------------
        //=================================================
        public TreeTraversalParameters<TTreeNode> TraversalParameters { get; private set; }
        public TTreeNode Node => this.State?.Target;
        public TreeNodePath NodePath => this.State?.TargetPath;

        //=================================================
        // ------------- Construction ---------------------
        //=================================================

        /// <summary>
        /// Specialty constructor for creating End node
        /// </summary>
        private TreeIter () { }

        /// <summary>
        /// Basic constructor
        /// </summary>
        internal TreeIter (TreeIterState<TTreeNode> state, TreeTraversalParameters<TTreeNode> traversalParameters)
        {
            this.State = state;
            this.TraversalParameters = traversalParameters;
        }

        /// <summary>
        /// Specialty constructor for creating iter from a root node
        /// </summary>
        /// <param name="start">The root node</param>
        /// <param name="traversalParameters">The parameters to the tree traversal</param>
        /// <param name="getChildrenMethodOverride">An override to the default GetChildren method</param>
        /// <param name="getParentMethodOverride">An override to the default GetParent method</param>
        internal TreeIter (TTreeNode start, TreeTraversalParameters<TTreeNode> traversalParameters)
        {
            this.TraversalParameters = traversalParameters;

            // We want our paths to be root-centric so if start is not root (and there is a root listed)
            //  then we need to give ourselves a starter path from root
            TreeNodePath pathFromRoot = null;
            if (traversalParameters.Root != null && start != traversalParameters.Root)
            {
                var treePath = new Stack<int>();
                TTreeNode target = start;
                while (target != null && target != traversalParameters.Root)
                {
                    var parent = this.TraversalParameters.GetParentMethod(target);
                    if (parent == null)
                    {
                        break;
                    }

                    // To path in this way, we must gurantee that the children we get are indexable
                    //  thus this will throw if they are not of IList
                    var children = this.TraversalParameters.GetChildrenMethod.Invoke(parent);
                    if (children is IList listChild)
                    {
                        treePath.Push(listChild.IndexOf(target));
                    }
                    else
                    {
                        treePath.Push(children.Count(_ => _ != target));
                    }

                    target = parent;
                }

                pathFromRoot = new TreeNodePath(treePath);
            }
            this.State = new TreeIterState<TTreeNode>(start, traversalParameters, pathFromRoot);
            this.State.ProcessNextEvaluations();
        }

        /// <summary>
        /// Specialized constructor for creating a ThroughChildren iterator at a node path. Basically, we create an iterator at the parent
        /// then queue up the remaining siblings for next evals in iter state. That allows us to start at a path and not miss the siblings
        /// </summary>
        /// <param name="nodeParent"></param>
        /// <param name="nodePath"></param>
        /// <param name="traversalParameters"></param>
        /// <param name="getChildrenMethodOverride"></param>
        /// <param name="getParentMethodOverride"></param>
        private TreeIter (TTreeNode nodeParent, TreeNodePath nodePath, TreeTraversalParameters<TTreeNode> traversalParameters, GetTreeNodeChildren<TTreeNode> getChildrenMethodOverride = null, GetTreeNodeParent<TTreeNode> getParentMethodOverride = null)
        {
            this.TraversalParameters = traversalParameters;

            //int finalIndex = nodePath.Last();
            //TTreeNode currentTarget = this.State.GetChildrenMethod(nodeParent).ElementAt(finalIndex);
            this.State = new TreeIterState<TTreeNode>(nodeParent, traversalParameters, nodePath);
            this.State.ProcessNextEvaluations(nodePath.Last());
        }

        internal static TreeIter<TTreeNode> CreateIteratorAtNodePath (TTreeNode root, TreeNodePath nodePath, TreeTraversalParameters<TTreeNode> traversalParameters, GetTreeNodeChildren<TTreeNode> getChildrenMethodOverride = null, GetTreeNodeParent<TTreeNode> getParentMethodOverride = null)
        {
            // If the node path is empty, then we're creating one from the root
            if (nodePath.Count == 0)
            {
                return new TreeIter<TTreeNode>(root, traversalParameters);
            }

            if (traversalParameters.FlowDirection == eTraversalFlowDirection.ThroughParents)
            {
                // Why is this not supported
                Debug.Fail("Node pathing through parents is not supported");
            }
            else if (traversalParameters.FlowDirection == eTraversalFlowDirection.ThroughChildren)
            {
                // Otherwise we are creating an iterator for the node's parent, but with the next evaluations stocked starting at the final nodepath piece,
                //  then we can just increment by one. This enables us to use the siblings to the indicated start node.
                TTreeNode parentNode = TreeTraversal<TTreeNode>.ParentNodeFromPath(root, nodePath);
                if (parentNode == null)
                {
                    return End;
                }

                var justBeforeIter = new TreeIter<TTreeNode>(parentNode, nodePath, traversalParameters, getChildrenMethodOverride, getParentMethodOverride);
                return ++justBeforeIter;
            }
            else if (traversalParameters.FlowDirection == eTraversalFlowDirection.ReversedThroughChildren)
            {
                Debug.Fail("Node pathing through parents is not supported");
            }

            return End;
        }

        static TreeIter ()
        {
            End = new TreeIter<TTreeNode>();
        }

        //==============================================
        // ------------- Iteration ---------------------
        //==============================================

        public static TreeIter<TTreeNode> operator ++ (TreeIter<TTreeNode> iterator)
        {
            if (End == iterator)
            {
                return End;
            }

            TreeIterState<TTreeNode> nextState = iterator.State.CreateCopyForNextEvaluation();
            TreeIter<TTreeNode> nextIter = new TreeIter<TTreeNode>(nextState, iterator.TraversalParameters);

            for (; ; )
            {
                eNextTraversalMove nextAction = nextIter.DetermineNextAction();

                switch (nextAction)
                {
                    case eNextTraversalMove.Process:
                        nextState.MoveToNext();
                        nextState.ProcessNextEvaluations();
                        return nextIter;

                    case eNextTraversalMove.ProcessAndPrune:
                        nextState.MoveToNext();
                        return nextIter;

                    case eNextTraversalMove.SkipAndContinueSearching:
                        nextState.MoveToNext();
                        nextState.ProcessNextEvaluations();
                        continue;

                    case eNextTraversalMove.PruneAndContinueSearching:
                        nextState.MoveToNext();
                        continue;

                    default:
                    case eNextTraversalMove.Quit:
                        return End;
                }
            }
        }

        private enum eNextTraversalMove { Quit, PruneAndContinueSearching, SkipAndContinueSearching, Process, ProcessAndPrune };

        /// <summary>
        /// Determines the next action in iteration that should be taken. This is called *after* a node has been sucessfully processed, 
        /// and determines what the next phase of iteration should be in regards to the next node.
        /// </summary>
        /// <returns></returns>
        private eNextTraversalMove DetermineNextAction ()
        {
            TreeEvalItem<TTreeNode> next = this.State.PeekNext();

            // If we are over our max number of allowed evaluations, or there is a termination path set, and we've already evaluted that, then it's time to quit.
            if (next == null
                || (this.TraversalParameters.HasEvaluationCap && this.State.CurrentNumberOfEvaluations >= this.TraversalParameters.MaxNumberOfEvaluations)
                || (this.TraversalParameters.TerminationNodePath != null && this.NodePath.Equals(this.TraversalParameters.TerminationNodePath)))
            {
                return eNextTraversalMove.Quit;
            }

            // Make sure we're allowed to run our action
            if (!this.State.HasPendingEvalItems || (this.TraversalParameters.EnforcesLevelRestriction && !this.TraversalParameters.DepthLimits.Passes(next.DepthFromOrigin, true)))
            {
                return eNextTraversalMove.PruneAndContinueSearching;
            }

            // Note: Evaluting type before predicate because some predicates are actually typed
            if (this.TraversalParameters.RequiresTypeMatching && !this.TraversalParameters.TypeMatching.Evaluate(next.Node.GetType()))
            {
                return eNextTraversalMove.SkipAndContinueSearching;
            }

            if (this.TraversalParameters.HasPredicate && !this.TraversalParameters.Predicate(next.Node))
            {
                return eNextTraversalMove.SkipAndContinueSearching;
            }

            if (this.TraversalParameters.PruneAfterFirstFind)
            {
                return eNextTraversalMove.ProcessAndPrune;
            }

            return eNextTraversalMove.Process;
        }

        //==============================================
        // ------------- Comparison --------------------
        //==============================================
        public bool Equals (TreeIter<TTreeNode> other)
        {
            return this.Node == other.Node;
        }

        public override bool Equals (object obj)
        {
            if (obj is TreeIter<TTreeNode> self)
            {
                return this.Equals(self);
            }
            else
            {
                return base.Equals(obj);
            }
        }

        public override int GetHashCode ()
        {
            return this.Node.GetHashCode();
        }
    }

    /// <summary>
    /// The up-coming to evaluate nodes need to keep track of a few things, this class let's me wrap that all up together
    /// </summary>
    /// <typeparam name="TTreeNode"></typeparam>
    internal class TreeEvalItem<TTreeNode> where TTreeNode : class
    {
        /// <summary>
        /// The node to evaluate.
        /// </summary>
        public TTreeNode Node { get; private set; }

        /// <summary>
        /// The way to index this node from the top (starting point)
        /// </summary>
        public TreeNodePath NodePath { get; private set; }

        public int DepthFromOrigin { get { return this.NodePath.Count - 1; } }

        public TreeEvalItem (TTreeNode node, TreeNodePath path)
        {
            this.Node = node;
            this.NodePath = path;
        }
    }

    /// <summary>
    /// The state used in the iteration, stores all the tracking
    /// </summary>
    /// <typeparam name="TTreeNode">The tree node type</typeparam>
    internal class TreeIterState<TTreeNode> where TTreeNode : class
    {
        private TreeTraversalParameters<TTreeNode> m_traversalParameters;
        private List<TTreeNode> m_reentrancyTracking = new List<TTreeNode>();
        private List<TreeEvalItem<TTreeNode>> m_nextEvalSet = new List<TreeEvalItem<TTreeNode>>();

        // Internal Properties
        internal TTreeNode Target { get; private set; }
        internal TreeNodePath TargetPath { get; private set; }

        /// <summary>
        /// The number of node levels down from the origin, the origin being where on the tree iteration began, not necessarily the tree root.
        /// </summary>
        internal int CurrentDepthFromOrigin { get { return this.TargetPath.Count; } }

        /// <summary>
        /// Indicates the number of nodes that have been evaluated from the tree (not necessarily processed or used).
        /// </summary>
        internal int CurrentNumberOfEvaluations { get; private set; }

        internal bool HasPendingEvalItems { get { return m_nextEvalSet.Count > 0; } }

        internal TreeIterState (TTreeNode node, TreeTraversalParameters<TTreeNode> traversalParameters, TreeNodePath nodePath = null)
        {
            m_traversalParameters = traversalParameters;

            this.Target = node;
            this.CurrentNumberOfEvaluations = 0;

            if (nodePath != null)
            {
                this.TargetPath = nodePath;
            }
            else
            {
                this.TargetPath = new TreeNodePath();
            }
        }

        internal TreeIterState<TTreeNode> CreateCopyForNextEvaluation ()
        {
            return new TreeIterState<TTreeNode>(this.Target, this.m_traversalParameters, new TreeNodePath(this.TargetPath))
            {
                m_reentrancyTracking = new List<TTreeNode>(this.m_reentrancyTracking), // Copy
                m_nextEvalSet = new List<TreeEvalItem<TTreeNode>>(m_nextEvalSet), // Copy
                CurrentNumberOfEvaluations = this.CurrentNumberOfEvaluations + 1,
            };
        }

        internal void MoveToNext ()
        {
            var nextItem = m_nextEvalSet[0];
            m_nextEvalSet.RemoveAt(0);

            this.Target = nextItem.Node;
            this.TargetPath = nextItem.NodePath;
        }

        internal TreeEvalItem<TTreeNode> PeekNext ()
        {
            if (m_nextEvalSet.Any())
            {
                return m_nextEvalSet[0];
            }

            return null;
        }

        private TreeEvalItem<TTreeNode> GetLastLeaf (TTreeNode node, int currentDepth, TreeNodePath path)
        {
            IList<TTreeNode> children = m_traversalParameters.GetChildrenMethod(node).ToList();
            if (children.Count == 0)
            {
                return new TreeEvalItem<TTreeNode>(node, path);
            }

            int lastIndex = children.Count - 1;
            path.AddToPath(lastIndex);
            return GetLastLeaf(children[lastIndex], currentDepth + 1, path);
        }

        private TreeEvalItem<TTreeNode> GetLastLeafAtDepth (TTreeNode node, TreeNodePath path, int targetDepth)
        {
            if (path.Count == targetDepth)
            {
                return new TreeEvalItem<TTreeNode>(node, path);
            }

            List<TTreeNode> children = m_traversalParameters.GetChildrenMethod(node).ToList();
            for (int childIndex = children.Count - 1; childIndex >= 0; --childIndex)
            {
                var result = GetLastLeafAtDepth(children[childIndex], path.CopyAndAddToPath(childIndex), targetDepth);
                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }

        private void ProcessNextEvaluationsForParentTraversal ()
        {
            TTreeNode parent = this.Target != null ? m_traversalParameters.GetParentMethod(this.Target) : null;
            if (parent == null)
            {
                return;
            }

            if (!this.IsUniqueInIteration(parent))
            {
                return;
            }

            TreeNodePath newPath = this.TargetPath.CopyAndRemoveEnd();
            m_nextEvalSet.Add(new TreeEvalItem<TTreeNode>(parent, newPath));
            if (m_traversalParameters.TrackReentrancy)
            {
                m_reentrancyTracking.Add(parent);
            }
            return;
        }

        private void ProcessNextEvaluationsForChildTraversal (List<TreeEvalItem<TTreeNode>> nextEvals, int targetChildStartIndex)
        {
            if (this.Target == null)
            {
                return;
            }

            int childIndex = 0;
            foreach (TTreeNode child in m_traversalParameters.GetChildrenMethod(this.Target))
            {
                int currentChildIndex = childIndex++;
                if (child == null || (targetChildStartIndex != -1 && currentChildIndex < targetChildStartIndex))
                {
                    continue;
                }

                if (this.IsUniqueInIteration(child))
                {
                    // If we're already targeting a completed path, we don't want to add to it
                    if (targetChildStartIndex != -1)
                    {
                        nextEvals.Add(new TreeEvalItem<TTreeNode>(child, new TreeNodePath(this.TargetPath)));
                    }
                    else
                    {
                        var newNodePath = this.TargetPath.CopyAndAddToPath(currentChildIndex);
                        nextEvals.Add(new TreeEvalItem<TTreeNode>(child, newNodePath));
                    }
                }
            }
        }

        public void SetupForArbitraryIteration(List<TreeEvalItem<TTreeNode>> nextEvals)
        {
            // It's possible we started this iteration arbitrarily
            TreeNodePath path = this.TargetPath;
            TTreeNode target = this.Target;
            while (nextEvals.Count == 0)
            {
                var parent = target != null ? m_traversalParameters.GetParentMethod(target) : null;
                if (parent == null)
                {
                    break;
                }

                path = path.CopyAndRemoveEnd();
                var children = m_traversalParameters.GetChildrenMethod(parent).ToList();
                int index = children.IndexOf(target);
                if (index + 1 < children.Count)
                {
                    for (int childIndex = index + 1; childIndex < children.Count; ++childIndex)
                    {
                        nextEvals.Add(new TreeEvalItem<TTreeNode>(children[childIndex], path.CopyAndAddToPath(index)));
                    }

                    return;
                }

                target = parent;
            }
        }

        private class IndexTrackedNode
        {
            public int Index { get; private set; }
            public TTreeNode Node { get; private set; }

            public IndexTrackedNode (int index, TTreeNode node)
            {
                this.Index = index;
                this.Node = node;
            }
        }
        private TreeEvalItem<TTreeNode> GetNextRBFSCousin (TTreeNode curr, TreeNodePath path, int targetDepth)
        {
            // If we've gotten into an error state, or if we're attempting to go past root
            if (curr == null || curr == m_traversalParameters.Root)
            {
                return null;
            }

            // Iterate over all siblings and build a stack of siblings for us to evaluate (popping will give us sibling closest to curr, instead of parent's index[0])
            var parent = m_traversalParameters.GetParentMethod(curr);
            if (parent == null)
            {
                return null;
            }

            var siblingStack = new Stack<IndexTrackedNode>();
            int siblingIndex = 0;
            foreach (var sibling in m_traversalParameters.GetChildrenMethod(parent))
            {
                if (sibling == curr)
                {
                    break;
                }

                siblingStack.Push(new IndexTrackedNode(siblingIndex++, sibling));
            }

            // Evaluate from closest to curr back to index zero, and call GetlastLeafAtDepth, if we find a cousin at the target depth
            //  then that is the next BFS item to search
            while (siblingStack.Count > 0)
            {
                var next = siblingStack.Pop();
                TreeEvalItem<TTreeNode> cousinLeaf = this.GetLastLeafAtDepth(next.Node, path.CopyAndSwapEndForSibling(next.Index), targetDepth);
                if (cousinLeaf != null)
                {
                    return cousinLeaf;
                }
            }

            // If no cousin is found, than recurse upwards, this will be evaluting parent as curr
            return this.GetNextRBFSCousin(parent, path.CopyAndRemoveEnd(), targetDepth);
        }

        private TreeEvalItem<TTreeNode> ProcessNextEvaluationsForRBFS (TTreeNode target, TreeNodePath path, int targetDepth)
        {
            if (target == null)
            {
                return null;
            }

            var parent = m_traversalParameters.GetParentMethod(target);
            if (parent == null)
            {
                return null;
            }


            TTreeNode nextClosestSibling = null;
            int nextClosestSiblingIndex = -1;
            int childIndex = 0;
            foreach (var sibling in m_traversalParameters.GetChildrenMethod(parent))
            {
                if (sibling == target)
                {
                    break;
                }

                nextClosestSiblingIndex = childIndex++;
                nextClosestSibling = sibling;
            }

            // No siblings, recurse to the parent/grandparent
            if (nextClosestSibling == null)
            {
                TreeEvalItem<TTreeNode> cousin = this.GetNextRBFSCousin(parent, path.CopyAndRemoveEnd(), targetDepth);
                if (cousin != null)
                {
                    return cousin;
                }

                // Looks like end of this depth... time to go forward and back to the end
                return this.GetLastLeafAtDepth(m_traversalParameters.Root, new TreeNodePath(), this.CurrentDepthFromOrigin - 1);
            }


            // There were some siblings, see if we can find our next target
            return new TreeEvalItem<TTreeNode>(nextClosestSibling, path.CopyAndSwapEndForSibling(nextClosestSiblingIndex));
        }
        private void ProcessNextEvaluationsForReverseChildTraversal (List<TreeEvalItem<TTreeNode>> nextEvals)
        {
            TreeEvalItem<TTreeNode> nextEval = null;
            if (m_traversalParameters.ChildTraversalStrategy == eTraversalStrategy.BreadthFirst)
            {
                nextEval = this.ProcessNextEvaluationsForRBFS(this.Target, new TreeNodePath(this.TargetPath), this.CurrentDepthFromOrigin);

                // If the processing gives us nothing, then it's time to move up to the next 
                if (nextEval == null)
                {
                    var root = TreeTraversal<TTreeNode>.FindRoot(this.Target);
                    if (root != null)
                    {
                        nextEval = this.GetLastLeafAtDepth(root, new TreeNodePath(), this.TargetPath.Count - 1);
                    }
                }

            }
            else if (m_traversalParameters.ChildTraversalStrategy == eTraversalStrategy.DepthFirst)
            {
                var parent = m_traversalParameters.GetParentMethod(this.Target);
                if (parent == null)
                {
                    return;
                }

                // Iterate over the children, saving the current one into toVisit until we reach
                //  the target, that will give us the sibling before target
                TTreeNode toVisit = null;
                foreach (var child in m_traversalParameters.GetChildrenMethod(parent))
                {
                    if (child == this.Target)
                    {
                        break;
                    }

                    toVisit = child;
                }

                // If there are no siblings before target, then we need to move up to the parent
                if (toVisit == null)
                {
                    TreeNodePath parentPath = this.TargetPath.CopyAndRemoveEnd();
                    nextEval = new TreeEvalItem<TTreeNode>(parent, parentPath);
                }
                else
                {
                    // If there was an adjacent sibling, then we need to get the last leaf node of it, that is the next
                    //  evaluation. When that item gets evaluated, it will go through this same process which will lead
                    //  to any of it's siblings, andtheir last leaf, etc. etc. giving us a depth first search backwards
                    nextEval = this.GetLastLeaf(toVisit, this.CurrentDepthFromOrigin, new TreeNodePath(this.TargetPath));
                }
            }

            if (nextEval != null && this.IsUniqueInIteration(nextEval.Node))
            {
                nextEvals.Add(nextEval);
            }
        }

        /// <summary>
        /// Iterates over the Children if through children, or parent if through parent, and adds valid items into the
        /// next evaluations list.
        /// </summary>
        /// <param name="targetChildStartIndex">On the occasion where iteration starts at an offset, we will process starting there</param>
        internal void ProcessNextEvaluations (int targetChildStartIndex = -1)
        {
            if (m_traversalParameters.FlowDirection == eTraversalFlowDirection.ThroughParents)
            {
                this.ProcessNextEvaluationsForParentTraversal();
                return;
            }

            var nextEvals = new List<TreeEvalItem<TTreeNode>>();
            if (m_traversalParameters.FlowDirection == eTraversalFlowDirection.ThroughChildren)
            {
                this.ProcessNextEvaluationsForChildTraversal(nextEvals, targetChildStartIndex);
            }
            else if (m_traversalParameters.FlowDirection == eTraversalFlowDirection.ReversedThroughChildren)
            {
                this.ProcessNextEvaluationsForReverseChildTraversal(nextEvals);
            }

            // Breadth first, we add to the TreeIter<TTreeNode>.End, like a queue, to make sure each level is built sibling by sibling
            if (m_traversalParameters.ChildTraversalStrategy == eTraversalStrategy.BreadthFirst)
            {
                m_nextEvalSet.AddRange(nextEvals);
            }
            // Depth first, we add the whole list of children to the beginning, interrupting the next sibling, 
            //  causing the evaluations to happen depth first.
            else if (m_traversalParameters.ChildTraversalStrategy == eTraversalStrategy.DepthFirst)
            {
                // If we've hit the proverbial end, we may want to load up some evaluations from the next sub-tree
                //  if we're allowed to exit the start depth
                if (nextEvals.Count == 0 && (m_traversalParameters.DepthLimits?.AllowsExitingStartDepth ?? false))
                {
                    this.SetupForArbitraryIteration(m_nextEvalSet);
                }
                else
                {
                    m_nextEvalSet.InsertRange(0, nextEvals);
                }
            }

            if (m_traversalParameters.TrackReentrancy)
            {
                m_reentrancyTracking.AddRange(nextEvals.Select(_ => _.Node));
            }
        }

        private bool IsUniqueInIteration (TTreeNode node)
        {
            if (m_traversalParameters.TrackReentrancy)
            {
                if (m_reentrancyTracking.Contains(node))
                {
                    return false;
                }
            }
            else if (m_nextEvalSet.Any(eval => eval.Node == node))
            {
                return false;
            }

            return true;
        }
    }
}
