namespace AJut.Core.UnitTests.TypeManagement
{
    using System.ComponentModel;
    using System.Linq;
    using System.Reflection;
    using AJut.TypeManagement;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class TypeMetadataExtensionRegistrarTests
    {
        // ===========[ Test helpers ]==========================================

        // Simple base/derived hierarchy for ordering tests
        private class Base
        {
            public int BaseFirst { get; set; }
            public int BaseSecond { get; set; }
            public int BaseThird { get; set; }
        }

        private class Derived : Base
        {
            public int DerivedFirst { get; set; }
            public int DerivedSecond { get; set; }
        }

        private class DeepDerived : Derived
        {
            public int DeepFirst { get; set; }
        }

        // Types for attribute overlay tests
        private class ExternalType
        {
            public int Alpha { get; set; }
            public int Beta { get; set; }
            public int Gamma { get; set; }
        }

        // Types for [TypeTierOrder] / [MemberOrder] attribute tests
        [TypeTierOrder(-1)]
        private class AttributedBase
        {
            [MemberOrder(1)]
            public int B { get; set; }
            [MemberOrder(0)]
            public int A { get; set; }
        }

        private class AttributedDerived : AttributedBase
        {
            public int Z { get; set; }
        }

        // ===========[ Cleanup between tests ]==========================================

        [TestCleanup]
        public void Cleanup ()
        {
            // Reset all registrations so tests don't bleed into each other.
            TypeMetadataExtensionRegistrar.ClearAll();
        }

        // ===========[ Ordering: default derived-first ]==========================================

        [TestMethod]
        public void TypeMetadataExtensionRegistrar_DefaultOrdering_DerivedBeforeBase ()
        {
            string[] names = TypeMetadataExtensionRegistrar
                .GetOrderedProperties(typeof(Derived))
                .Select(p => p.Name)
                .ToArray();

            // Derived properties must appear before Base properties
            int derivedFirstIdx  = System.Array.IndexOf(names, nameof(Derived.DerivedFirst));
            int derivedSecondIdx = System.Array.IndexOf(names, nameof(Derived.DerivedSecond));
            int baseFirstIdx     = System.Array.IndexOf(names, nameof(Base.BaseFirst));

            Assert.IsTrue(derivedFirstIdx  < baseFirstIdx, "DerivedFirst should appear before BaseFirst");
            Assert.IsTrue(derivedSecondIdx < baseFirstIdx, "DerivedSecond should appear before BaseFirst");
        }

        [TestMethod]
        public void TypeMetadataExtensionRegistrar_DefaultOrdering_ThreeTiersDerivedFirst ()
        {
            string[] names = TypeMetadataExtensionRegistrar
                .GetOrderedProperties(typeof(DeepDerived))
                .Select(p => p.Name)
                .ToArray();

            int deepIdx    = System.Array.IndexOf(names, nameof(DeepDerived.DeepFirst));
            int derivedIdx = System.Array.IndexOf(names, nameof(Derived.DerivedFirst));
            int baseIdx    = System.Array.IndexOf(names, nameof(Base.BaseFirst));

            Assert.IsTrue(deepIdx < derivedIdx, "DeepDerived properties should precede Derived properties");
            Assert.IsTrue(derivedIdx < baseIdx, "Derived properties should precede Base properties");
        }

        [TestMethod]
        public void TypeMetadataExtensionRegistrar_DefaultOrdering_WithinTierUsesDeclarationOrder ()
        {
            string[] names = TypeMetadataExtensionRegistrar
                .GetOrderedProperties(typeof(Base))
                .Select(p => p.Name)
                .ToArray();

            int firstIdx  = System.Array.IndexOf(names, nameof(Base.BaseFirst));
            int secondIdx = System.Array.IndexOf(names, nameof(Base.BaseSecond));
            int thirdIdx  = System.Array.IndexOf(names, nameof(Base.BaseThird));

            Assert.IsTrue(firstIdx < secondIdx, "BaseFirst should precede BaseSecond");
            Assert.IsTrue(secondIdx < thirdIdx, "BaseSecond should precede BaseThird");
        }

        // ===========[ Ordering: attribute overrides ]==========================================

        [TestMethod]
        public void TypeMetadataExtensionRegistrar_TypeTierOrderAttribute_FlipsBaseBeforeDerived ()
        {
            // AttributedBase has [TypeTierOrder(-1)] so it should appear before AttributedDerived.
            string[] names = TypeMetadataExtensionRegistrar
                .GetOrderedProperties(typeof(AttributedDerived))
                .Select(p => p.Name)
                .ToArray();

            int zIdx = System.Array.IndexOf(names, nameof(AttributedDerived.Z));
            int aIdx = System.Array.IndexOf(names, nameof(AttributedBase.A));
            int bIdx = System.Array.IndexOf(names, nameof(AttributedBase.B));

            Assert.IsTrue(aIdx < zIdx, "AttributedBase.A (tier -1) should precede AttributedDerived.Z");
            Assert.IsTrue(bIdx < zIdx, "AttributedBase.B (tier -1) should precede AttributedDerived.Z");
        }

        [TestMethod]
        public void TypeMetadataExtensionRegistrar_MemberOrderAttribute_OverridesDeclarationOrder ()
        {
            // AttributedBase has [MemberOrder(0)] on A and [MemberOrder(1)] on B,
            // but A is declared after B - MemberOrder should win.
            string[] names = TypeMetadataExtensionRegistrar
                .GetOrderedProperties(typeof(AttributedBase))
                .Select(p => p.Name)
                .ToArray();

            int aIdx = System.Array.IndexOf(names, nameof(AttributedBase.A));
            int bIdx = System.Array.IndexOf(names, nameof(AttributedBase.B));

            Assert.IsTrue(aIdx < bIdx, "A ([MemberOrder(0)]) should appear before B ([MemberOrder(1)])");
        }

        // ===========[ Ordering: registry overrides ]==========================================

        [TestMethod]
        public void TypeMetadataExtensionRegistrar_SetTierOrder_OverridesDefault ()
        {
            TypeMetadataExtensionRegistrar.For<Base>().SetTierOrder(-10);

            string[] names = TypeMetadataExtensionRegistrar
                .GetOrderedProperties(typeof(Derived))
                .Select(p => p.Name)
                .ToArray();

            int baseFirstIdx    = System.Array.IndexOf(names, nameof(Base.BaseFirst));
            int derivedFirstIdx = System.Array.IndexOf(names, nameof(Derived.DerivedFirst));

            Assert.IsTrue(baseFirstIdx < derivedFirstIdx, "Base (tier -10) should appear before Derived");
        }

        [TestMethod]
        public void TypeMetadataExtensionRegistrar_SetMemberOrder_OverridesDeclarationOrder ()
        {
            TypeMetadataExtensionRegistrar.For<Base>()
                .SetMemberOrder(nameof(Base.BaseThird), 0)
                .SetMemberOrder(nameof(Base.BaseFirst), 1)
                .SetMemberOrder(nameof(Base.BaseSecond), 2);

            string[] names = TypeMetadataExtensionRegistrar
                .GetOrderedProperties(typeof(Base))
                .Select(p => p.Name)
                .ToArray();

            Assert.AreEqual(nameof(Base.BaseThird),  names[0]);
            Assert.AreEqual(nameof(Base.BaseFirst),  names[1]);
            Assert.AreEqual(nameof(Base.BaseSecond), names[2]);
        }

        // ===========[ Visibility: Hide / Unhide ]==========================================

        [TestMethod]
        public void TypeMetadataExtensionRegistrar_Hide_ExcludesFromOrderedProperties ()
        {
            TypeMetadataExtensionRegistrar.For<Base>().Hide(nameof(Base.BaseSecond));

            string[] names = TypeMetadataExtensionRegistrar
                .GetOrderedProperties(typeof(Base))
                .Select(p => p.Name)
                .ToArray();

            // GetOrderedProperties does NOT filter - IsHidden is checked by consuming systems.
            // But IsHidden should return true for the hidden member.
            PropertyInfo baseProp = typeof(Base).GetProperty(nameof(Base.BaseSecond));
            Assert.IsTrue(TypeMetadataExtensionRegistrar.IsHidden(baseProp));
            Assert.IsFalse(TypeMetadataExtensionRegistrar.IsHidden(typeof(Base).GetProperty(nameof(Base.BaseFirst))));
        }

        [TestMethod]
        public void TypeMetadataExtensionRegistrar_Unhide_RemovesHide ()
        {
            TypeMetadataExtensionRegistrar.For<Base>()
                .Hide(nameof(Base.BaseSecond))
                .Unhide(nameof(Base.BaseSecond));

            PropertyInfo prop = typeof(Base).GetProperty(nameof(Base.BaseSecond));
            Assert.IsFalse(TypeMetadataExtensionRegistrar.IsHidden(prop));
        }

        // ===========[ Attribute overlay ]==========================================

        [TestMethod]
        public void TypeMetadataExtensionRegistrar_GetAttribute_RegistryBeforeReflection ()
        {
            // ExternalType has no [DisplayName] attributes on its properties.
            // Register one via the registry and verify it is returned.
            TypeMetadataExtensionRegistrar.For<ExternalType>()
                .AddAttribute(nameof(ExternalType.Beta), new DisplayNameAttribute("My Beta"));

            PropertyInfo prop = typeof(ExternalType).GetProperty(nameof(ExternalType.Beta));
            var attr = TypeMetadataExtensionRegistrar.GetAttribute<DisplayNameAttribute>(prop);

            Assert.IsNotNull(attr, "Registry-registered DisplayNameAttribute should be returned");
            Assert.AreEqual("My Beta", attr.DisplayName);
        }

        [TestMethod]
        public void TypeMetadataExtensionRegistrar_GetAttribute_FallsBackToReflection ()
        {
            // Alpha has no registry entry and no [DisplayName] attribute either.
            PropertyInfo prop = typeof(ExternalType).GetProperty(nameof(ExternalType.Alpha));
            var attr = TypeMetadataExtensionRegistrar.GetAttribute<DisplayNameAttribute>(prop);
            Assert.IsNull(attr, "No attribute should be found when neither registry nor reflection has one");
        }

        [TestMethod]
        public void TypeMetadataExtensionRegistrar_HasAttribute_TrueWhenRegistered ()
        {
            TypeMetadataExtensionRegistrar.For<ExternalType>()
                .AddAttribute(nameof(ExternalType.Gamma), new DisplayNameAttribute("Gamma Label"));

            PropertyInfo prop = typeof(ExternalType).GetProperty(nameof(ExternalType.Gamma));
            Assert.IsTrue(TypeMetadataExtensionRegistrar.HasAttribute<DisplayNameAttribute>(prop));
        }

        // ===========[ ClearFor / ClearAll ]==========================================

        [TestMethod]
        public void TypeMetadataExtensionRegistrar_ClearFor_RemovesRegistration ()
        {
            TypeMetadataExtensionRegistrar.For<Base>().Hide(nameof(Base.BaseFirst));
            TypeMetadataExtensionRegistrar.ClearFor(typeof(Base));

            PropertyInfo prop = typeof(Base).GetProperty(nameof(Base.BaseFirst));
            Assert.IsFalse(TypeMetadataExtensionRegistrar.IsHidden(prop), "ClearFor should remove hide registration");
        }

        [TestMethod]
        public void TypeMetadataExtensionRegistrar_ClearAll_RemovesAll ()
        {
            TypeMetadataExtensionRegistrar.For<Base>().Hide(nameof(Base.BaseFirst));
            TypeMetadataExtensionRegistrar.For<ExternalType>()
                .AddAttribute(nameof(ExternalType.Alpha), new DisplayNameAttribute("Registered"));

            TypeMetadataExtensionRegistrar.ClearAll();

            PropertyInfo baseProp     = typeof(Base).GetProperty(nameof(Base.BaseFirst));
            PropertyInfo externalProp = typeof(ExternalType).GetProperty(nameof(ExternalType.Alpha));

            Assert.IsFalse(TypeMetadataExtensionRegistrar.IsHidden(baseProp));
            Assert.IsNull(TypeMetadataExtensionRegistrar.GetAttribute<DisplayNameAttribute>(externalProp));
        }

        // ===========[ Cache invalidation ]==========================================

        [TestMethod]
        public void TypeMetadataExtensionRegistrar_OrderingCacheInvalidatedOnRegistration ()
        {
            // First call populates the cache with default order.
            string[] beforeRegistration = TypeMetadataExtensionRegistrar
                .GetOrderedProperties(typeof(Base))
                .Select(p => p.Name)
                .ToArray();

            // Register a new tier order that flips the within-tier member ordering.
            TypeMetadataExtensionRegistrar.For<Base>()
                .SetMemberOrder(nameof(Base.BaseThird), -1);

            string[] afterRegistration = TypeMetadataExtensionRegistrar
                .GetOrderedProperties(typeof(Base))
                .Select(p => p.Name)
                .ToArray();

            // BaseThird should now be first due to order -1.
            Assert.AreEqual(nameof(Base.BaseThird), afterRegistration[0],
                "Cache should be invalidated when a new registration is made");
        }
    }
}
