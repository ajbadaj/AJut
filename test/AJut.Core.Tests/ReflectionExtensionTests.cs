namespace AJut.Core.UnitTests
{
    using System;
    using System.Collections;
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

        [TestMethod]
        public void ReflectionXT_SetComplexPropertyPathWorks_WithIndexing ()
        {
            var test = new List<int> { 1, 2, 3 };
            Assert.AreEqual(2, test.GetComplexPropertyValue<int>("[1]"));
        }


        [TestMethod]
        public void ReflectionXT_SetComplexPropertyPathWorks_WithSubObjectIndexing ()
        {
            var test = new ThingWithList(1, 2, 3);
            Assert.AreEqual(2, test.GetComplexPropertyValue<int>("Items[1]"));
        }


        [TestMethod]
        public void ReflectionXT_GetPropertyWorks_EnsurePathWithNullChainElements ()
        {
            var test = new SimpleContainer();
            Assert.AreEqual(0, test.GetComplexPropertyValue<int>("Item.Value", null, ensureSubObjectPath: true));
        }

        [TestMethod]
        public void ReflectionXT_GetPropertyWorks_InvalidPropertyPathReturnsNull ()
        {
            Assert.AreEqual(null, (new object()).GetComplexPropertyValue<SimpleContainer>("Fake.SupaFake", null));
        }

        [TestMethod]
        public void ReflectionXT_RunStaticTemplatedMethod_FindsAStaticGenericMethod ()
        {
            object result = ReflectionXT.RunStaticTemplatedMethod(typeof(TemplatedStatics), nameof(TemplatedStatics.Describe), typeof(int), 5);
            Assert.AreEqual("Int32:5", result);
        }

        [TestMethod]
        public void EnumerableXT_CastToArrayOfType_MakesATypedArray ()
        {
            Array result = new object[] { 1, 2, 3 }.CastToArrayOfType(typeof(int));
            Assert.IsInstanceOfType(result, typeof(int[]));
            CollectionAssert.AreEqual(new[] { 1, 2, 3 }, (int[])result);
        }

        [TestMethod]
        public void EnumerableXT_CastAndToArrayOverAType_GiveTheTypedElements ()
        {
            IEnumerable cast = new List<object> { "a", "b" }.Cast(typeof(string));
            Assert.IsInstanceOfType(cast, typeof(IEnumerable<string>));

            Array array = new List<object> { 4.5, 6.0 }.ToArray(typeof(double));
            Assert.IsInstanceOfType(array, typeof(double[]));
            CollectionAssert.AreEqual(new[] { 4.5, 6.0 }, (double[])array);
        }
    }

    public static class TemplatedStatics
    {
        public static string Describe<T> (T value) => $"{typeof(T).Name}:{value}";
    }

    public class SimpleSetTest
    {
        public int Value { get; set; }
    }

    public class SimpleContainer
    {
        public SimpleSetTest Item { get; set; }
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

    public class ThingWithList
    {
        public ThingWithList(params int[] items)
        {
            this.Items = new List<int>(items);
        }

        public List<int> Items { get; set; }
    }
}
