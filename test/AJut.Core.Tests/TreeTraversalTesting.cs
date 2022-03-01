namespace AJut.Core.UnitTests
{
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Diagnostics;
    using System.Linq;
    using AJut;
    using AJut.Tree;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [DebuggerDisplay("Tree Node - {Value} - {m_children.Count} Children")]
    public class TestTreePart
    {
        public TestTreePart Parent { get; private set; }
        public IReadOnlyList<TestTreePart> Children { get; private set; }

        List<TestTreePart> m_children = new List<TestTreePart>();


        public string Value { get; private set; }

        public TestTreePart(string value)
            : this(null, value)
        {
        }
        public TestTreePart(TestTreePart parent, string value)
        {
            this.Parent = parent;
            this.Value = value;
            this.Children = new ReadOnlyCollection<TestTreePart>(m_children);
            if (this.Parent != null)
            {
                this.Parent.m_children.Add(this);
            }
        }

        public static IEnumerable<TestTreePart> GetChildren(TestTreePart treePart)
        {
            return treePart.Children;
        }
        public static TestTreePart GetParent(TestTreePart treePart)
        {
            return treePart?.Parent;
        }

        static TestTreePart()
        {
            TreeTraversal<TestTreePart>.SetupDefaults(GetChildren, GetParent);
        }

        public override string ToString()
        {
            return "Tree Node - {0} - {1} Children".ApplyFormatArgs(this.Value, m_children.Count);
        }
    }

    public class DerivedTreePart : TestTreePart
    {
        public DerivedTreePart(string value)
            : base(value)
        { }

        public DerivedTreePart(TestTreePart parent, string value)
            : base(parent, value)
        { }
    }

    [TestClass]
    public class TreeTraversalTesting
    {
        public TestTreePart root = null;
        private TestTreePart root_a = null;
        private TestTreePart root_c = null;
        private TestTreePart a_b_c = null;
        private TestTreePart a_a = null;
        private TestTreePart a_c = null;
        private TestTreePart a_b_a = null;
        private TestTreePart a_b_b = null;
        private TestTreePart c_a_a = null;
        private TestTreePart end;
        private TestTreePart root_b;
        private TreeTraverser<TestTreePart> m_tree;

        [TestInitialize]
        public void Setup()
        {
            /*
                      root
                     /  | \
                   a    b  c
                  /|\      |
                 / | \     |
               aa ab  ac  ca
                  /|\      |\
                 / | \     | \
              aba abb abc caa cab
             * */
            root = new TestTreePart("root");
            root_a = new TestTreePart(root, "a");
            root_b = new TestTreePart(root, "b");
            root_c = new TestTreePart(root, "c");

            a_a = new DerivedTreePart(root_a, "a.a");
            var a_b = new TestTreePart(root_a, "a.b");
            a_c = new DerivedTreePart(root_a, "a.c");

            a_b_a = new DerivedTreePart(a_b, "a.b.a");
            a_b_b = new TestTreePart(a_b, "a.b.b");
            a_b_c = new TestTreePart(a_b, "a.b.c");

            var c_a = new TestTreePart(root_c, "c.a");
            c_a_a = new TestTreePart(c_a, "c.a.a");
            end = new TestTreePart(c_a, "c.a.b");

            m_tree = new TreeTraverser<TestTreePart>(root);

            Logger.Enable();
            Logger.FlushAfterEach = true;
        }

        private static readonly string[] kBFS_Order = { "root", "a", "b", "c", "a.a", "a.b", "a.c", "c.a", "a.b.a", "a.b.b", "a.b.c", "c.a.a", "c.a.b" };
        private static readonly string[] kDFS_Order = { "root", "a", "a.a", "a.b", "a.b.a", "a.b.b", "a.b.c", "a.c", "b", "c", "c.a", "c.a.a", "c.a.b" };

        [TestMethod]
        public void TreeTraversal_CanFindChild()
        {
            var foundTreePart = TreeTraversal<TestTreePart>.GetFirstChildWhichPasses(root, node => node == a_b_c);
            Assert.AreSame(a_b_c, foundTreePart);
        }

        [TestMethod]
        public void TreeTraversal_CanFindParent()
        {
            var foundTreePart = TreeTraversal<TestTreePart>.GetFirstParentWhichPasses(root, node => node == root);
            Assert.AreSame(root, foundTreePart);
        }

        [TestMethod]
        public void TreeTraversal_CanIterateOverSeveral()
        {
            Assert.AreEqual(13, TreeTraversal<TestTreePart>.All(root).Count());
        }

        [TestMethod]
        public void TreeTraversal_BreadthFirstWorks()
        {
            int expectationIndex = 0;
            foreach (TestTreePart node in TreeTraversal<TestTreePart>.All(root, eTraversalFlowDirection.ThroughChildren, eTraversalStrategy.BreadthFirst))
            {
                Assert.AreEqual(kBFS_Order[expectationIndex++], node.Value);
            }
        }

        [TestMethod]
        public void TreeTraversal_DepthFirstWorks()
        {
            int expectationIndex = 0;
            foreach (TestTreePart node in TreeTraversal<TestTreePart>.All(root, eTraversalFlowDirection.ThroughChildren, eTraversalStrategy.DepthFirst))
            {
                Assert.AreEqual(kDFS_Order[expectationIndex++], node.Value);
            }
        }

        [TestMethod]
        public void TreeTraversal_IteratorAccessor_BuiltProperly()
        {
            var treeIter = TreeTraversal<TestTreePart>.CreateIterator(root);
            while (treeIter != TreeIter<TestTreePart>.End)
            {
                if (treeIter.Node == a_b_c)
                {
                    break;
                }

                ++treeIter;
            }

            Assert.AreSame(a_b_c, treeIter.Node);
            CollectionAssert.AreEqual(treeIter.NodePath, new[] { 0, 1, 2 });

            treeIter = TreeTraversal<TestTreePart>.CreateIterator(root);
            while (treeIter != TreeIter<TestTreePart>.End)
            {
                if (treeIter.Node == c_a_a)
                {
                    break;
                }

                ++treeIter;
            }

            Assert.AreSame(c_a_a, treeIter.Node);
            CollectionAssert.AreEqual(treeIter.NodePath, new[] { 2, 0, 0 });
        }

        [TestMethod]
        public void TreeTraversal_IteratorAccessor_NodeAt_Works()
        {
            Assert.AreSame(a_b_c, TreeTraversal<TestTreePart>.NodeAt(root, new TreeNodePath(0, 1, 2)));
        }

        [TestMethod]
        public void TreeTraversal_IteratorAccessor_NodeAt_OutOfBoundsReturnsNull()
        {
            Assert.IsNull(TreeTraversal<TestTreePart>.NodeAt(root, new TreeNodePath(550, 1, 2)));
        }

        [TestMethod]
        public void TreeTraversal_IteratorAccessor_IteratorAt_OutOfBoundsReturnsEnd()
        {
            Assert.AreSame(TreeIter<TestTreePart>.End, TreeTraversal<TestTreePart>.IteratorAt(root, new TreeNodePath(550, 1, 2)));
        }

        [TestMethod]
        public void TreeTraversal_IteratorAccessor_IterateFrom_Works()
        {
            // At 0,0 we got to a->a_a, a_b_c is a child of a_b, which is a sibling to a_a - this is the hardest test of if this works or not
            TreeIter<TestTreePart> iter = TreeTraversal<TestTreePart>.IteratorAt(root, new TreeNodePath(0, 0), node => node == a_b_c);
            Assert.AreSame(a_b_c, iter.Node);
        }

        [TestMethod]
        public void TreeTraversal_ReverseIteration_BFS_Works()
        {
            Logger.LogInfo("==============[ Forwards ]=======================");
            var forwards = TreeTraversal<TestTreePart>.All(root, eTraversalFlowDirection.ThroughChildren, eTraversalStrategy.BreadthFirst).Select(_ => _.Value).ToList();

            Logger.LogInfo("==============[ Backwards ]=======================");
            var backwards = TreeTraversal<TestTreePart>.All(root, end, eTraversalFlowDirection.ReversedThroughChildren, eTraversalStrategy.BreadthFirst).Select(_ => _.Value).ToList();


            backwards.Reverse();

            Assert.AreEqual(forwards.Count, backwards.Count);
            for (int index = 0; index < forwards.Count; ++index)
            {
                Assert.AreEqual(forwards[index], backwards[index]);
            }
        }

        [TestMethod]
        public void TreeTraversal_ReverseIteration_DFS_Works()
        {
            var forwards = TreeTraversal<TestTreePart>.All(root, eTraversalFlowDirection.ThroughChildren, eTraversalStrategy.DepthFirst).Select(_ => _.Value).ToList();

            var backwards = TreeTraversal<TestTreePart>.All(end, eTraversalFlowDirection.ReversedThroughChildren, eTraversalStrategy.DepthFirst).Select(_ => _.Value).ToList();


            backwards.Reverse();

            Assert.AreEqual(forwards.Count, backwards.Count);
            for (int index = 0; index < forwards.Count; ++index)
            {
                Assert.AreEqual(forwards[index], backwards[index]);
            }
        }


        const int Match = 0;
        const int LeftBeforeRight = -1;
        const int RightBeforeLeft = 1;

        [TestMethod]
        public void TreeTraversal_TreeCompare_Paths_Match__BFS()
        {
            TreeNodePath left = new TreeNodePath(0, 1, 1);
            TreeNodePath right = new TreeNodePath(0, 1, 1);
            Assert.AreEqual(Match, TreeCompare.CompareTreeNodePaths(left, right, eTraversalFlowDirection.ThroughChildren, eTraversalStrategy.BreadthFirst));
        }
        [TestMethod]
        public void TreeTraversal_TreeCompare_Paths_Match__DFS()
        {
            TreeNodePath left = new TreeNodePath(0, 1, 1);
            TreeNodePath right = new TreeNodePath(0, 1, 1);
            Assert.AreEqual(Match, TreeCompare.CompareTreeNodePaths(left, right, eTraversalFlowDirection.ThroughChildren, eTraversalStrategy.DepthFirst));
        }


        [TestMethod]
        public void TreeTraversal_TreeCompare_Paths_RightDeeperEarlyBranch__BFS()
        {
            TreeNodePath left = new TreeNodePath(0, 1);
            TreeNodePath right = new TreeNodePath(0, 0, 0);
            Assert.AreEqual(LeftBeforeRight, TreeCompare.CompareTreeNodePaths(left, right, eTraversalFlowDirection.ThroughChildren, eTraversalStrategy.BreadthFirst));
        }
        [TestMethod]
        public void TreeTraversal_TreeCompare_Paths_RightDeeperEarlyBranch__DFS()
        {
            TreeNodePath left = new TreeNodePath(0, 1);
            TreeNodePath right = new TreeNodePath(0, 0, 0);
            Assert.AreEqual(RightBeforeLeft, TreeCompare.CompareTreeNodePaths(left, right, eTraversalFlowDirection.ThroughChildren, eTraversalStrategy.DepthFirst));
        }


        [TestMethod]
        public void TreeTraversal_TreeCompare_Paths_RightDeeperLaterBranch__BFS()
        {
            TreeNodePath left  = new TreeNodePath(0, 1, 0, 0);
            TreeNodePath right = new TreeNodePath(0, 1, 1, 0, 0);
            Assert.AreEqual(LeftBeforeRight, TreeCompare.CompareTreeNodePaths(left, right, eTraversalFlowDirection.ThroughChildren, eTraversalStrategy.BreadthFirst));
        }
        [TestMethod]
        public void TreeTraversal_TreeCompare_Paths_RightDeeperLaterBranch__DFS()
        {
            TreeNodePath left  = new TreeNodePath(0, 1, 0, 0);
            TreeNodePath right = new TreeNodePath(0, 1, 1, 0, 0);
            Assert.AreEqual(LeftBeforeRight, TreeCompare.CompareTreeNodePaths(left, right, eTraversalFlowDirection.ThroughChildren, eTraversalStrategy.DepthFirst));
        }



        [TestMethod]
        public void TreeTraversal_TreeCompare_Paths_MatchButRightDeeper__BFS()
        {
            TreeNodePath left = new TreeNodePath(0, 1, 1);
            TreeNodePath right = new TreeNodePath(0, 1, 1, 0);
            Assert.AreEqual(LeftBeforeRight, TreeCompare.CompareTreeNodePaths(left, right, eTraversalFlowDirection.ThroughChildren, eTraversalStrategy.BreadthFirst));
        }
        [TestMethod]
        public void TreeTraversal_TreeCompare_Paths_MatchButRightDeeper__DFS()
        {
            TreeNodePath left = new TreeNodePath(0, 1, 1);
            TreeNodePath right = new TreeNodePath(0, 1, 1, 0);
            Assert.AreEqual(LeftBeforeRight, TreeCompare.CompareTreeNodePaths(left, right, eTraversalFlowDirection.ThroughChildren, eTraversalStrategy.DepthFirst));
        }

        private static readonly string[] kDerivedTreepartOrder_BFS = new string[] { "a.a", "a.c", "a.b.a" };
        private static readonly string[] kDerivedTreepartOrder_DFS = new string[] { "a.a", "a.b.a", "a.c" };

        [TestMethod]
        public void TreeTraversal_TypeTraversal_FirstChildOfType_DFS()
        {
            int resultIndex = 0;
            for(var treeIter = m_tree.IterateOverNodesOfType<DerivedTreePart>(); treeIter != TreeIter<TestTreePart>.End; ++treeIter)
            {
                Assert.AreEqual(kDerivedTreepartOrder_DFS[resultIndex++], treeIter.Node.Value);
            }
        }

        [TestMethod]
        public void TreeTraversal_TypeTraversal_FirstChildOfType_BFS()
        {
            int resultIndex = 0;
            for (var treeIter = m_tree.IterateOverNodesOfType<DerivedTreePart>(strategy:eTraversalStrategy.BreadthFirst); treeIter != TreeIter<TestTreePart>.End; ++treeIter)
            {
                Assert.AreEqual(kDerivedTreepartOrder_BFS[resultIndex++], treeIter.Node.Value);
            }
        }

        [TestMethod]
        public void TreeTraversal_ArbitraryStartPoint_BFS_Works()
        {
            /*

                     root
                     /|\
                    / | \
                   a  b  c
                  /|\     \
                 / | \     \
              aa  ab  ac    ca
                  /|\       | \
                 / | \      |  \
              aba abb abc  caa cab

             * */
            var pathTo_abb = new TreeNodePath(0, 1, 1);
            var iter = TreeTraversal<TestTreePart>.IteratorAt(root, pathTo_abb, strategy: eTraversalStrategy.BreadthFirst);
            Assert.AreNotEqual(TreeIter<TestTreePart>.End, iter);
            ++iter;
            Assert.AreNotEqual(TreeIter<TestTreePart>.End, iter);
            Assert.AreEqual(a_b_c, iter.Node);
        }

        [TestMethod]
        public void TreeTraversal_ArbitraryStartPoint_DFS_Works ()
        {
            /*
             
                    root
                     /|\
                    / | \
                   a  b  c
                  /|\     \
                 / | \     \
              aa  ab  ac    ca
                  /|\       | \
                 / | \      |  \
              aba abb abc  caa cab

             * */
            TreeNodePath pathTo_abc = new TreeNodePath(0, 1, 2);
            var iter = TreeTraversal<TestTreePart>.IteratorAt(root, pathTo_abc, strategy: eTraversalStrategy.DepthFirst, depthLimits:new LevelRestriction { AllowsExitingStartDepth = true });
            Assert.AreNotEqual(TreeIter<TestTreePart>.End, iter);
            ++iter;
            Assert.AreNotEqual(TreeIter<TestTreePart>.End, iter);
            Assert.AreEqual(a_c, iter.Node);
        }

        [TestMethod]
        public void TreeTraversal_ArbitraryStartPoint_IncludeSelfOffWorks ()
        {
            /*
             
                    root
                     /|\
                    / | \
                   a  b  c
                  /|\     \
                 / | \     \
              aa  ab  ac    ca
                  /|\       | \
                 / | \      |  \
              aba abb abc  caa cab

             * */

            var arbitraryStartPointIter = TreeTraversal<TestTreePart>.IteratorAt(root, new TreeNodePath(0,1));
            Assert.AreEqual("a.b", arbitraryStartPointIter.Node.Value);
            var found = TreeTraversal<TestTreePart>.All(arbitraryStartPointIter.Node, arbitraryStartPointIter.Node, includeSelf: false).FirstOrDefault();
            Assert.AreEqual("a.b.a", found.Value);

            var iterEnd = TreeTraversal<TestTreePart>.IteratorAt(root, new TreeNodePath(0,1,0));
            Assert.AreEqual("a.b.a", iterEnd.Node.Value);

            Assert.AreEqual(found, iterEnd.Node);
        }

        [TestMethod]
        public void TreeTraversal_FindNextSiblingOrCousin ()
        {
            /*
                      root
                     /  | \
                   a    b  c
                  /|\      |
                 / | \     |
               aa ab  ac  ca
                  /|\      |\
                 / | \     | \
              aba abb abc caa cab
            */

            // sibling
            Assert.AreEqual(a_b_c, TreeTraversal<TestTreePart>.FindNextSiblingOrCousin(a_b_b));

            // cousin
            Assert.AreEqual(a_c, TreeTraversal<TestTreePart>.FindNextSiblingOrCousin(a_b_c));

            // cousin through root
            Assert.AreEqual(root_b, TreeTraversal<TestTreePart>.FindNextSiblingOrCousin(a_c));
        }

        [TestMethod]
        public void TreeTraversal_DescendantCountWorks()
        {
            /*
                      root
                     /  | \
                   a    b  c
                  /|\      |
                 / | \     |
               aa ab  ac  ca
                  /|\      |\
                 / | \     | \
              aba abb abc caa cab
            */

            Assert.AreEqual(12, TreeTraversal<TestTreePart>.CountAllDescendants(root));
            Assert.AreEqual(0, TreeTraversal<TestTreePart>.CountAllDescendants(a_a));
            Assert.AreEqual(6, TreeTraversal<TestTreePart>.CountAllDescendants(root_a));
            Assert.AreEqual(3, TreeTraversal<TestTreePart>.CountAllDescendants(root_c));
        }
    }
}
