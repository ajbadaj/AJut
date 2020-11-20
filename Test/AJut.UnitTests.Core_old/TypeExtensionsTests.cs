namespace AJut.UnitTests.Core
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using System.Collections.Generic;

    [TestClass]
    public class TypeExtensionsTests
    {
        public class Test : Dictionary<int, string>
        {
        }

        [TestMethod]
        public void TypeExtensions_FindBaseTypeOrInterface_FullTest ()
        {
            var found = typeof(Test).FindBaseTypeOrInterface(typeof(IDictionary<,>));
            Assert.IsNotNull(found);
            Assert.AreEqual(2, found.GenericTypeArguments.Length);
            Assert.AreEqual(typeof(int), found.GenericTypeArguments[0]);
            Assert.AreEqual(typeof(string), found.GenericTypeArguments[1]);
        }

        [TestMethod]
        public void TypeExtensions_TargetsSameTypeAs_FullTest ()
        {
            Assert.IsTrue(typeof(Dictionary<,>).TargetsSameTypeAs(typeof(Dictionary<int,string>)));
        }
    }
}
