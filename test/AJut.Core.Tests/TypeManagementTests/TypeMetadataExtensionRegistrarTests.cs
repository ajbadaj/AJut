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

        // ===========[ Setup / Cleanup between tests ]==========================================

        [TestInitialize]
        public void Setup ()
        {
            // Ensure every test starts from a known-clean state regardless of
            // what a previous test (or default field init) may have left behind.
            TypeMetadataExtensionRegistrar.ClearAll();
            TypeMetadataExtensionRegistrar.DefaultMemberOrdering = eMemberInheritanceOrdering.BaseFirst;
        }

        [TestCleanup]
        public void Cleanup ()
        {
            TypeMetadataExtensionRegistrar.ClearAll();
        }

        // ===========[ Ordering: default derived-first ]==========================================

        [TestMethod]
        public void TypeMetadataExtensionRegistrar_DefaultOrdering_DerivedBeforeBase ()
        {
            TypeMetadataExtensionRegistrar.DefaultMemberOrdering = eMemberInheritanceOrdering.DerivedFirst;

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
            TypeMetadataExtensionRegistrar.DefaultMemberOrdering = eMemberInheritanceOrdering.DerivedFirst;

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

        // ===========[ Explicit vs unattributed ordering boundary ]==========================================

        // Class with 5 explicitly ordered properties followed by 5 unattributed ones.
        // The explicit orders use very large values to exercise the two-key sort boundary.
        private class PartialOrderModel
        {
            [MemberOrder(int.MaxValue - 1)]
            public int Explicit1 { get; set; }

            [MemberOrder(int.MaxValue)]
            public int Explicit2 { get; set; }

            // These three have no [MemberOrder] — they must always follow the explicit ones.
            public int Unordered1 { get; set; }
            public int Unordered2 { get; set; }
            public int Unordered3 { get; set; }
        }

        [TestMethod]
        public void TypeMetadataExtensionRegistrar_ExplicitOrderedAlwaysBeforeUnattributed_EvenWithLargeValues ()
        {
            string[] names = TypeMetadataExtensionRegistrar
                .GetOrderedProperties(typeof(PartialOrderModel))
                .Select(p => p.Name)
                .ToArray();

            int explicit1Idx   = System.Array.IndexOf(names, nameof(PartialOrderModel.Explicit1));
            int explicit2Idx   = System.Array.IndexOf(names, nameof(PartialOrderModel.Explicit2));
            int unordered1Idx  = System.Array.IndexOf(names, nameof(PartialOrderModel.Unordered1));
            int unordered2Idx  = System.Array.IndexOf(names, nameof(PartialOrderModel.Unordered2));
            int unordered3Idx  = System.Array.IndexOf(names, nameof(PartialOrderModel.Unordered3));

            // All explicitly ordered properties must come before all unattributed ones,
            // even though the explicit orders are int.MaxValue-1 and int.MaxValue.
            Assert.IsTrue(explicit1Idx < unordered1Idx,
                "Explicit1 (int.MaxValue-1) must precede Unordered1 despite large order value");
            Assert.IsTrue(explicit2Idx < unordered1Idx,
                "Explicit2 (int.MaxValue) must precede Unordered1 despite large order value");

            // Within the explicit group: order by the explicit value.
            Assert.IsTrue(explicit1Idx < explicit2Idx,
                "Explicit1 (int.MaxValue-1) should precede Explicit2 (int.MaxValue)");

            // Within the unattributed group: declaration order (MetadataToken).
            Assert.IsTrue(unordered1Idx < unordered2Idx, "Unordered1 declared before Unordered2");
            Assert.IsTrue(unordered2Idx < unordered3Idx, "Unordered2 declared before Unordered3");
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

        // ===========[ DefaultMemberOrdering ]==========================================

        [TestMethod]
        public void TypeMetadataExtensionRegistrar_DefaultOrdering_IsBaseToConcrete ()
        {
            Assert.AreEqual(eMemberInheritanceOrdering.BaseFirst, TypeMetadataExtensionRegistrar.DefaultMemberOrdering,
                "Default should be BaseToConcrete");
        }

        [TestMethod]
        public void TypeMetadataExtensionRegistrar_DefaultOrdering_BaseToConcrete_BaseBeforeDerived ()
        {
            TypeMetadataExtensionRegistrar.DefaultMemberOrdering = eMemberInheritanceOrdering.BaseFirst;

            string[] names = TypeMetadataExtensionRegistrar
                .GetOrderedProperties(typeof(Derived))
                .Select(p => p.Name)
                .ToArray();

            int derivedFirstIdx = System.Array.IndexOf(names, nameof(Derived.DerivedFirst));
            int baseFirstIdx    = System.Array.IndexOf(names, nameof(Base.BaseFirst));

            Assert.IsTrue(baseFirstIdx < derivedFirstIdx,
                "BaseToConcrete: Base properties should appear before Derived properties");
        }

        [TestMethod]
        public void TypeMetadataExtensionRegistrar_DefaultOrdering_BaseToConcrete_ThreeTiers ()
        {
            TypeMetadataExtensionRegistrar.DefaultMemberOrdering = eMemberInheritanceOrdering.BaseFirst;

            string[] names = TypeMetadataExtensionRegistrar
                .GetOrderedProperties(typeof(DeepDerived))
                .Select(p => p.Name)
                .ToArray();

            int deepIdx    = System.Array.IndexOf(names, nameof(DeepDerived.DeepFirst));
            int derivedIdx = System.Array.IndexOf(names, nameof(Derived.DerivedFirst));
            int baseIdx    = System.Array.IndexOf(names, nameof(Base.BaseFirst));

            Assert.IsTrue(baseIdx < derivedIdx,   "BaseToConcrete: Base before Derived");
            Assert.IsTrue(derivedIdx < deepIdx,   "BaseToConcrete: Derived before DeepDerived");
        }

        [TestMethod]
        public void TypeMetadataExtensionRegistrar_DefaultOrdering_ExplicitTierOrder_UnaffectedByGlobalDefault ()
        {
            // Pin Base to tier 99 (explicit) so it appears last regardless of global default.
            TypeMetadataExtensionRegistrar.For<Base>().SetTierOrder(99);

            // Switch global default to BaseToConcrete — should NOT override the explicit registration.
            TypeMetadataExtensionRegistrar.DefaultMemberOrdering = eMemberInheritanceOrdering.BaseFirst;

            string[] names = TypeMetadataExtensionRegistrar
                .GetOrderedProperties(typeof(Derived))
                .Select(p => p.Name)
                .ToArray();

            int derivedFirstIdx = System.Array.IndexOf(names, nameof(Derived.DerivedFirst));
            int baseFirstIdx    = System.Array.IndexOf(names, nameof(Base.BaseFirst));

            Assert.IsTrue(derivedFirstIdx < baseFirstIdx,
                "Explicit SetTierOrder(99) on Base should keep Base after Derived even with BaseToConcrete default");
        }

        [TestMethod]
        public void TypeMetadataExtensionRegistrar_DefaultOrdering_ChangingDefaultInvalidatesCache ()
        {
            // Populate the cache with BaseToConcrete order (base properties first).
            string[] baseToConcrete = TypeMetadataExtensionRegistrar
                .GetOrderedProperties(typeof(Derived))
                .Select(p => p.Name)
                .ToArray();

            // Switch default — should invalidate the cache and flip the order.
            TypeMetadataExtensionRegistrar.DefaultMemberOrdering = eMemberInheritanceOrdering.DerivedFirst;

            string[] concreteToBases = TypeMetadataExtensionRegistrar
                .GetOrderedProperties(typeof(Derived))
                .Select(p => p.Name)
                .ToArray();

            Assert.AreNotEqual(baseToConcrete[0], concreteToBases[0],
                "Changing DefaultMemberOrdering should invalidate the cache and produce different first property");
        }

        [TestMethod]
        public void TypeMetadataExtensionRegistrar_ClearAll_DoesNotResetDefaultMemberOrdering ()
        {
            TypeMetadataExtensionRegistrar.DefaultMemberOrdering = eMemberInheritanceOrdering.DerivedFirst;
            TypeMetadataExtensionRegistrar.ClearAll();

            Assert.AreEqual(eMemberInheritanceOrdering.DerivedFirst, TypeMetadataExtensionRegistrar.DefaultMemberOrdering,
                "ClearAll is a registration-level reset and must not change the program-level DefaultMemberOrdering preference");
        }
    }
}
