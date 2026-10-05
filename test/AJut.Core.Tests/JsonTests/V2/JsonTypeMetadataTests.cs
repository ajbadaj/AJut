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
            try
            {
                CollectionAssert.Contains(WrittenKeys(new HideAfterUseWriteModel()), nameof(HideAfterUseWriteModel.Hidden), "Precondition: written before the hide");

                TypeMetadataExtensionRegistrar.For<HideAfterUseWriteModel>().Hide(nameof(HideAfterUseWriteModel.Hidden));

                string[] keys = WrittenKeys(new HideAfterUseWriteModel());
                CollectionAssert.DoesNotContain(keys, nameof(HideAfterUseWriteModel.Hidden), "Written after the hide: " + String.Join(", ", keys));
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
            try
            {
                TypeMetadataExtensionRegistrar.For<UnhideAfterUseModel>().Hide(nameof(UnhideAfterUseModel.Hidden));
                CollectionAssert.DoesNotContain(WrittenKeys(new UnhideAfterUseModel()), nameof(UnhideAfterUseModel.Hidden), "Precondition: hidden before the unhide");

                TypeMetadataExtensionRegistrar.For<UnhideAfterUseModel>().Unhide(nameof(UnhideAfterUseModel.Hidden));

                string[] keys = WrittenKeys(new UnhideAfterUseModel());
                CollectionAssert.Contains(keys, nameof(UnhideAfterUseModel.Hidden), "Written after the unhide: " + String.Join(", ", keys));
            }
            finally
            {
                TypeMetadataExtensionRegistrar.ClearFor(typeof(UnhideAfterUseModel));
            }
        }

        [TestMethod]
        public void ClearAll_AfterFirstUse_HiddenPropertyWrittenAgain ()
        {
            try
            {
                TypeMetadataExtensionRegistrar.For<ClearAllAfterUseModel>().Hide(nameof(ClearAllAfterUseModel.Hidden));
                CollectionAssert.DoesNotContain(WrittenKeys(new ClearAllAfterUseModel()), nameof(ClearAllAfterUseModel.Hidden), "Precondition: hidden before ClearAll");

                TypeMetadataExtensionRegistrar.ClearAll();

                string[] keys = WrittenKeys(new ClearAllAfterUseModel());
                CollectionAssert.Contains(keys, nameof(ClearAllAfterUseModel.Hidden), "Written after ClearAll: " + String.Join(", ", keys));
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
            try
            {
                CollectionAssert.Contains(WrittenKeys(new HideOnBaseDerivedModel()), nameof(HideOnBaseModel.BaseProp), "Precondition: written before the hide");

                TypeMetadataExtensionRegistrar.For<HideOnBaseModel>().Hide(nameof(HideOnBaseModel.BaseProp));

                string[] keys = WrittenKeys(new HideOnBaseDerivedModel());
                CollectionAssert.DoesNotContain(keys, nameof(HideOnBaseModel.BaseProp), "Written after the hide: " + String.Join(", ", keys));
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
                CollectionAssert.AreEqual(new[] { "Alpha", "Beta" }, WrittenKeys(new ReorderAfterUseModel()), "Precondition: declaration order before the reorder");

                TypeMetadataExtensionRegistrar.For<ReorderAfterUseModel>()
                    .SetMemberOrder(nameof(ReorderAfterUseModel.Beta), 0)
                    .SetMemberOrder(nameof(ReorderAfterUseModel.Alpha), 1);

                string[] keys = WrittenKeys(new ReorderAfterUseModel());
                CollectionAssert.AreEqual(new[] { "Beta", "Alpha" }, keys, "Written after the reorder: " + String.Join(", ", keys));
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
                CollectionAssert.AreEqual(new[] { "BaseProp", "DerivedProp" }, WrittenKeys(new TierOnBaseDerivedModel()), "Precondition: base first before the tier order");

                // The derived tier keeps its default order (1, with base first), so a base tier order
                //  of 100 puts the base last

                TypeMetadataExtensionRegistrar.For<TierOnBaseModel>().SetTierOrder(100);

                string[] keys = WrittenKeys(new TierOnBaseDerivedModel());
                CollectionAssert.AreEqual(new[] { "DerivedProp", "BaseProp" }, keys, "Written after the tier order: " + String.Join(", ", keys));
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
                CollectionAssert.AreEqual(new[] { "BaseProp", "DerivedProp" }, WrittenKeys(new DefaultOrderingDerivedModel()), "Precondition: base first");

                TypeMetadataExtensionRegistrar.DefaultMemberOrdering = eMemberInheritanceOrdering.DerivedFirst;

                string[] keys = WrittenKeys(new DefaultOrderingDerivedModel());
                CollectionAssert.AreEqual(new[] { "DerivedProp", "BaseProp" }, keys, "Written after switching to derived first: " + String.Join(", ", keys));
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
            return ((JsonDocument)json.Data).Select(kvp => kvp.Key).Where(key => key != JsonDocument.kTypeIndicator).ToArray();
        }

        private static T ReadFrom<T> (string text)
        {
            Json json = JsonHelper.ParseText(text);
            Assert.IsFalse(json.HasErrors, json.GetErrorReport());
            return JsonHelper.BuildObjectForJson<T>(json);
        }
    }
}
