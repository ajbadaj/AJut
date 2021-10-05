namespace AJut.Core.UnitTests
{
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
    }
}
