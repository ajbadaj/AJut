namespace AJut.Core.UnitTests
{
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class ListExtensionTests
    {
        [TestMethod]
        public void ListXT_Randomize ()
        {
            // Fill test with 0-99
            List<int> test = new List<int>(100);
            for (int x = 0; x < 100; ++x)
            {
                test.Add(x);
            }

            // Copy test
            List<int> testCopy = new List<int>(test);
            test.Randomize();
            Assert.AreEqual(testCopy.Count, test.Count);
            Assert.IsTrue(test.All(testCopy.Contains));
            Assert.AreNotEqual(test, testCopy);

            bool anyNotEqual = false;
            for (int index = 0; index < test.Count; ++index)
            {
                if (test[index] != testCopy[index])
                {
                    anyNotEqual = true;
                    break;
                }
            }

            // Technically it is possible for this to fail and it still have worked, but not too likely
            Assert.IsTrue(anyNotEqual);
        }

        [TestMethod]
        public void ListXT_Randomize_Seed ()
        {
            // Fill test with 0-99
            List<int> test = new List<int>(100);
            for (int x = 0; x < 100; ++x)
            {
                test.Add(x);
            }

            // Copy t est
            List<int> testCopy1 = new List<int>(test);
            List<int> testCopy2 = new List<int>(test);
            testCopy1.Randomize(8);
            testCopy2.Randomize(8);

            Assert.AreEqual(test.Count, testCopy1.Count);
            Assert.AreEqual(test.Count, testCopy2.Count);


            Assert.IsTrue(test.All(testCopy1.Contains));
            Assert.IsTrue(test.All(testCopy2.Contains));

            Assert.AreNotEqual(test, testCopy1);
            Assert.AreNotEqual(test, testCopy2);

            // Did actually randomize from test
            bool anyNotEqual = false;
            for (int index = 0; index < test.Count; ++index)
            {
                if (test[index] != testCopy1[index])
                {
                    anyNotEqual = true;
                    break;
                }
            }

            Assert.IsTrue(anyNotEqual);

            // Did actually randomize from test
            anyNotEqual = false;
            for (int index = 0; index < test.Count; ++index)
            {
                if (test[index] != testCopy2[index])
                {
                    anyNotEqual = true;
                    break;
                }
            }

            Assert.IsTrue(anyNotEqual);

            // Are new sequences a match
            for (int index = 0; index < test.Count; ++index)
            {
                Assert.AreEqual(testCopy1[index], testCopy2[index]);
            }
        }

        [TestMethod]
        public void ListXT_Reverse ()
        {
            List<int> test = new List<int> { 0, 1, 2, 3, 4, 5, 6 };
            List<int> reversed = new List<int>(test);
            reversed.Reverse();
            int index = 0;
            foreach (int item in test.EnumerateReversed())
            {
                Assert.AreEqual(reversed[index++], item);
            }
        }

        [TestMethod]
        public void ListXT_Reverse_AtOffset ()
        {
            List<int> test = new List<int> { 0, 1, 2, 3, 4, 5, 6 };
            List<int> reversed = new List<int> { 4, 3, 2, 1, 0 };
            int index = 0;
            foreach (int item in test.EnumerateReversed(2))
            {
                Assert.AreEqual(reversed[index++], item);
            }
        }

        [TestMethod]
        public void ListXT_BinarySearch_SearhForContained_CustomSearchers_Works ()
        {
            List<int> numbers = new List<int>
            {
                1, 2, 4, 5, 7
            };

            Assert.AreEqual(4, numbers.BinarySearchXT(v => 7.CompareTo(v)));
        }

        [TestMethod]
        public void ListXT_BinarySearch_SearchForMissing_CustomSearchers_Works ()
        {
            List<int> numbers = new List<int>
            {
                1, 2, 4, 5, 7
            };

            Assert.AreEqual(~4, numbers.BinarySearchXT(v => 6.CompareTo(v)));
        }

        [TestMethod]
        public void ListXT_InsertSorted_NoParamsInt_Works ()
        {
            List<int> numbers = new List<int>();
            Assert.AreEqual(0, numbers.InsertSorted(0));
            Assert.AreEqual(1, numbers.Count);

            Assert.AreEqual(1, numbers.InsertSorted(3));
            Assert.AreEqual(2, numbers.Count);

            Assert.AreEqual(0, numbers.InsertSorted(-1));
            Assert.AreEqual(3, numbers.Count);

            Assert.AreEqual(3, numbers.InsertSorted(5));
            Assert.AreEqual(4, numbers.Count);

            Assert.AreEqual(2, numbers.InsertSorted(1));
            Assert.AreEqual(5, numbers.Count);

            Assert.AreEqual(-1, numbers[0]);
            Assert.AreEqual(0, numbers[1]);
            Assert.AreEqual(1, numbers[2]);
            Assert.AreEqual(3, numbers[3]);
            Assert.AreEqual(5, numbers[4]);
        }

        [TestMethod]
        public void ListXT_InsertSorted_Params_Works ()
        {
            List<int> numbers = new List<int>();
            Assert.AreEqual(0, numbers.InsertSorted(0, _NumberComparer));
            Assert.AreEqual(1, numbers.Count);

            Assert.AreEqual(1, numbers.InsertSorted(3, _NumberComparer));
            Assert.AreEqual(2, numbers.Count);

            Assert.AreEqual(0, numbers.InsertSorted(-1, _NumberComparer));
            Assert.AreEqual(3, numbers.Count);

            Assert.AreEqual(3, numbers.InsertSorted(5, _NumberComparer));
            Assert.AreEqual(4, numbers.Count);

            Assert.AreEqual(2, numbers.InsertSorted(1, _NumberComparer));
            Assert.AreEqual(5, numbers.Count);

            Assert.AreEqual(-1, numbers[0]);
            Assert.AreEqual(0, numbers[1]);
            Assert.AreEqual(1, numbers[2]);
            Assert.AreEqual(3, numbers[3]);
            Assert.AreEqual(5, numbers[4]);

            int _NumberComparer (int inserting, int existing)
            {
                return inserting - existing;
            }
        }

        [TestMethod]
        public void ListXT_InsertSorted_ProducesSameResultAsDotNetListSort ()
        {
            List<int> dotnetSorted = new List<int>();
            List<int> insertSorted = new List<int>();

            insertSorted.InsertSorted(0, _NumberComparer);
            insertSorted.InsertSorted(3, _NumberComparer);
            insertSorted.InsertSorted(-1, _NumberComparer);
            insertSorted.InsertSorted(5, _NumberComparer);
            insertSorted.InsertSorted(1, _NumberComparer);

            dotnetSorted.Add(0);
            dotnetSorted.Add(3);
            dotnetSorted.Add(-1);
            dotnetSorted.Add(5);
            dotnetSorted.Add(1);
            dotnetSorted.Sort(_NumberComparer);

            Assert.AreEqual(dotnetSorted[0], insertSorted[0]);
            Assert.AreEqual(dotnetSorted[1], insertSorted[1]);
            Assert.AreEqual(dotnetSorted[2], insertSorted[2]);
            Assert.AreEqual(dotnetSorted[3], insertSorted[3]);
            Assert.AreEqual(dotnetSorted[4], insertSorted[4]);

            int _NumberComparer (int inserting, int existing)
            {
                return inserting - existing;
            }
        }
    }
}
