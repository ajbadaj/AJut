namespace AJut.Core.UnitTests.AJsonV2
{
    using System;
    using System.Linq;
    using AJut.Text.AJson;
    using AJut.TypeManagement;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// AJson takes property visibility and order from <see cref="TypeMetadataExtensionRegistrar"/>.
    /// A registration made after AJson has already used a type has to show up the next time that type
    /// is written or read, including registrations on a base class of the type used.
    /// </summary>
    [TestClass]
    public class JsonTypeMetadataTests
    {
        // ===========================[ Test Models ]===================================
        // One model per test, so no test can warm another's cache entries.
        public class HideAfterUseWriteModel
        {
            public int Kept { get; set; } = 1;
            public int Hidden { get; set; } = 2;
        }

        public class HideAfterUseReadModel
        {
            public int Kept { get; set; }
            public int Hidden { get; set; }
        }

        public class UnhideAfterUseModel
        {
            public int Kept { get; set; } = 1;
            public int Hidden { get; set; } = 2;
        }

        public class ClearAllAfterUseModel
        {
            public int Kept { get; set; } = 1;
            public int Hidden { get; set; } = 2;
        }

        public class ReorderAfterUseModel
        {
            public int Alpha { get; set; } = 1;
            public int Beta { get; set; } = 2;
        }

        public class HideOnBaseModel
        {
            public int BaseProp { get; set; } = 1;
        }

        public class HideOnBaseDerivedModel : HideOnBaseModel
        {
            public int DerivedProp { get; set; } = 2;
        }

        public class TierOnBaseModel
        {
            public int BaseProp { get; set; } = 1;
        }

        public class TierOnBaseDerivedModel : TierOnBaseModel
        {
            public int DerivedProp { get; set; } = 2;
        }

        public class DefaultOrderingBaseModel
        {
            public int BaseProp { get; set; } = 1;
        }

        public class DefaultOrderingDerivedModel : DefaultOrderingBaseModel
        {
            public int DerivedProp { get; set; } = 2;
        }

        // ===========================[ Visibility ]===================================
        [TestMethod]
        public void Hide_AfterFirstUse_PropertyNoLongerWritten ()
        {
            const string kHidden = nameof(HideAfterUseWriteModel.Hidden);
            try
            {
                string[] before = WrittenKeys(new HideAfterUseWriteModel());
                CollectionAssert.Contains(before, kHidden, "Precondition: written before the hide");

                TypeMetadataExtensionRegistrar.For<HideAfterUseWriteModel>().Hide(kHidden);

                string[] keys = WrittenKeys(new HideAfterUseWriteModel());
                CollectionAssert.DoesNotContain(keys, kHidden, Listed("Written after the hide", keys));
            }
            finally
            {
                TypeMetadataExtensionRegistrar.ClearFor(typeof(HideAfterUseWriteModel));
            }
        }

        [TestMethod]
        public void Hide_AfterFirstUse_PropertyNoLongerRead ()
        {
            const string kText = "{ \"Kept\": 1, \"Hidden\": 2 }";
            try
            {
                Assert.AreEqual(2, ReadFrom<HideAfterUseReadModel>(kText).Hidden, "Precondition: read before the hide");

                TypeMetadataExtensionRegistrar.For<HideAfterUseReadModel>().Hide(nameof(HideAfterUseReadModel.Hidden));

                HideAfterUseReadModel read = ReadFrom<HideAfterUseReadModel>(kText);
                Assert.AreEqual(1, read.Kept);
                Assert.AreEqual(0, read.Hidden, "A hidden property must not be read");
            }
            finally
            {
                TypeMetadataExtensionRegistrar.ClearFor(typeof(HideAfterUseReadModel));
            }
        }

        [TestMethod]
        public void Unhide_AfterFirstUse_PropertyWrittenAgain ()
        {
            const string kHidden = nameof(UnhideAfterUseModel.Hidden);
            try
            {
                TypeMetadataExtensionRegistrar.For<UnhideAfterUseModel>().Hide(kHidden);
                string[] before = WrittenKeys(new UnhideAfterUseModel());
                CollectionAssert.DoesNotContain(before, kHidden, "Precondition: hidden before the unhide");

                TypeMetadataExtensionRegistrar.For<UnhideAfterUseModel>().Unhide(kHidden);

                string[] keys = WrittenKeys(new UnhideAfterUseModel());
                CollectionAssert.Contains(keys, kHidden, Listed("Written after the unhide", keys));
            }
            finally
            {
                TypeMetadataExtensionRegistrar.ClearFor(typeof(UnhideAfterUseModel));
            }
        }

        [TestMethod]
        public void ClearAll_AfterFirstUse_HiddenPropertyWrittenAgain ()
        {
            const string kHidden = nameof(ClearAllAfterUseModel.Hidden);
            try
            {
                TypeMetadataExtensionRegistrar.For<ClearAllAfterUseModel>().Hide(kHidden);
                string[] before = WrittenKeys(new ClearAllAfterUseModel());
                CollectionAssert.DoesNotContain(before, kHidden, "Precondition: hidden before ClearAll");

                TypeMetadataExtensionRegistrar.ClearAll();

                string[] keys = WrittenKeys(new ClearAllAfterUseModel());
                CollectionAssert.Contains(keys, kHidden, Listed("Written after ClearAll", keys));
            }
            finally
            {
                TypeMetadataExtensionRegistrar.ClearFor(typeof(ClearAllAfterUseModel));
            }
        }

        [TestMethod]
        public void Hide_OnBaseType_AfterDerivedFirstUse_PropertyNoLongerWritten ()
        {
            // The hide goes on the type that declares the property, which is the base. The derived
            //  type's cached property list has to see it too.
            const string kBaseProp = nameof(HideOnBaseModel.BaseProp);
            try
            {
                string[] before = WrittenKeys(new HideOnBaseDerivedModel());
                CollectionAssert.Contains(before, kBaseProp, "Precondition: written before the hide");

                TypeMetadataExtensionRegistrar.For<HideOnBaseModel>().Hide(kBaseProp);

                string[] keys = WrittenKeys(new HideOnBaseDerivedModel());
                CollectionAssert.DoesNotContain(keys, kBaseProp, Listed("Written after the hide", keys));
            }
            finally
            {
                TypeMetadataExtensionRegistrar.ClearFor(typeof(HideOnBaseModel));
            }
        }

        // ===========================[ Ordering ]===================================
        [TestMethod]
        public void MemberOrder_AfterFirstUse_WriteOrderFollows ()
        {
            try
            {
                string[] before = WrittenKeys(new ReorderAfterUseModel());
                CollectionAssert.AreEqual(new[] { "Alpha", "Beta" }, before, "Precondition: declaration order");

                TypeMetadataExtensionRegistrar.For<ReorderAfterUseModel>()
                    .SetMemberOrder(nameof(ReorderAfterUseModel.Beta), 0)
                    .SetMemberOrder(nameof(ReorderAfterUseModel.Alpha), 1);

                string[] keys = WrittenKeys(new ReorderAfterUseModel());
                CollectionAssert.AreEqual(new[] { "Beta", "Alpha" }, keys, Listed("Written after the reorder", keys));
            }
            finally
            {
                TypeMetadataExtensionRegistrar.ClearFor(typeof(ReorderAfterUseModel));
            }
        }

        [TestMethod]
        public void TierOrder_OnBaseType_AfterDerivedFirstUse_WriteOrderFollows ()
        {
            // The derived type's order is built from its base's tier order, so a tier order set on
            //  the base after first use has to reorder the derived type as well.
            eMemberInheritanceOrdering previousDefault = TypeMetadataExtensionRegistrar.DefaultMemberOrdering;
            try
            {
                TypeMetadataExtensionRegistrar.DefaultMemberOrdering = eMemberInheritanceOrdering.BaseFirst;
                string[] before = WrittenKeys(new TierOnBaseDerivedModel());
                CollectionAssert.AreEqual(new[] { "BaseProp", "DerivedProp" }, before, "Precondition: base first");

                // The derived tier keeps its default order (1, with base first), so a base tier order
                //  of 100 puts the base last
                TypeMetadataExtensionRegistrar.For<TierOnBaseModel>().SetTierOrder(100);

                string[] keys = WrittenKeys(new TierOnBaseDerivedModel());
                string[] expected = { "DerivedProp", "BaseProp" };
                CollectionAssert.AreEqual(expected, keys, Listed("Written after the tier order", keys));
            }
            finally
            {
                TypeMetadataExtensionRegistrar.ClearFor(typeof(TierOnBaseModel));
                TypeMetadataExtensionRegistrar.DefaultMemberOrdering = previousDefault;
            }
        }

        [TestMethod]
        public void DefaultMemberOrdering_ChangedAfterFirstUse_WriteOrderFollows ()
        {
            eMemberInheritanceOrdering previousDefault = TypeMetadataExtensionRegistrar.DefaultMemberOrdering;
            try
            {
                TypeMetadataExtensionRegistrar.DefaultMemberOrdering = eMemberInheritanceOrdering.BaseFirst;
                string[] before = WrittenKeys(new DefaultOrderingDerivedModel());
                CollectionAssert.AreEqual(new[] { "BaseProp", "DerivedProp" }, before, "Precondition: base first");

                TypeMetadataExtensionRegistrar.DefaultMemberOrdering = eMemberInheritanceOrdering.DerivedFirst;

                string[] keys = WrittenKeys(new DefaultOrderingDerivedModel());
                CollectionAssert.AreEqual(new[] { "DerivedProp", "BaseProp" }, keys, Listed("Written with derived first", keys));
            }
            finally
            {
                TypeMetadataExtensionRegistrar.DefaultMemberOrdering = previousDefault;
            }
        }

        // ===========================[ Helpers ]===================================
        private static string[] WrittenKeys (object source)
        {
            Json json = JsonHelper.BuildJsonForObject(source);
            Assert.IsFalse(json.HasErrors, json.GetErrorReport());
            return ((JsonDocument)json.Data)
                .Select(kvp => kvp.Key)
                .Where(key => key != JsonDocument.kTypeIndicator)
                .ToArray();
        }

        private static T ReadFrom<T> (string text)
        {
            Json json = JsonHelper.ParseText(text);
            Assert.IsFalse(json.HasErrors, json.GetErrorReport());
            return JsonHelper.BuildObjectForJson<T>(json);
        }

        private static string Listed (string label, string[] keys) => label + ": " + String.Join(", ", keys);
    }
}
