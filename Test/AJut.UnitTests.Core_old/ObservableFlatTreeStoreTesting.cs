namespace AJut.UnitTests.Core
{
    using AJut.Storage;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Runtime.InteropServices;

    [TestClass]
    public class ObservableFlatTreeStoreTesting
    {
        [TestMethod]
        public void WorksWithPreConstructedTree ()
        {
            var a = new TreeNode("A");
            var b = a.AddChild("B");
            var c = a.AddChild("C");

            var d = c.AddChild("D");
            var e = c.AddChild("E");
            var f = e.AddChild("F");
            var g = e.AddChild("G");

            var h = c.AddChild("H");

            var flat = new ObservableFlatTreeStore();
            flat.RootNode = a;

            Assert.AreEqual(8, flat.Count);
            Assert.AreEqual(a, flat[0]);
            Assert.AreEqual(b, flat[1]);
            Assert.AreEqual(c, flat[2]);
            Assert.AreEqual(d, flat[3]);
            Assert.AreEqual(e, flat[4]);
            Assert.AreEqual(f, flat[5]);
            Assert.AreEqual(g, flat[6]);
            Assert.AreEqual(h, flat[7]);
        }

        [TestMethod]
        public void WorksWhenTreeConstructedAfterRootIsSet ()
        {
            var flat = new ObservableFlatTreeStore();

            var a = new TreeNode("A");
            flat.RootNode = a;
            var b = a.AddChild("B");
            var c = a.AddChild("C");

            var d = c.AddChild("D");
            var e = c.AddChild("E");
            var f = e.AddChild("F");
            var g = e.AddChild("G");

            var h = c.AddChild("H");


            Assert.AreEqual(8, flat.Count);
            Assert.AreEqual(a.Value, ((TreeNode)flat[0]).Value);
            Assert.AreEqual(b.Value, ((TreeNode)flat[1]).Value);
            Assert.AreEqual(c.Value, ((TreeNode)flat[2]).Value);
            Assert.AreEqual(d.Value, ((TreeNode)flat[3]).Value);
            Assert.AreEqual(e.Value, ((TreeNode)flat[4]).Value);
            Assert.AreEqual(f.Value, ((TreeNode)flat[5]).Value);
            Assert.AreEqual(g.Value, ((TreeNode)flat[6]).Value);
            Assert.AreEqual(h.Value, ((TreeNode)flat[7]).Value);
        }

        [TestMethod]
        public void WorksWhenInserting_InsertAtIndexAndWithExistingChild ()
        {
            /*               A
             *            /  |  \
             *          B    C    D
             *        / | \       | \
             *      E   F  G      H  I
             *          |         |
             *          J         K
             * 
             * Expected flat tree order:
             * [A][B][E][F][J][G][C][D][H][K][I]
             * */
            var flat = new ObservableFlatTreeStore();

            var a = new TreeNode("A");
            flat.RootNode = a;

            // =========== Construct the normie tree ==============
            var b = a.AddChild("B");
            var e = b.AddChild("E");
            var g = b.AddChild("G");

            var d = a.AddChild("D");
            var i = d.AddChild("I");

            // =========== Asserting Normies are in place ==============
            string flatTreeStr = "[" + String.Join("][", flat.OfType<TreeNode>().Select(n => n.Value)) + "]";
            Console.WriteLine("Expecting: [A][B][E][G][D][I]");
            Console.WriteLine("Actually : " + flatTreeStr);
            Assert.AreEqual(6, flat.Count, flatTreeStr);

            Assert.AreEqual(a.Value, ((TreeNode)flat[0]).Value, flatTreeStr);
            Assert.AreEqual(b.Value, ((TreeNode)flat[1]).Value, flatTreeStr);
            Assert.AreEqual(e.Value, ((TreeNode)flat[2]).Value, flatTreeStr);
            Assert.AreEqual(g.Value, ((TreeNode)flat[3]).Value, flatTreeStr);
            Assert.AreEqual(d.Value, ((TreeNode)flat[4]).Value, flatTreeStr);
            Assert.AreEqual(i.Value, ((TreeNode)flat[5]).Value, flatTreeStr);

            Console.WriteLine("Done adding the normies:");
            Console.WriteLine("> " + flatTreeStr);
            Console.WriteLine();
            Console.WriteLine("Now inserting [C][F][H][J][K]");

            // =========== Construct the odd tree ==============
            var c = a.InsertChild(1, "C");

            var f = new TreeNode("F");
            var j = f.AddChild("J");
            b.InsertChild(1, f);

            var h = d.InsertChild(0, "H");
            var k = h.InsertChild(0, "K");

            // =========== Asserting the odd tree is in place ==============
            flatTreeStr = "[" + String.Join("][", flat.OfType<TreeNode>().Select(n => n.Value)) + "]";
            Console.WriteLine("Expecting: [A][B][E][F][J][G][C][D][H][K][I]");
            Console.WriteLine("Actually : " + flatTreeStr);
            Assert.AreEqual(11, flat.Count, flatTreeStr);
            Assert.AreEqual(a.Value, ((TreeNode)flat[0]).Value, flatTreeStr);
            Assert.AreEqual(b.Value, ((TreeNode)flat[1]).Value, flatTreeStr);
            Assert.AreEqual(e.Value, ((TreeNode)flat[2]).Value, flatTreeStr);
            Assert.AreEqual(f.Value, ((TreeNode)flat[3]).Value, flatTreeStr);
            Assert.AreEqual(j.Value, ((TreeNode)flat[4]).Value, flatTreeStr);
            Assert.AreEqual(g.Value, ((TreeNode)flat[5]).Value, flatTreeStr);
            Assert.AreEqual(c.Value, ((TreeNode)flat[6]).Value, flatTreeStr);
            Assert.AreEqual(d.Value, ((TreeNode)flat[7]).Value, flatTreeStr);
            Assert.AreEqual(h.Value, ((TreeNode)flat[8]).Value, flatTreeStr);
            Assert.AreEqual(k.Value, ((TreeNode)flat[9]).Value, flatTreeStr);
            Assert.AreEqual(i.Value, ((TreeNode)flat[10]).Value, flatTreeStr);
        }

        [TestMethod]
        public void LargeTreeTest ()
        {
            TreeNode root = new TreeNode("Root");
            var flat = new ObservableFlatTreeStore();
            flat.RootNode = root;

            const int kFirstBatchCount = 200;
            const int kSecondBatchCount = 2000;
            const int kThirdBatchCount = 200;

            // 100% increase from batch 1 → 2 - this due to reallocations of OC - it's meant as an acceptable maximum
            const double kAcceptablePercentIncrease_1_to_2 = 1.0;

            // 75% increase from batch 2 → 3
            const double kAcceptablePercentIncrease_2_to_3 = 0.75;

            // 100% increase from batch 1 → 3
            const double kAcceptablePercentIncrease_1_to_3 = 1.00;

            Kernal32_Timer timer = new Kernal32_Timer();
            int itemCount = kFirstBatchCount;

            timer.Start();
            var leaves = this.ConstructTree(root, itemCount);
            double seconds = timer.Stop();

            double averageSeconds1 = seconds / kFirstBatchCount;
            Console.WriteLine($"Run 1 => Took {seconds} seconds to add {kFirstBatchCount}");
            Console.WriteLine($"Run 1 => Average of {averageSeconds1} seconds per item");
            Console.WriteLine("-----------------------------------------");

            timer.Start();
            ConstructTree(leaves, itemCount, itemCount += kSecondBatchCount);
            seconds = timer.Stop();
            double averageSeconds2 = seconds / kSecondBatchCount;
            double increase_1_to_2 = (averageSeconds2 - averageSeconds1) / averageSeconds2;

            Console.WriteLine($"Run 2 => Adding an additional {kSecondBatchCount} took {seconds}");
            Console.WriteLine($"Run 2 => Average of {averageSeconds2} seconds per item");
            Console.WriteLine($"Run 2 => Change in percent, {increase_1_to_2 * 100.0:N2}%");
            Console.WriteLine("-----------------------------------------");
            Assert.IsTrue(increase_1_to_2 < kAcceptablePercentIncrease_1_to_2, $"Average seconds per add increased from frist {kFirstBatchCount} by over {kAcceptablePercentIncrease_1_to_2 * 100.0}%");

            timer.Start();
            ConstructTree(leaves, itemCount, itemCount += kThirdBatchCount);
            seconds = timer.Stop();
            double averageSeconds3 = seconds / kThirdBatchCount;
            double increase_1_to_3 = (averageSeconds3 - averageSeconds1) / averageSeconds3;
            double increase_2_to_3 = (averageSeconds3 - averageSeconds2) / averageSeconds3;

            Console.WriteLine($"Run 3 => Adding an additional {kThirdBatchCount} took {seconds}");
            Console.WriteLine($"Run 3 => Average of {averageSeconds3} seconds per item");
            Console.WriteLine($"Run 3 => % change in average from the first batch of {kFirstBatchCount}   -> {increase_1_to_3 * 100.0:N2}%");
            Console.WriteLine($"Run 3 => % change in average from the second batch of {kSecondBatchCount} -> {increase_2_to_3 * 100.0:N2}%");
            Console.WriteLine("-----------------------------------------");

            Assert.IsTrue(increase_2_to_3 < kAcceptablePercentIncrease_2_to_3, $"Average seconds per add increased from second {kSecondBatchCount} by over {kAcceptablePercentIncrease_1_to_3 * 100.0}%");
            Assert.IsTrue(increase_1_to_3 < kAcceptablePercentIncrease_1_to_3, $"Average seconds per add increased from second {kSecondBatchCount} by over {kAcceptablePercentIncrease_1_to_3 * 100.0}%");
        }

        private Stack<TreeNode> ConstructTree (TreeNode root, int count)
        {
            Stack<TreeNode> leaves = new Stack<TreeNode>();
            leaves.Push(root);
            ConstructTree(leaves, 1, count);
            return leaves;
        }

        private void ConstructTree (Stack<TreeNode> leaves, int itemIndex, int stopAt)
        {
            while (true)
            {
                TreeNode node = leaves.Pop();
                leaves.Push(node.AddChild($"Node {itemIndex++}"));
                if (itemIndex >= stopAt)
                {
                    break;
                }

                leaves.Push(node.AddChild($"Node {itemIndex++}"));
                if (itemIndex >= stopAt)
                {
                    break;
                }
            }
        }

        private TreeNode ConstructTree()
        {
            var a = new TreeNode("A");
            a.AddChild("B");
            var c = a.AddChild("C");

            c.AddChild("D");
            var e = c.AddChild("E");
            e.AddChild("F");
            e.AddChild("G");

            c.AddChild("H");
            return a;
        }

        [TestMethod]
        public void ClearWorks ()
        {
            var a = this.ConstructTree();
            var flat = new ObservableFlatTreeStore();
            flat.RootNode = a;
            
            // Before clear so we can gurantee change, even at the cost of adding an assert that is elsewhere
            Assert.AreEqual(8, flat.Count);
            
            flat.Clear();
            Assert.AreEqual(0, flat.Count);

            // Add child to see if lingereing references remain
            a.AddChild("X");
            Assert.AreEqual(0, flat.Count);
        }

        [TestMethod]
        public void WorksWhenRootIsReset ()
        {
            var flat = new ObservableFlatTreeStore();
            flat.RootNode = this.ConstructTree();

            Assert.AreEqual(8, flat.Count);

            var a2 = new TreeNode("A2");
            var b2 = a2.AddChild("B2");
            var c2 = b2.AddChild("C2");
            var d2 = a2.AddChild("D2");
            flat.RootNode = a2;

            Assert.AreEqual(4, flat.Count);
            Assert.AreEqual(a2, flat[0]);
            Assert.AreEqual(b2, flat[1]);
            Assert.AreEqual(c2, flat[2]);
            Assert.AreEqual(d2, flat[3]);
        }

        [TestMethod]
        public void IncludeRootFalse_SetBefore ()
        {
            var flat = new ObservableFlatTreeStore();
            flat.IncludeRoot = false;
            flat.RootNode = this.ConstructTree();
            Assert.AreEqual(7, flat.Count);
            Assert.AreEqual("B", ((TreeNode)flat[0]).Value);
            Assert.AreEqual("C", ((TreeNode)flat[1]).Value);
            Assert.AreEqual("D", ((TreeNode)flat[2]).Value);
            Assert.AreEqual("E", ((TreeNode)flat[3]).Value);
            Assert.AreEqual("F", ((TreeNode)flat[4]).Value);
            Assert.AreEqual("G", ((TreeNode)flat[5]).Value);
            Assert.AreEqual("H", ((TreeNode)flat[6]).Value);
        }

        [TestMethod]
        public void IncludeRootFalse_SetAfter ()
        {
            var flat = new ObservableFlatTreeStore();
            flat.RootNode = this.ConstructTree();
            flat.IncludeRoot = false;
            Assert.AreEqual(7, flat.Count);
            Assert.AreEqual("B", ((TreeNode)flat[0]).Value);
            Assert.AreEqual("C", ((TreeNode)flat[1]).Value);
            Assert.AreEqual("D", ((TreeNode)flat[2]).Value);
            Assert.AreEqual("E", ((TreeNode)flat[3]).Value);
            Assert.AreEqual("F", ((TreeNode)flat[4]).Value);
            Assert.AreEqual("G", ((TreeNode)flat[5]).Value);
            Assert.AreEqual("H", ((TreeNode)flat[6]).Value);
        }

        [TestMethod]
        public void RemoveChild_Works ()
        {
            /*
             *          root
             *          / \
             *         a   b
             *            / \
             *           c   d
             *               |
             *               e
             * */
            TreeNode root = new TreeNode("root");
            var a = root.AddChild("a");
            var b = root.AddChild("b");

            var c = b.AddChild("c");
            var d = b.AddChild("d");
            var e = d.AddChild("e");

            ObservableFlatTreeStore storage = new ObservableFlatTreeStore();
            storage.RootNode = root;

            // Full tree test
            Assert.AreEqual(6, storage.Count);

            Assert.AreEqual(root, storage[0]);
            Assert.AreEqual(a, storage[1]);
            Assert.AreEqual(b, storage[2]);
            Assert.AreEqual(c, storage[3]);
            Assert.AreEqual(d, storage[4]);
            Assert.AreEqual(e, storage[5]);

            // Remove C
            Assert.IsTrue(b.RemoveChild(c));
            Assert.AreEqual(5, storage.Count);

            Assert.AreEqual(root, storage[0]);
            Assert.AreEqual(a, storage[1]);
            Assert.AreEqual(b, storage[2]);
            Assert.AreEqual(d, storage[3]);
            Assert.AreEqual(e, storage[4]);

            // Remove D & by proxy child E
            Assert.IsTrue(b.RemoveChild(d));
            Assert.AreEqual(3, storage.Count);

            Assert.AreEqual(root, storage[0]);
            Assert.AreEqual(a, storage[1]);
            Assert.AreEqual(b, storage[2]);
        }
    }

    public class Kernal32_Timer
    {
        private long m_frequency;
        private long m_startTime;

        [DllImport("Kernel32.dll")]
        private static extern bool QueryPerformanceCounter (out long lpPerformanceCount);

        [DllImport("Kernel32.dll")]
        private static extern bool QueryPerformanceFrequency (out long lpFrequency);

        public Kernal32_Timer ()
        {
            QueryPerformanceFrequency(out m_frequency);
        }
        public void Start ()
        {
            QueryPerformanceCounter(out m_startTime);
        }

        public double Stop ()
        {
            QueryPerformanceCounter(out long endTime);
            return (double)(endTime - m_startTime) / (double)m_frequency;
        }
    }

    [DebuggerDisplay("{Value}")]
    public class TreeNode : ObservableTreeNode<TreeNode>
    {
        public string Value { get; set; }
        public TreeNode (string value)
        {
            this.Value = value;
        }

        public TreeNode InsertChild (int index, string value)
        {
            var node = new TreeNode(value);
            this.InsertChild(index, node);
            return node;
        }

        public TreeNode AddChild (string value)
        {
            var node = new TreeNode(value);
            this.AddChild(node);
            return node;
        }

        public override string ToString () => this.Value;
    }
}
