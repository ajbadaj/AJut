namespace AJut.UnitTests.Core
{
    using AJut.Storage;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class MultiMapTests
    {
        [TestMethod]
        public void MultiMap_BasicTest ()
        {
            MultiMap<int, string> map = new MultiMap<int, string>();
            map[0].Add("Test");
            Assert.AreEqual(1, map[0].Count);

            map.Add(0, "Test2");
            Assert.AreEqual(2, map[0].Count);

            Assert.IsTrue(map.Remove(0, "Test"));
            Assert.AreEqual(1, map[0].Count);

            map[5].Add("Test");
            Assert.AreEqual(1, map[0].Count);
            Assert.AreEqual(1, map[5].Count);

            Assert.AreEqual(2, map.GetTotalElementCount());
        }
    }
}
