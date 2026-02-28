namespace AJut.Core.UnitTests.AJson
{
    using System.Linq;
    using AJut.Text.AJson;
    using AJut.TypeManagement;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class JsonOrderingTests
    {
        // ===========[ Test helpers ]==========================================

        private class BaseModel
        {
            public int BaseA { get; set; } = 1;
            public int BaseB { get; set; } = 2;
        }

        private class DerivedModel : BaseModel
        {
            public int DerivedX { get; set; } = 10;
            public int DerivedY { get; set; } = 20;
        }

        private class RegistryOrderModel
        {
            public int Third { get; set; } = 3;
            public int First { get; set; } = 1;
            public int Second { get; set; } = 2;
        }

        // ===========[ Setup / Cleanup ]==========================================

        [TestInitialize]
        public void Setup ()
        {
            TypeMetadataExtensionRegistrar.ClearAll();
            TypeMetadataExtensionRegistrar.DefaultMemberOrdering = eMemberInheritanceOrdering.BaseFirst;
        }

        [TestCleanup]
        public void Cleanup ()
        {
            TypeMetadataExtensionRegistrar.ClearAll();
        }

        // ===========[ Tests ]==========================================

        [TestMethod]
        public void AJson_Ordering_DerivedPropertiesSerializeBeforeBase ()
        {
            var obj = new DerivedModel { DerivedX = 10, DerivedY = 20, BaseA = 1, BaseB = 2 };
            Json json = JsonHelper.BuildJsonForObject(obj);

            Assert.IsFalse(json.HasErrors, json.BuildJsonErrorReport());
            Assert.IsTrue(json.Data.IsDocument);

            string[] keys = ((JsonDocument)json.Data)
                .Select(kvp => kvp.Key.ToString())
                .ToArray();

            int derivedXIdx = System.Array.IndexOf(keys, nameof(DerivedModel.DerivedX));
            int derivedYIdx = System.Array.IndexOf(keys, nameof(DerivedModel.DerivedY));
            int baseAIdx    = System.Array.IndexOf(keys, nameof(BaseModel.BaseA));
            int baseBIdx    = System.Array.IndexOf(keys, nameof(BaseModel.BaseB));

            Assert.IsTrue(baseAIdx < derivedXIdx, $"BaseA (idx {baseAIdx}) should appear before DerivedX (idx {derivedXIdx})");
            Assert.IsTrue(baseAIdx < derivedYIdx, $"BaseA (idx {baseAIdx}) should appear before DerivedY (idx {derivedYIdx})");
            Assert.IsTrue(baseAIdx < baseBIdx, $"BaseA (idx {baseAIdx}) should appear before BaseB (idx {baseBIdx})");
        }

        [TestMethod]
        public void AJson_Ordering_RegistrySetMemberOrder_IsRespected ()
        {
            TypeMetadataExtensionRegistrar.For<RegistryOrderModel>()
                .SetMemberOrder(nameof(RegistryOrderModel.First),  0)
                .SetMemberOrder(nameof(RegistryOrderModel.Second), 1)
                .SetMemberOrder(nameof(RegistryOrderModel.Third),  2);

            var obj = new RegistryOrderModel();
            Json json = JsonHelper.BuildJsonForObject(obj);

            Assert.IsFalse(json.HasErrors, json.BuildJsonErrorReport());

            string[] keys = ((JsonDocument)json.Data)
                .Select(kvp => kvp.Key.ToString())
                .ToArray();

            Assert.AreEqual(nameof(RegistryOrderModel.First),  keys[0], "First should be first");
            Assert.AreEqual(nameof(RegistryOrderModel.Second), keys[1], "Second should be second");
            Assert.AreEqual(nameof(RegistryOrderModel.Third),  keys[2], "Third should be third");
        }

        [TestMethod]
        public void AJson_Ordering_RegistryHide_ExcludesPropertyFromJson ()
        {
            TypeMetadataExtensionRegistrar.For<RegistryOrderModel>()
                .Hide(nameof(RegistryOrderModel.Second));

            var obj = new RegistryOrderModel();
            Json json = JsonHelper.BuildJsonForObject(obj);

            Assert.IsFalse(json.HasErrors, json.BuildJsonErrorReport());

            string[] keys = ((JsonDocument)json.Data)
                .Select(kvp => kvp.Key.ToString())
                .ToArray();

            Assert.IsFalse(keys.Contains(nameof(RegistryOrderModel.Second)),
                "Hidden property should not appear in serialized JSON");
            Assert.IsTrue(keys.Contains(nameof(RegistryOrderModel.First)));
            Assert.IsTrue(keys.Contains(nameof(RegistryOrderModel.Third)));
        }

        [TestMethod]
        public void AJson_Ordering_IsStableAcrossMultipleCalls ()
        {
            var obj = new DerivedModel();

            string[] firstCall = ((JsonDocument)JsonHelper.BuildJsonForObject(obj).Data)
                .Select(kvp => kvp.Key.ToString()).ToArray();

            string[] secondCall = ((JsonDocument)JsonHelper.BuildJsonForObject(obj).Data)
                .Select(kvp => kvp.Key.ToString()).ToArray();

            CollectionAssert.AreEqual(firstCall, secondCall,
                "Property order should be identical across multiple serialization calls");
        }
    }
}
