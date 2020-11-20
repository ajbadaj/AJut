namespace AJut.UnitTests.Core
{
    using System.Collections.Generic;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    public class Foo
    {
        public Bar BarThing { get; private set; }

        public Foo()
        {
            this.BarThing = new Bar();
        }

        public class Bar
        {
            public List<int> Values { get; private set; }

            public Bar()
            {
                this.Values = new List<int> { 1, 2, 3 };
            }
        }

    }
    [TestClass]
    public class ReflectionExtensionTests
    {
        [TestMethod]
        public void ReflectionXT_ComplexPropertyValue_Works()
        {
            Foo test = new Foo();
            Assert.AreEqual(3, test.GetComplexPropertyValue<int>("BarThing.Values.Count"));
            
        }
    }
}
