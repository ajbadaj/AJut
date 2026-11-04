namespace AJut.Core.UnitTests
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Reflection;
    using AJut.TypeManagement;
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
        public void ReflectionXT_RunStaticTemplatedMethod_WithoutDynamicCode_StillRunsWhereTheRuntimeHasTheCode ()
        {
            using (DynamicCodeAvailability.SimulateUnsupported())
            {
                object result = ReflectionXT.RunStaticTemplatedMethod(typeof(TemplatedStatics), nameof(TemplatedStatics.Describe), typeof(int), 5);
                Assert.AreEqual("Int32:5", result, "A platform without dynamic code can still have the code for an instantiation, so the call is made");
            }
        }

        [TestMethod]
        public void ReflectionXT_RunStaticTemplatedMethod_WithoutDynamicCode_MissingCompiledCode_ThrowsPlatformNotSupported ()
        {
            using (DynamicCodeAvailability.SimulateUnsupported())
            {
                var error = Assert.ThrowsExactly<PlatformNotSupportedException>(() => ReflectionXT.RunStaticTemplatedMethod(typeof(TemplatedFailures), nameof(TemplatedFailures.FailLikeAnAotOnlyRuntime), typeof(int), 5));
                StringAssert.Contains(error.Message, nameof(TemplatedFailures.FailLikeAnAotOnlyRuntime));
                StringAssert.Contains(error.Message, nameof(Int32));
                Assert.IsNotNull(error.InnerException, "The runtime's own failure should come along with it");
            }
        }

        [TestMethod]
        public void ReflectionXT_InvokeTemplatedMethod_WithoutDynamicCode_MissingCompiledCode_ThrowsPlatformNotSupported ()
        {
            var target = new TemplatedInstanceFailures();
            using (DynamicCodeAvailability.SimulateUnsupported())
            {
                var error = Assert.ThrowsExactly<PlatformNotSupportedException>(() => target.InvokeTemplatedMethod(nameof(TemplatedInstanceFailures.FailLikeAnAotOnlyRuntime), typeof(string), "five"));
                StringAssert.Contains(error.Message, nameof(TemplatedInstanceFailures.FailLikeAnAotOnlyRuntime));
            }
        }

        [TestMethod]
        public void ReflectionXT_RunStaticTemplatedMethod_WithoutDynamicCode_TheTargetsOwnFailure_IsLeftAlone ()
        {
            using (DynamicCodeAvailability.SimulateUnsupported())
            {
                var error = Assert.ThrowsExactly<TargetInvocationException>(() => ReflectionXT.RunStaticTemplatedMethod(typeof(TemplatedFailures), nameof(TemplatedFailures.FailOnItsOwn), typeof(int), 5));
                Assert.IsInstanceOfType(error.InnerException, typeof(NotSupportedException));
            }
        }

        [TestMethod]
        public void ReflectionXT_IsMissingCompiledCode_KnowsBothRuntimesShapes_AndNothingElse ()
        {
            // NativeAOT: bare, straight out of MakeGenericMethod or Invoke
            Assert.IsTrue(ReflectionXT.IsMissingCompiledCode(new NotSupportedException("'Describe<Int32>' is missing native code.")));

            // Mono: from Invoke, wrapped like anything else the call throws
            Assert.IsTrue(ReflectionXT.IsMissingCompiledCode(new TargetInvocationException(TemplatedFailureMaker.MakeAotOnlyFailure("Describe<Int32>"))));

            // The target's own failures come back wrapped, and are the target's to report
            Assert.IsFalse(ReflectionXT.IsMissingCompiledCode(new TargetInvocationException(new NotSupportedException("The target's own failure"))));
            Assert.IsFalse(ReflectionXT.IsMissingCompiledCode(new ArgumentException("A type argument breaks a constraint")));
        }

        [TestMethod]
        public void ReflectionXT_RunStaticTemplatedMethod_WithDynamicCode_AFailureIsLeftAlone ()
        {
            // Where code can be made at runtime there is no missing code to report, whatever the target throws
            Assert.ThrowsExactly<TargetInvocationException>(() => ReflectionXT.RunStaticTemplatedMethod(typeof(TemplatedFailures), nameof(TemplatedFailures.FailLikeAnAotOnlyRuntime), typeof(int), 5));
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

    /// <summary>
    /// Templated targets that fail the two ways a templated call can fail on a platform without dynamic code
    /// </summary>
    public static class TemplatedFailures
    {
        /// <summary>
        /// Throws what Mono raises in an ahead of time compiled build (iOS) when it has no compiled code for a generic method
        /// and can't JIT one, which reflection then hands back inside a TargetInvocationException
        /// </summary>
        public static string FailLikeAnAotOnlyRuntime<T> (T value) => throw TemplatedFailureMaker.MakeAotOnlyFailure($"FailLikeAnAotOnlyRuntime<{typeof(T).Name}>");

        /// <summary>A failure of the target's own, which has nothing to do with compiled code</summary>
        public static string FailOnItsOwn<T> (T value) => throw new NotSupportedException("The target's own failure");
    }

    public class TemplatedInstanceFailures
    {
        public string FailLikeAnAotOnlyRuntime<T> (T value) => throw TemplatedFailureMaker.MakeAotOnlyFailure($"FailLikeAnAotOnlyRuntime<{typeof(T).Name}>");
    }

    internal static class TemplatedFailureMaker
    {
        public static Exception MakeAotOnlyFailure (string methodName)
        {
            // ExecutionEngineException is obsolete because CoreCLR no longer raises it, but Mono still does, for exactly this
#pragma warning disable CS0618
            return new ExecutionEngineException($"Attempting to JIT compile method '{methodName}' while running in aot-only mode.");
#pragma warning restore CS0618
        }
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
