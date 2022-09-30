namespace AJut.Core.UnitTests
{
    using System;
    using System.Linq;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class StringXT_Tests
    {
        private static int g_hashcodeForTest = 696679732; // generated from string "This works well"

        [TestMethod]
        public void Extensions_GenerateStableHashCode_ProducesExpectedResults ()
        {
            Assert.AreEqual(g_hashcodeForTest, "This works well".GenerateStableHashCode());

            // Silly but still, should be fast and easy to verify
            Assert.AreNotEqual(g_hashcodeForTest, "this works well".GenerateStableHashCode());
            Assert.AreNotEqual(g_hashcodeForTest, "Txis works well".GenerateStableHashCode());
            Assert.AreNotEqual(g_hashcodeForTest, "Thisworkswell".GenerateStableHashCode());
        }

        [TestMethod]
        public void Extensions_Replace_ProducesExpectedResults()
        {
            string source = "AJ is not awesome!";
            Assert.AreEqual("AJ is super awesome!", source.Replace(6, 8, "super"));
        }


        [TestMethod]
        public void Extensions_Replace_ReplacingLastCharsProducesExpectedResults()
        {
            string source = "Hello Dude!";
            Assert.AreEqual("Hello dude.", source.Replace(6, 10, "dude."));
        }


        [TestMethod]
        public void Extensions_SubstringIndex_ProducesExpectedResults()
        {
            string source = "Dude I am so super-duper smart!";
            Assert.AreEqual("super-duper", source.SubstringWithIndices(13, 23));
        }

        [TestMethod]
        public void Extensions_FindIndexRange_Simple()
        {
            string source = "This (is) sweet.";
            Tuple<int,int> range = source.FindIndexRange(0, '(', ')');
            Assert.AreEqual(5, range.Item1);
            Assert.AreEqual(8, range.Item2);
        }

        [TestMethod]
        public void Extensions_FindIndexRange_Invalid()
        {
            string source = "This (is) sweet.";
            Tuple<int, int> range = source.FindIndexRange(0, '[', ']');
            Assert.IsNull(range);
        }

        [TestMethod]
        public void Extensions_FindIndexRange_Complex()
        {
            string source = "func test(r1, r2, sweet(3,4), sweet(5,6), 8)";
            Tuple<int, int> range = source.FindIndexRange(0, '(', ')');
            Assert.AreEqual(9, range.Item1);
            Assert.AreEqual(43, range.Item2);
        }

        [TestMethod]
        public void Extensions_FindCapitalsEn_BasicTest()
        {
            string eval = "This Is A Test";
            var found = eval.FindCapitalsEn().ToArray();
            Assert.AreEqual(4, found.Length);
            Assert.AreEqual('T', found[0]);
            Assert.AreEqual('I', found[1]);
            Assert.AreEqual('A', found[2]);
            Assert.AreEqual('T', found[3]);
        }

        [TestMethod]
        public void Extensions_FindCapitalsEn_ContiguousUppers ()
        {
            string eval = "And THIS is A Test";
            var found = eval.FindCapitalsEn().ToArray();
            Assert.AreEqual(7, found.Length);
            Assert.AreEqual('A', found[0]);
            Assert.AreEqual('T', found[1]);
            Assert.AreEqual('H', found[2]);
            Assert.AreEqual('I', found[3]);
            Assert.AreEqual('S', found[4]);
            Assert.AreEqual('A', found[5]);
            Assert.AreEqual('T', found[6]);
        }

    }
}
