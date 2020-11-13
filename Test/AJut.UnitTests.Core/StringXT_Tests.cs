namespace AJut.UnitTests.Core
{
    using System;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class StringXT_Tests
    {
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

    }
}
