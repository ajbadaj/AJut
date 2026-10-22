namespace AJut.UX.Tests
{
    using System;
    using System.IO;
    using System.Linq;
    using AJut.UX.Docking;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// The layout file the WPF showroom saved, read with WPF's own layout reader. The same file is held to the shared
    /// reader in AJut.UX.Tests' DockLayoutFixtureTests, which this project takes the file from.
    /// </summary>
    [TestClass]
    public class WpfDockLayoutFixtureTests
    {
        // WPF's DockingManager still reads its own layout shape, so it must keep reading the files it already wrote
        [TestMethod]
        public void DockLayoutFixture_WpfShowRoom_ReadsWithWpfsOwnReader ()
        {
            string fixturePath = Path.Combine(AppContext.BaseDirectory, "_TestData", "wpf-showroom-docking-overview.layout");
            DockingSerialization.SerializationInfo state = DockingSerialization.ReadFile(fixturePath);

            Assert.IsNotNull(state);
            DockZoneSerializationData root = state.Core.ZoneInfoByRoot["RootZone"];
            Assert.AreEqual(eDockOrientation.Horizontal, root.Orientation);
            Assert.AreEqual(2, root.ChildZones.Count);
            Assert.AreEqual("ColorController", root.ChildZones[0].DisplayState.Single().TypeId);
            CollectionAssert.AreEqual(
                new[] { "ColorController", "CommandSender" },
                root.ChildZones[1].ChildZones.Select(z => z.DisplayState.Single().TypeId).ToList()
            );
            Assert.AreEqual(0, state.Ancillary.Length);
        }
    }
}
