namespace AJut.Core.UnitTests
{
    using System;
    using System.Linq;
    using AJut.Algorithms;
    using Microsoft.VisualStudio.TestTools.UnitTesting;


    [TestClass]
    public class AlgorithmTests
    {
        private static readonly Random g_rng = new Random(1);

        [TestMethod]
        public void Algo_QuickSortTest_OddNumberOfEvals ()
        {
            TestQuickSort(new[] { 10, 9, 20, 2, -6 });
        }

        [TestMethod]
        public void Algo_QuickSortTest_EvenNumberOfEvals ()
        {
            TestQuickSort(new[] { 10, 9, 20, 2, -6, 42 });
        }

        [TestMethod]
        public void Algo_QuickSortTest_LargeNumberOfElementsToSortEval ()
        {
            const int kElementCount = 10000;
            int[] elements = new int[kElementCount];
            for (int index = 0; index < kElementCount; ++index)
            {
                elements[index] = g_rng.Next(-888, 888);
            }

            TestQuickSort(elements);
        }

        private void TestQuickSort (int[] evalSet)
        {
            var sortedByMicrosoft = evalSet.ToList();
            sortedByMicrosoft.Sort();

            Sort.QuickSortInplace(evalSet);
            int[] sortedByAjutAlgo = evalSet.ToArray();

            Assert.AreEqual(evalSet.Length, sortedByAjutAlgo.Length);
            for (int index = 0; index < evalSet.Length; ++index)
            {
                Assert.AreEqual(sortedByMicrosoft[index], sortedByAjutAlgo[index]);
            }

        }


        [TestMethod]
        public void Algo_BinarySearch_SearchForContained_EvenNumberOfItems_Works ()
        {
            int[] numbers = new[]
            {
                1, 2, 3, 4, 5, 7
            };

            Assert.AreEqual(2, Search.BinarySearch(numbers, 3));
        }
        [TestMethod]
        public void Algo_BinarySearch_SearchForContained_OddNumberOfItems_Works ()
        {
            int[] numbers = new[]
            {
                1, 2, 4, 5, 7
            };

            Assert.AreEqual(2, numbers.BinarySearchXT(4));
        }


        [TestMethod]
        public void Algo_BinarySearch_SearchForContained_BeginningOfSequence_Works ()
        {
            int[] numbers = new[]
            {
                1, 2, 4, 5, 7
            };

            Assert.AreEqual(0, numbers.BinarySearchXT(1));
        }
        [TestMethod]
        public void Algo_BinarySearch_SearchForContained_EndOfSequence_Works ()
        {
            int[] numbers = new[]
            {
                1, 2, 4, 5, 7
            };

            Assert.AreEqual(4, numbers.BinarySearchXT(7));
        }

    }
}
