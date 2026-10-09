namespace AJut.Core.UnitTests
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using AJut.Storage;
    using AJut.Text.AJson;
    using AJut.TypeManagement;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class StratabaseLayerExportImportTests
    {
        private const int kLayerCount = 3;
        private const int kCommitted = 1;

        [TestInitialize]
        public void Setup ()
        {
            TypeIdRegistrar.RegisterAllTypeIds(typeof(StratabaseLayerExportImportTests).Assembly);
        }

        [TestMethod]
        public void Stratabase_LayerExport_RoundTripOneOverrideLayerIntoExistingStore ()
        {
            Guid a = Guid.NewGuid(), b = Guid.NewGuid();

            Stratabase source = new Stratabase(kLayerCount);
            source.SetBaselinePropertyValue(a, "Name", "a baseline");
            source.SetOverridePropertyValue(kCommitted, a, "Name", "a committed");
            source.SetOverridePropertyValue(kCommitted, a, "Count", 3);
            source.SetOverridePropertyValue(kCommitted, b, "Numbers", new List<int> { 1, 2, 3 });
            source.SetOverridePropertyValue(0, b, "Ignored", "not in the exported layer");

            // The target already exists, shares the ids, and holds things the import must leave alone
            Stratabase target = new Stratabase(kLayerCount);
            target.SetBaselinePropertyValue(a, "Name", "a baseline");
            target.SetOverridePropertyValue(0, a, "Name", "a lower layer");
            target.SetOverridePropertyValue(kCommitted, b, "Untouched", 99);
            target.SetOverridePropertyValue(2, b, "Top", true);

            StratumEntry[] export = source.ExportOverrideLayer(kCommitted);
            Assert.AreEqual(3, export.Length);

            int changeCount = target.ImportIntoOverrideLayer(kCommitted, export);
            Assert.AreEqual(3, changeCount);

            // The imported layer now holds the source layer, plus what the target already had there (Merge)
            Dictionary<string, object> committed = _ToLookup(target.GetAllOverrideEntries(kCommitted));
            Assert.AreEqual(4, committed.Count);
            Assert.AreEqual("a committed", committed[_Key(a, "Name")]);
            Assert.AreEqual(3, committed[_Key(a, "Count")]);
            CollectionAssert.AreEqual(new List<int> { 1, 2, 3 }, (List<int>)committed[_Key(b, "Numbers")]);
            Assert.AreEqual(99, committed[_Key(b, "Untouched")]);

            // Other layers are untouched
            Assert.IsTrue(target.TryGetOverridePropertyValue(0, a, "Name", out string lowerName));
            Assert.AreEqual("a lower layer", lowerName);
            Assert.IsFalse(target.TryGetOverridePropertyValue(0, b, "Ignored", out object _));
            Assert.IsTrue(target.TryGetOverridePropertyValue(2, b, "Top", out bool top));
            Assert.IsTrue(top);

            // And the layering still resolves through the import
            Assert.AreEqual("a committed", target.GeneratePropertyAccess<string>(a, "Name").GetValue());
        }

        [TestMethod]
        public void Stratabase_LayerImport_Replace_LayerEndsUpHoldingExactlyTheImport ()
        {
            Guid a = Guid.NewGuid(), b = Guid.NewGuid();

            Stratabase source = new Stratabase(kLayerCount);
            source.SetOverridePropertyValue(kCommitted, a, "Name", "a committed");
            source.SetOverridePropertyValue(kCommitted, a, "Count", 3);

            Stratabase target = new Stratabase(kLayerCount);
            target.SetOverridePropertyValue(kCommitted, a, "Name", "stale name");
            target.SetOverridePropertyValue(kCommitted, a, "Stale", "remove me");
            target.SetOverridePropertyValue(kCommitted, b, "AlsoStale", 12);
            target.SetOverridePropertyValue(0, b, "OtherLayer", "keep me");

            int changeCount = target.ImportIntoOverrideLayer(kCommitted, source.ExportOverrideLayer(kCommitted), eStratumImportMode.Replace);

            // Name changed, Count added, Stale and AlsoStale removed
            Assert.AreEqual(4, changeCount);

            Dictionary<string, object> committed = _ToLookup(target.GetAllOverrideEntries(kCommitted));
            Assert.AreEqual(2, committed.Count);
            Assert.AreEqual("a committed", committed[_Key(a, "Name")]);
            Assert.AreEqual(3, committed[_Key(a, "Count")]);

            Assert.IsTrue(target.TryGetOverridePropertyValue(0, b, "OtherLayer", out string otherLayer));
            Assert.AreEqual("keep me", otherLayer);
        }

        [TestMethod]
        public void Stratabase_LayerImport_NotifyOff_RaisesNoStoreEvents ()
        {
            Guid a = Guid.NewGuid();
            int storeEventCount = 0;

            Stratabase source = new Stratabase(kLayerCount);
            source.SetBaselinePropertyValue(a, "Name", "new baseline");
            source.SetOverridePropertyValue(kCommitted, a, "Name", "new committed");
            source.SetOverridePropertyValue(kCommitted, a, "Count", 7);

            Stratabase target = new Stratabase(kLayerCount);
            target.SetBaselinePropertyValue(a, "Name", "old baseline");
            target.SetBaselinePropertyValue(a, "Stale", "old");
            target.SetOverridePropertyValue(kCommitted, a, "Name", "old committed");
            target.SetOverridePropertyValue(kCommitted, a, "Stale", "old");

            target.BaselineDataChanged += _OnBaselineDataChanged;
            target.OverrideDataChanged += _OnOverrideDataChanged;

            // Sets, adds and Replace removals, in both the baseline and an override layer
            Assert.AreEqual(3, target.ImportIntoOverrideLayer(kCommitted, source.ExportOverrideLayer(kCommitted), eStratumImportMode.Replace, notifyOfChanges: false));
            Assert.AreEqual(2, target.ImportIntoBaseline(source.ExportBaseline(), eStratumImportMode.Replace, notifyOfChanges: false));
            Assert.AreEqual(0, storeEventCount);

            // The data is there all the same
            Assert.IsTrue(target.TryGetOverridePropertyValue(kCommitted, a, "Count", out int count));
            Assert.AreEqual(7, count);
            Assert.IsFalse(target.TryGetOverridePropertyValue(kCommitted, a, "Stale", out object _));
            Assert.IsTrue(target.TryGetBaselinePropertyValue(a, "Name", out string baselineName));
            Assert.AreEqual("new baseline", baselineName);
            Assert.IsFalse(target.TryGetBaselinePropertyValue(a, "Stale", out object _));

            // Normal writes after a silent import still notify
            target.SetOverridePropertyValue(kCommitted, a, "Count", 8);
            Assert.AreEqual(1, storeEventCount);

            target.BaselineDataChanged -= _OnBaselineDataChanged;
            target.OverrideDataChanged -= _OnOverrideDataChanged;

            void _OnBaselineDataChanged (object sender, BaselineStratumModificationEventArgs e) => ++storeEventCount;
            void _OnOverrideDataChanged (object sender, OverrideStratumModificationEventArgs e) => ++storeEventCount;
        }

        [TestMethod]
        public void Stratabase_LayerImport_NotifyOff_AccessObjectsReadTheImportedValue ()
        {
            Guid a = Guid.NewGuid();
            int valueChangedCount = 0;

            Stratabase target = new Stratabase(kLayerCount);
            target.SetBaselinePropertyValue(a, "Name", "baseline");
            target.SetOverridePropertyValue(2, a, "Fallback", "top");
            target.SetOverridePropertyValue(0, a, "Fallback", "bottom");

            // Created before the import: one unset, one active on the baseline, one active on layer 2
            StrataPropertyValueAccess<int> count = target.GeneratePropertyAccess<int>(a, "Count");
            StrataPropertyValueAccess<string> name = target.GeneratePropertyAccess<string>(a, "Name");
            StrataPropertyValueAccess<string> fallback = target.GeneratePropertyAccess<string>(a, "Fallback");
            Assert.IsFalse(count.IsSet);
            Assert.AreEqual("baseline", name.GetValue());
            Assert.AreEqual("top", fallback.GetValue());

            name.ValueChanged += _OnValueChanged;

            var silentMerge = new[]
            {
                new StratumEntry(a, "Count", 5),
                new StratumEntry(a, "Name", "committed"),
            };
            target.ImportIntoOverrideLayer(kCommitted, silentMerge, notifyOfChanges: false);

            Assert.IsTrue(count.IsSet);
            Assert.AreEqual(5, count.GetValue());
            Assert.AreEqual("committed", name.GetValue());
            Assert.AreEqual(1, valueChangedCount, "Access objects still raise their own ValueChanged on a silent import");

            // A silent Replace that removes the active layer's value drops the access object back to the next layer down
            target.ImportIntoOverrideLayer(2, Array.Empty<StratumEntry>(), eStratumImportMode.Replace, notifyOfChanges: false);
            Assert.AreEqual("bottom", fallback.GetValue());

            name.ValueChanged -= _OnValueChanged;

            void _OnValueChanged (object sender, EventArgs e) => ++valueChangedCount;
        }

        [TestMethod]
        public void Stratabase_LayerImport_NotifyOn_RaisesOneStoreEventPerChange ()
        {
            Guid a = Guid.NewGuid();
            var changedProperties = new List<string>();

            Stratabase target = new Stratabase(kLayerCount);
            target.SetOverridePropertyValue(kCommitted, a, "Same", 1);
            target.SetOverridePropertyValue(kCommitted, a, "Changes", 1);
            target.SetOverridePropertyValue(kCommitted, a, "Removed", 1);
            target.OverrideDataChanged += _OnOverrideDataChanged;

            var import = new[]
            {
                new StratumEntry(a, "Same", 1),
                new StratumEntry(a, "Changes", 2),
                new StratumEntry(a, "Added", 3),
            };

            Assert.AreEqual(3, target.ImportIntoOverrideLayer(kCommitted, import, eStratumImportMode.Replace));
            CollectionAssert.AreEquivalent(new[] { "Changes", "Added", "Removed" }, changedProperties);

            target.OverrideDataChanged -= _OnOverrideDataChanged;

            void _OnOverrideDataChanged (object sender, OverrideStratumModificationEventArgs e)
            {
                Assert.AreEqual(kCommitted, e.LayerIndex);
                changedProperties.Add(e.PropertyName);
            }
        }

        [TestMethod]
        public void Stratabase_LayerImport_EqualValues_RaiseNothingAndReturnZero ()
        {
            Guid a = Guid.NewGuid();
            int storeEventCount = 0, valueChangedCount = 0;

            Stratabase sb = new Stratabase(kLayerCount);
            sb.SetBaselinePropertyValue(a, "Name", "baseline");
            sb.SetBaselinePropertyValue(a, "Nothing", null);
            sb.SetOverridePropertyValue(kCommitted, a, "Name", "committed");
            sb.SetOverridePropertyValue(kCommitted, a, "Count", 5);
            sb.SetOverridePropertyValue(kCommitted, a, "Nothing", null);
            sb.SetOverridePropertyValue(kCommitted, a, "Numbers", new List<int> { 1, 2, 3 });

            StratumEntry[] committedExport = sb.ExportOverrideLayer(kCommitted);
            StratumEntry[] baselineExport = sb.ExportBaseline();

            StrataPropertyValueAccess<string> name = sb.GeneratePropertyAccess<string>(a, "Name");
            name.ValueChanged += _OnValueChanged;
            sb.BaselineDataChanged += _OnBaselineDataChanged;
            sb.OverrideDataChanged += _OnOverrideDataChanged;

            // The export carries a copy of the list and a null value, both of which still count as equal
            Assert.AreEqual(0, sb.ImportIntoOverrideLayer(kCommitted, committedExport));
            Assert.AreEqual(0, sb.ImportIntoOverrideLayer(kCommitted, committedExport, eStratumImportMode.Replace));
            Assert.AreEqual(0, sb.ImportIntoBaseline(baselineExport, eStratumImportMode.Replace));

            // A live enumeration of the same layer, imported straight back in
            Assert.AreEqual(0, sb.ImportIntoOverrideLayer(kCommitted, sb.GetAllOverrideEntries(kCommitted)));

            Assert.AreEqual(0, storeEventCount);
            Assert.AreEqual(0, valueChangedCount);

            name.ValueChanged -= _OnValueChanged;
            sb.BaselineDataChanged -= _OnBaselineDataChanged;
            sb.OverrideDataChanged -= _OnOverrideDataChanged;

            void _OnValueChanged (object sender, EventArgs e) => ++valueChangedCount;
            void _OnBaselineDataChanged (object sender, BaselineStratumModificationEventArgs e) => ++storeEventCount;
            void _OnOverrideDataChanged (object sender, OverrideStratumModificationEventArgs e) => ++storeEventCount;
        }

        [TestMethod]
        public void Stratabase_LayerImport_LiveEnumerationOfAnotherLayerInTheSameStore ()
        {
            Guid a = Guid.NewGuid(), b = Guid.NewGuid();

            Stratabase sb = new Stratabase(kLayerCount);
            sb.SetOverridePropertyValue(0, a, "Name", "preview");
            sb.SetOverridePropertyValue(0, b, "Count", 2);
            sb.SetOverridePropertyValue(kCommitted, a, "Name", "committed");

            // Committing a preview layer: the source is enumerated live while the target layer is written
            Assert.AreEqual(2, sb.ImportIntoOverrideLayer(kCommitted, sb.GetAllOverrideEntries(0)));

            Dictionary<string, object> committed = _ToLookup(sb.GetAllOverrideEntries(kCommitted));
            Assert.AreEqual(2, committed.Count);
            Assert.AreEqual("preview", committed[_Key(a, "Name")]);
            Assert.AreEqual(2, committed[_Key(b, "Count")]);
        }

        [TestMethod]
        public void Stratabase_LayerExport_BaselineRoundTripKeepsTypeIds ()
        {
            var data = new ExportTypedData(Guid.NewGuid())
            {
                Name = "typed",
                Size = 4,
            };

            Stratabase source = new Stratabase(1);
            Assert.IsTrue(source.SetBaselineFromPropertiesOf(data));

            StratumEntry[] export = source.ExportBaseline();
            CollectionAssert.AreEquivalent(
                source.GetAllBaselineEntries().Select(e => _Key(e.ItemId, e.PropertyName)).ToList(),
                export.Select(e => _Key(e.ItemId, e.PropertyName)).ToList()
            );
            Assert.IsTrue(export.Any(e => e.PropertyName == Stratabase.kTypeIdStorage));

            Stratabase target = new Stratabase(1);
            target.ImportIntoBaseline(export);

            Assert.IsTrue(target.TryGetTypeIdForItem(data.Id, out string typeId));
            Assert.AreEqual(ExportTypedData.kTypeId, typeId);

            var rebuilt = new ExportTypedData(data.Id);
            target.SetObjectWithProperties(data.Id, ref rebuilt);
            Assert.AreEqual(data.Name, rebuilt.Name);
            Assert.AreEqual(data.Size, rebuilt.Size);
        }

        [TestMethod]
        public void Stratabase_LayerExport_ListValuesAreDetachedFromTheStore ()
        {
            Guid a = Guid.NewGuid();
            Stratabase source = new Stratabase(kLayerCount);
            source.SetOverridePropertyValue(kCommitted, a, "Numbers", new List<int> { 1, 2, 3 });

            // The store edits its lists in place, which must not reach an export already taken
            StratumEntry[] export = source.ExportOverrideLayer(kCommitted);
            Assert.IsTrue(source.InsertElementIntoOverrideList(kCommitted, a, "Numbers", 0, 0));
            CollectionAssert.AreEqual(new List<int> { 1, 2, 3 }, (IList)export.Single().Value);

            // Two stores importing one export must not share a list either
            Stratabase first = new Stratabase(kLayerCount);
            Stratabase second = new Stratabase(kLayerCount);
            first.ImportIntoOverrideLayer(kCommitted, export);
            second.ImportIntoOverrideLayer(kCommitted, export);
            Assert.IsTrue(first.InsertElementIntoOverrideList(kCommitted, a, "Numbers", 3, 4));

            Assert.IsTrue(first.TryGetOverridePropertyValue(kCommitted, a, "Numbers", out List<int> firstNumbers));
            Assert.IsTrue(second.TryGetOverridePropertyValue(kCommitted, a, "Numbers", out List<int> secondNumbers));
            CollectionAssert.AreEqual(new List<int> { 1, 2, 3, 4 }, firstNumbers);
            CollectionAssert.AreEqual(new List<int> { 1, 2, 3 }, secondNumbers);
            CollectionAssert.AreEqual(new List<int> { 1, 2, 3 }, (IList)export.Single().Value);
        }

        [TestMethod]
        public void Stratabase_LayerImport_EntryWithNoPropertyName_ThrowsBeforeAnyWrite ()
        {
            Guid a = Guid.NewGuid();
            Stratabase target = new Stratabase(kLayerCount);
            target.SetOverridePropertyValue(kCommitted, a, "Existing", 1);

            var import = new[]
            {
                new StratumEntry(a, "Valid", 2),
                new StratumEntry(a, "", 3),
            };

            Assert.ThrowsException<ArgumentException>(() => target.ImportIntoOverrideLayer(kCommitted, import, eStratumImportMode.Replace));
            Assert.ThrowsException<ArgumentException>(() => target.ImportIntoBaseline(new[] { new StratumEntry(a, null, 3) }));

            // Nothing was written, and nothing was removed by the Replace
            Dictionary<string, object> committed = _ToLookup(target.GetAllOverrideEntries(kCommitted));
            Assert.AreEqual(1, committed.Count);
            Assert.AreEqual(1, committed[_Key(a, "Existing")]);
            Assert.IsFalse(target.GetAllBaselineEntries().Any());
        }

        [TestMethod]
        public void Stratabase_LayerEntries_BadLayerThrowsAtTheCall ()
        {
            Stratabase sb = new Stratabase(kLayerCount);

            Assert.ThrowsException<ArgumentOutOfRangeException>(() => sb.GetAllOverrideEntries(kLayerCount));
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => sb.GetAllOverrideEntries(-1));
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => sb.GetAllOverridePropertiesFor(kLayerCount, Guid.NewGuid()));
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => sb.ExportOverrideLayer(kLayerCount));
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => sb.ImportIntoOverrideLayer(kLayerCount, Array.Empty<StratumEntry>()));
            Assert.ThrowsException<ArgumentNullException>(() => sb.ImportIntoOverrideLayer(0, null));
        }

        [TestMethod]
        public void Stratabase_GetAllOverridePropertiesFor_ListsOneIdsPropertiesInOneLayer ()
        {
            Guid a = Guid.NewGuid(), b = Guid.NewGuid();
            Stratabase sb = new Stratabase(kLayerCount);
            sb.SetBaselinePropertyValue(a, "BaselineOnly", 1);
            sb.SetOverridePropertyValue(kCommitted, a, "Name", "a");
            sb.SetOverridePropertyValue(kCommitted, a, "Count", 2);
            sb.SetOverridePropertyValue(0, a, "OtherLayer", 3);
            sb.SetOverridePropertyValue(kCommitted, b, "OtherId", 4);

            CollectionAssert.AreEquivalent(new[] { "Name", "Count" }, sb.GetAllOverridePropertiesFor(kCommitted, a).ToList());
            Assert.IsFalse(sb.GetAllOverridePropertiesFor(2, a).Any());
            Assert.IsFalse(sb.GetAllOverridePropertiesFor(kCommitted, Guid.NewGuid()).Any());
        }

        [TestMethod]
        public void Stratabase_LayerExport_JsonRoundTripIntoExistingStore ()
        {
            Guid a = Guid.NewGuid(), b = Guid.NewGuid(), reference = Guid.NewGuid();

            Stratabase source = new Stratabase(kLayerCount);
            source.SetOverridePropertyValue(kCommitted, a, "Count", 5);
            source.SetOverridePropertyValue(kCommitted, a, "Label", "five");
            source.SetOverridePropertyValue(kCommitted, a, "Ratio", 0.5);
            source.SetOverridePropertyValue(kCommitted, b, "Reference", reference);
            source.SetOverridePropertyValue(kCommitted, b, "Numbers", new List<int> { 1, 2, 3 });
            source.SetOverridePropertyValue(kCommitted, b, "Payload", new ExportPayload { Name = "payload", Size = 3 });

            Json json = JsonHelper.BuildJsonForObject(source.ExportOverrideLayer(kCommitted));
            Assert.IsFalse(json.HasErrors, json.GetErrorReport());

            Json parsed = JsonHelper.ParseText(json.ToString());
            Assert.IsFalse(parsed.HasErrors, parsed.GetErrorReport());

            StratumEntry[] readBack = JsonHelper.BuildObjectForJson<StratumEntry[]>(parsed);
            Assert.IsFalse(parsed.HasErrors, parsed.GetErrorReport());
            Assert.AreEqual(6, readBack.Length);

            Stratabase target = new Stratabase(kLayerCount);
            target.SetOverridePropertyValue(kCommitted, a, "Count", 1);
            Assert.AreEqual(6, target.ImportIntoOverrideLayer(kCommitted, readBack));

            Assert.IsTrue(target.TryGetOverridePropertyValue(kCommitted, a, "Count", out object count));
            Assert.AreEqual(5, count, "the value keeps its runtime type (int), not just its digits");
            Assert.IsTrue(target.TryGetOverridePropertyValue(kCommitted, a, "Label", out string label));
            Assert.AreEqual("five", label);
            Assert.IsTrue(target.TryGetOverridePropertyValue(kCommitted, a, "Ratio", out double ratio));
            Assert.AreEqual(0.5, ratio);
            Assert.IsTrue(target.TryGetOverridePropertyValue(kCommitted, b, "Reference", out Guid readReference));
            Assert.AreEqual(reference, readReference);
            Assert.IsTrue(target.TryGetOverridePropertyValue(kCommitted, b, "Numbers", out List<int> numbers));
            CollectionAssert.AreEqual(new List<int> { 1, 2, 3 }, numbers);
            Assert.IsTrue(target.TryGetOverridePropertyValue(kCommitted, b, "Payload", out ExportPayload payload));
            Assert.AreEqual("payload", payload.Name);
            Assert.AreEqual(3, payload.Size);

            // Importing the same json again changes only the payload: a class with no Equals of its own compares by reference under
            //  the default ValueEqualityTester, and reading json always builds a new instance
            Assert.AreEqual(1, target.ImportIntoOverrideLayer(kCommitted, JsonHelper.BuildObjectForJson<StratumEntry[]>(parsed)));
        }

        // ===========================[ Utility Methods ]======================================

        private static string _Key (Guid id, string property) => $"{id}/{property}";

        private static Dictionary<string, object> _ToLookup (IEnumerable<StratumEntry> entries)
        {
            return entries.ToDictionary(e => _Key(e.ItemId, e.PropertyName), e => e.Value);
        }

        // ===========================[ Subclasses/structs ]===================================

        [TypeId(kTypeId)]
        public class ExportTypedData
        {
            public const string kTypeId = "StratabaseLayerExportImportTests.ExportTypedData";

            [StratabaseIdConstructor]
            public ExportTypedData (Guid id)
            {
                this.Id = id;
            }

            [StratabaseId]
            public Guid Id { get; set; }
            public string Name { get; set; }
            public int Size { get; set; }
        }

        [TypeId(kTypeId)]
        public class ExportPayload
        {
            public const string kTypeId = "StratabaseLayerExportImportTests.ExportPayload";

            public string Name { get; set; }
            public int Size { get; set; }
        }
    }
}
