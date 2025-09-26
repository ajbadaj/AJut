namespace AJut.Core.UnitTests
{
    using AJut.Storage;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using System.Collections.Generic;

    [TestClass]
    public class MemLibraryTests
    {
        [TestInitialize]
        public void Initialize()
        {
            MemLibrary.Setup(3, true);
        }

        [TestMethod]
        public void BasicUsageTest()
        {
            List<int> list = MemLibrary.Take<List<int>>();
            Assert.IsNotNull(list);
            Assert.IsTrue(MemLibrary.Return(list));
        }


        [TestMethod]
        public void PastCapacityTest()
        {
            var a = MemLibrary.Take<List<int>>();
            var b = MemLibrary.Take<List<int>>();
            var c = MemLibrary.Take<List<int>>();
            var d = MemLibrary.Take<List<int>>();
            var e = MemLibrary.Take<List<int>>();
            
            // Should have made them
            Assert.IsNotNull(a);
            Assert.IsNotNull(b);
            Assert.IsNotNull(c);
            Assert.IsNotNull(d);
            Assert.IsNotNull(e);

            // Should accept the first 3 (up to capacity)
            Assert.IsTrue(MemLibrary.Return(a));
            Assert.IsTrue(MemLibrary.Return(b));
            Assert.IsTrue(MemLibrary.Return(c));

            // Should reject the final 2 (past capacity)
            Assert.IsFalse(MemLibrary.Return(d));
            Assert.IsFalse(MemLibrary.Return(e));
        }

        [TestMethod]
        public void TestingCheckout()
        {
            var a = MemLibrary.Checkout<List<int>>();
            var b = MemLibrary.Checkout<List<int>>();
            var c = MemLibrary.Checkout<List<int>>();
            var d = MemLibrary.Take<List<int>>();

            // Should have made them
            Assert.IsNotNull(a.Value);
            Assert.IsNotNull(b.Value);
            Assert.IsNotNull(c.Value);
            Assert.IsNotNull(d);

            a.Dispose();
            b.Dispose();
            c.Dispose();

            // With a, b, c returned automatically via dispose - it should be too full to accept d
            Assert.IsFalse(MemLibrary.Return(d));
        }

        [TestMethod]
        public void TestingConciergeLists()
        {
            MemLibraryListConcierge<int> listMaker = new MemLibraryListConcierge<int> { Capacity = 8 };
            List<int> list = listMaker.TakeFromMemLibrary();
            Assert.AreEqual(8, list.Capacity);

            list.Add(1);
            list.Add(2);
            list.Add(3);

            MemLibrary.Return(list);
            list = listMaker.TakeFromMemLibrary();
            Assert.AreEqual(8, list.Capacity);
            Assert.AreEqual(0, list.Count);
        }
    }
}
