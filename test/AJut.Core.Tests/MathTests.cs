namespace AJut.Core.UnitTests
{
    using System.Collections;
    using System.Collections.Generic;
    using AJut.MathUtilities;
    using Microsoft.VisualStudio.TestTools.UnitTesting;


    [TestClass]
    public class MathTests
    {
        [TestMethod]
        public void Math_Lerp_Value ()
        {
            Assert.AreEqual(3, Lerp.Value(1, 5, 0.5));
            Assert.AreEqual(5, Lerp.Value(1, 9, 0.5));
        }

        [TestMethod]
        public void Math_Lerp_Percent ()
        {
            Assert.AreEqual(0.5, Lerp.Percent(1, 5, 3));
            Assert.AreEqual(0.5, Lerp.Percent(1, 9, 5));
        }

        [TestMethod]
        public void Math_Lerp_Start ()
        {
            Assert.AreEqual(1, Lerp.Start(5, 3, 0.5));
            Assert.AreEqual(1, Lerp.Start(9, 5, 0.5));
        }

        [TestMethod]
        public void Math_Lerp_End ()
        {
            Assert.AreEqual(5, Lerp.End(1, 3, 0.5));
            Assert.AreEqual(9, Lerp.End(1, 5, 0.5));
        }

        [TestMethod]
        public void Math_Calculate_LCM()
        {
            Assert.AreEqual(600, Calculate.LeastCommonMultiple(100, 100, 300, 600));
            Assert.AreEqual(-600, Calculate.LeastCommonMultiple(100, -100, 300, 600));
        }

        [TestMethod]
        public void Math_Calculate_GCD ()
        {
            Assert.AreEqual(100, Calculate.GreatestCommonDenominator(100, 300, 300, 500));
        }

        [TestMethod]
        public void Math_Calculate_Mean ()
        {
            Assert.AreEqual(20, Calculate.Mean(10, 20, 30));
            IEnumerable<int> toAverage = (IEnumerable<int>)(new int[] { 10, 20, 30 });
            Assert.AreEqual(20, Calculate.Mean(toAverage));
        }

        [TestMethod]
        public void Math_Calculate_MinAndMaxValuesIn ()
        {
            IEnumerable<int> minMax = new[] { 600, 10, 20, 30, -600 };
            Calculate.MinAndMaxValuesIn(minMax, out int min, out int max);
            Assert.AreEqual(-600, min);
            Assert.AreEqual(600, max);
        }

        [TestMethod]
        public void Math_Calculate_QuickEvaluateIfSumIsLessThan()
        {
            var earlyOutTest = new VisitWatchEnumeration<int>(10, 20, 30, 40, 50);
            Assert.IsFalse(Calculate.QuickEvaluateIfSumIsLessThan(earlyOutTest, 30));
            Assert.AreEqual(1, earlyOutTest.LastVisitedIndex);

            var madeItToEndTest = new VisitWatchEnumeration<int>(10, 20, 30, 40, 50);
            Assert.IsTrue(Calculate.QuickEvaluateIfSumIsLessThan(madeItToEndTest, 2000));
            Assert.AreEqual(5, madeItToEndTest.LastVisitedIndex);
        }

        [TestMethod]
        public void Math_Calculate_QuickEvaluateIfSumIsLessThanOrEqualTo()
        {
            var earlyOutTest = new VisitWatchEnumeration<int>(10, 20, 30, 40, 50);
            Assert.IsFalse(Calculate.QuickEvaluateIfSumIsLessThanOrEqualTo(earlyOutTest, 30));
            Assert.AreEqual(2, earlyOutTest.LastVisitedIndex);

            var madeItToEndTest = new VisitWatchEnumeration<int>(10, 20, 30, 40, 50);
            Assert.IsTrue(Calculate.QuickEvaluateIfSumIsLessThanOrEqualTo(madeItToEndTest, 2000));
            Assert.AreEqual(5, madeItToEndTest.LastVisitedIndex);
        }

        public class VisitWatchEnumeration<T> : IEnumerable<T>
        {
            T[] m_items;
            public VisitWatchEnumeration(params T[] items)
            {
                m_items = items;
            }

            public int LastVisitedIndex { get; set; } = -1;

            public IEnumerator<T> GetEnumerator () => new VisitWatchEnumerator(this);
            IEnumerator IEnumerable.GetEnumerator () => new VisitWatchEnumerator(this);

            private class VisitWatchEnumerator : IEnumerator<T>
            {
                private VisitWatchEnumeration<T> m_owner;
                public VisitWatchEnumerator (VisitWatchEnumeration<T> owner)
                {
                    m_owner = owner;
                }

                public T Current => m_owner.m_items[m_owner.LastVisitedIndex];

                object IEnumerator.Current => this.Current;

                public void Dispose ()
                {
                    m_owner = null;
                }

                public bool MoveNext ()
                {
                    if (++m_owner.LastVisitedIndex >= m_owner.m_items.Length)
                    {
                        return false;
                    }

                    return true;
                }

                public void Reset ()
                {
                    m_owner.LastVisitedIndex = 0;
                }
            }
        }
    }
}
