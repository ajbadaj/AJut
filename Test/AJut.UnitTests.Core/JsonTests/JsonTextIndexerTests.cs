namespace AJut.UnitTests.Core.AJson
{
    using AJut.Text.AJson;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class JsonTextIndexerTests
    {
        const string kTestText = "test { test[ test, test ], { test: [value, { key : value2 } ] }, \"test\" }";
        //                             5    11     17     24      33      41      49       58

        [TestMethod]
        public void AJson_TextIndexer_TestNext_OpenCurly()
        {
            JsonTextIndexer text = new JsonTextIndexer(kTestText);

            Assert.AreEqual(5, text.Next('{', 0));
            Assert.AreEqual(27, text.Next('{', 6));
            Assert.AreEqual(43, text.Next('{', 28));
            Assert.AreEqual(-1, text.Next('{', 44));
        }

        [TestMethod]
        public void AJson_TextIndexer_TestNext_CloseCurly()
        {
            JsonTextIndexer text = new JsonTextIndexer(kTestText);

            Assert.AreEqual(58, text.Next('}', 0));
            Assert.AreEqual(62, text.Next('}', 59));
            Assert.AreEqual(72, text.Next('}', 63));
            Assert.AreEqual(-1, text.Next('}', 73));
        }

        [TestMethod]
        public void AJson_TextIndexer_TestNext_SquareOpenBracket()
        {
            JsonTextIndexer text = new JsonTextIndexer(kTestText);

            Assert.AreEqual(11, text.Next('[', 0));
            Assert.AreEqual(35, text.Next('[', 12));
            Assert.AreEqual(-1, text.Next('[', 36));
        }

        [TestMethod]
        public void AJson_TextIndexer_TestNext_SquareCloseBracket()
        {
            JsonTextIndexer text = new JsonTextIndexer(kTestText);

            Assert.AreEqual(24, text.Next(']', 0));
            Assert.AreEqual(60, text.Next(']', 25));
            Assert.AreEqual(-1, text.Next(']', 61));
        }

        [TestMethod]
        public void AJson_TextIndexer_TestNext_Comma()
        {
            JsonTextIndexer text = new JsonTextIndexer(kTestText);

            Assert.AreEqual(17, text.Next(',', 0));
            Assert.AreEqual(25, text.Next(',', 18));
            Assert.AreEqual(41, text.Next(',', 26));
            Assert.AreEqual(63, text.Next(',', 42));
            Assert.AreEqual(-1, text.Next(',', 64));
        }

        [TestMethod]
        public void AJson_TextIndexer_TestNext_Colon()
        {
            JsonTextIndexer text = new JsonTextIndexer(kTestText);

            Assert.AreEqual(33, text.Next(':', 0));
            Assert.AreEqual(49, text.Next(':', 34));
        }

        [TestMethod]
        public void AJson_TextIndexer_TestNext_Quote()
        {
            JsonTextIndexer text = new JsonTextIndexer(kTestText);

            Assert.AreEqual(65, text.Next('\"', 0));
            Assert.AreEqual(70, text.Next('\"', 66));
        }

        [TestMethod]
        public void AJson_TextIndexer_TestNext_Any()
        {
            JsonTextIndexer text = new JsonTextIndexer(kTestText);

            Assert.AreEqual(5,  text.NextAny(0));
            Assert.AreEqual(11, text.NextAny(6));
            Assert.AreEqual(17, text.NextAny(12));
            Assert.AreEqual(24, text.NextAny(18));
            Assert.AreEqual(25, text.NextAny(25));
            Assert.AreEqual(27, text.NextAny(26));
            Assert.AreEqual(33, text.NextAny(28));
            Assert.AreEqual(35, text.NextAny(34));
            Assert.AreEqual(41, text.NextAny(36));
            Assert.AreEqual(43, text.NextAny(42));
            Assert.AreEqual(49, text.NextAny(44));
            Assert.AreEqual(58, text.NextAny(50));
            Assert.AreEqual(60, text.NextAny(59));
            Assert.AreEqual(62, text.NextAny(61));
            Assert.AreEqual(63, text.NextAny(63));
            Assert.AreEqual(65, text.NextAny(64));
            Assert.AreEqual(70, text.NextAny(66));
            Assert.AreEqual(72, text.NextAny(71));
            Assert.AreEqual(-1, text.NextAny(73));
        }

        // TODO TestNextAnyWith items

        [TestMethod]
        public void AJson_TextIndexer_TestNext_Invalid()
        {
            JsonTextIndexer text = new JsonTextIndexer(kTestText);

            Assert.AreEqual(-1, text.Next('x', 0));
            Assert.AreEqual(-1, text.Next('{', 5001));
        }

        [TestMethod]
        public void AJson_TextIndexer_TestNext_AnyInvalid()
        {
            JsonTextIndexer text = new JsonTextIndexer(kTestText);

            Assert.AreEqual(-1, text.NextAny(5001));
            Assert.AreEqual(-1, text.NextAny(0, 'x'));
        }
        
    }
}
