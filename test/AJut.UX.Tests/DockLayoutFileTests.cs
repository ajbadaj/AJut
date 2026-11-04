namespace AJut.UX.Tests
{
    using System.Collections.Generic;
    using AJut.Text.AJson;
    using AJut.TypeManagement;
    using AJut.UX.Docking;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// The docking layout file where dynamic code is not supported (an iOS build compiled ahead of time), where AJson reads and
    /// writes a type's members only through its generated serializer. The test runner always has a JIT, so these simulate that
    /// platform on their own thread.
    /// </summary>
    [TestClass]
    public class DockLayoutFileTests
    {
        [TestMethod]
        public void DockLayoutFile_WithoutDynamicCode_WritesAndReadsBackALayout ()
        {
            var contents = new DockLayoutFile.Contents
            {
                Core = new DockLayoutFile.CoreLayout(),
                Ancillary = new[]
                {
                    new DockLayoutFile.TearoffLayout
                    {
                        WindowX = 40,
                        WindowY = 60,
                        WindowWidth = 640,
                        WindowHeight = 480,
                        IsMaximized = true,
                        State = MakeZone(eDockOrientation.Single, "Tearoff Panel"),
                    },
                },
            };

            var root = new DockZoneSerializationData(eDockOrientation.Horizontal) { SizeOnParent = new DockZoneSize(1200, 800) };
            root.ChildZones.Add(MakeZone(eDockOrientation.Tabbed, "First Tab", "Second Tab"));
            root.ChildZones.Add(MakeZone(eDockOrientation.Single, "Side Panel"));
            root.ChildZones[0].SelectedIndex = 1;
            contents.Core.ZoneInfoByRoot["Root"] = root;

            using (DynamicCodeAvailability.SimulateUnsupported())
            {
                Json json = JsonHelper.BuildJsonForObject(contents);
                Assert.IsFalse(json.HasErrors, json.GetErrorReport());

                Json reparsed = JsonHelper.ParseText(json.ToString());
                DockLayoutFile.Contents read = JsonHelper.BuildObjectForJson<DockLayoutFile.Contents>(reparsed);
                Assert.IsFalse(reparsed.HasErrors, reparsed.GetErrorReport());
                Assert.IsNotNull(read, json.ToString());

                DockZoneSerializationData readRoot = read.Core.ZoneInfoByRoot["Root"];
                Assert.AreEqual(eDockOrientation.Horizontal, readRoot.Orientation);
                Assert.AreEqual(1200, readRoot.SizeOnParent.Width);
                Assert.AreEqual(800, readRoot.SizeOnParent.Height);
                Assert.AreEqual(2, readRoot.ChildZones.Count);
                Assert.AreEqual(eDockOrientation.Tabbed, readRoot.ChildZones[0].Orientation);
                Assert.AreEqual(1, readRoot.ChildZones[0].SelectedIndex);
                CollectionAssert.AreEqual(new[] { "First Tab", "Second Tab" }, TypeIdsOf(readRoot.ChildZones[0]));
                CollectionAssert.AreEqual(new[] { "Side Panel" }, TypeIdsOf(readRoot.ChildZones[1]));
                Assert.AreEqual(300, readRoot.ChildZones[1].SizeOnParent.Width);

                DockLayoutFile.TearoffLayout tearoff = read.Ancillary[0];
                Assert.AreEqual(40, tearoff.WindowX);
                Assert.AreEqual(60, tearoff.WindowY);
                Assert.AreEqual(640, tearoff.WindowWidth);
                Assert.AreEqual(480, tearoff.WindowHeight);
                Assert.IsTrue(tearoff.IsMaximized);
                CollectionAssert.AreEqual(new[] { "Tearoff Panel" }, TypeIdsOf(tearoff.State));
            }
        }

        // ===========[ Helpers ]===============================================
        private static DockZoneSerializationData MakeZone (eDockOrientation orientation, params string[] panelTypeIds)
        {
            var displays = new List<DockDisplaySerializationData>();
            foreach (string typeId in panelTypeIds)
            {
                displays.Add(new DockDisplaySerializationData { TypeId = typeId });
            }

            return new DockZoneSerializationData(orientation)
            {
                SizeOnParent = new DockZoneSize(300, 200),
                DisplayState = displays.ToArray(),
            };
        }

        private static List<string> TypeIdsOf (DockZoneSerializationData zone)
        {
            var typeIds = new List<string>();
            foreach (DockDisplaySerializationData display in zone.DisplayState)
            {
                typeIds.Add(display.TypeId);
            }

            return typeIds;
        }
    }
}
