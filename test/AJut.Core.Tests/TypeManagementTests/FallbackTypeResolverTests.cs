namespace AJut.Core.UnitTests.TypeManagement
{
    using System;
    using System.Reflection;
    using AJut.TypeManagement;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class FallbackTypeResolverTests
    {
        // ===========[ Test helpers ]==========================================

        // Stand-in for a consumer type a receiver must resolve by name when the assembly identity
        //  in an AQN cannot be bound - the packaged/ReadyToRun failure Type.GetType hits.
        public enum eResolverProbe { X, Y }

        // ===========[ ResolveByName ]==========================================
        [TestMethod]
        public void ResolveByName_SingleMatchInProvidedAssembly_ResolvesType ()
        {
            Type resolved = FallbackTypeResolver.ResolveByName(
                typeof(eResolverProbe).FullName,
                new[] { typeof(eResolverProbe).Assembly }
            );

            Assert.AreEqual(typeof(eResolverProbe), resolved);
        }

        [TestMethod]
        public void ResolveByName_AssemblyQualifiedNameInput_StripsAssemblyAndResolves ()
        {
            // A caller may hand the whole AQN through; the resolver keys off the full name and
            //  ignores the (unbindable) assembly identity.
            string aqnStyle = typeof(eResolverProbe).FullName + ", Some.Bogus.Assembly, Version=1.2.3.4";

            Type resolved = FallbackTypeResolver.ResolveByName(
                aqnStyle,
                new[] { typeof(eResolverProbe).Assembly }
            );

            Assert.AreEqual(typeof(eResolverProbe), resolved);
        }

        [TestMethod]
        public void ResolveByName_NameNotFoundAnywhere_ReturnsNull ()
        {
            Type resolved = FallbackTypeResolver.ResolveByName(
                "AJut.Core.UnitTests.TypeManagement.ThisTypeDoesNotExist",
                new[] { typeof(FallbackTypeResolverTests).Assembly }
            );

            Assert.IsNull(resolved);
        }

        [TestMethod]
        public void ResolveByName_TypeExistsButAssemblyNotProvided_ReturnsNull ()
        {
            // The probe lives in this test assembly; searching only the core library must not find
            //  it. Resolution stays scoped to the assemblies handed in, never all-loaded.
            Type resolved = FallbackTypeResolver.ResolveByName(
                typeof(eResolverProbe).FullName,
                new[] { typeof(object).Assembly }
            );

            Assert.IsNull(resolved);
        }

        // ===========[ PickSingleCandidate - the 0/1/many decision ]============
        [TestMethod]
        public void PickSingleCandidate_NoCandidates_ReturnsNull ()
        {
            Assert.IsNull(FallbackTypeResolver.PickSingleCandidate("Any.Name", new Type[0]));
        }

        [TestMethod]
        public void PickSingleCandidate_ExactlyOne_ReturnsThatCandidate ()
        {
            Assert.AreEqual(typeof(int), FallbackTypeResolver.PickSingleCandidate("Any.Name", new[] { typeof(int) }));
        }

        [TestMethod]
        public void PickSingleCandidate_MultipleDistinct_ReturnsNull ()
        {
            // Two tracked assemblies carrying the same full name land here; ambiguity resolves to
            //  null (and logs) rather than guessing at which one was meant.
            Type resolved = FallbackTypeResolver.PickSingleCandidate("Colliding.Name", new[] { typeof(int), typeof(string) });
            Assert.IsNull(resolved);
        }
    }
}
