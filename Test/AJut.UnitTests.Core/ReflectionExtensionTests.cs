namespace AJut.UnitTests.Core
{
    using System.Collections.Generic;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class ReflectionExtensionTests
    {
        [TestMethod]
        public void ReflectionXT_ComplexPropertyValue_Works()
        {
            Complex test = new Complex();
            Assert.AreEqual(3, test.GetComplexPropertyValue<int>("BarThing.Values.Count"));
        }

        [TestMethod]
        public void ReflectionXT_SetComplexPropertyPathWorks_Simple ()
        {
            var test = new SimpleSetTest { Value = 5 };
            test.SetPropertyByComplexPath("Value", 2);
            Assert.AreEqual(2, test.Value);
        }

        [TestMethod]
        public void ReflectionXT_SetComplexPropertyPathWorks_Complex ()
        {
            var test = new Complex();
            test.BarThing.SingleValue = 100;
            test.SetPropertyByComplexPath("BarThing.SingleValue", 8);
            Assert.AreEqual(8, test.BarThing.SingleValue);
        }


        [TestMethod]
        public void ReflectionXT_SetComplexPropertyPathWorks_WithStructInPath ()
        {
            var test = new ThingWithStruct();
            test.SetPropertyByComplexPath("Test2.Test", 800);
            Assert.AreEqual(800, test.Test2.Test);
        }
    }

    public class SimpleSetTest
    {
        public int Value { get; set; }
    }

    public class Complex
    {
        public Bar BarThing { get; private set; }

        public Complex ()
        {
            this.BarThing = new Bar();
        }

        public class Bar
        {
            public List<int> Values { get; private set; }
            public int SingleValue { get; set; }

            public Bar ()
            {
                this.Values = new List<int> { 1, 2, 3 };
            }
        }
    }

    public struct StructyThing
    {
        public int Test { get; set; }
    }

    public class ThingWithStruct
    { 
        public StructyThing Test2 { get; set; } = new StructyThing { Test = 8 };
    }
}
