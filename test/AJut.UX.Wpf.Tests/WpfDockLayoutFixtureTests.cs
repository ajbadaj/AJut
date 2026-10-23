namespace AJut.UX.Tests
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Windows;
    using AJut.Text.AJson;
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

        // The WPF showroom's layout, read and written back out with a tearoff added, is held to the text AJson wrote for it
        //  through reflection, before WPF's layout types had generated serializers, so a save writes what it always has
        [TestMethod]
        public void DockLayoutFixture_WpfShowRoom_WritesBackTheSameText ()
        {
            DockingSerialization.SerializationInfo state = DockingSerialization.ReadFile(FixturePath("wpf-showroom-docking-overview.layout"));
            Assert.IsNotNull(state);
            state.Ancillary = new[]
            {
                new DockingSerialization.WindowStorageData
                {
                    WindowSize = new Size(900, 700),
                    WindowLocation = new Point(120, 80),
                    WindowState = WindowState.Maximized,
                    WindowIsFullscreened = true,
                    State = state.Core.ZoneInfoByRoot["RootZone"].ChildZones[1],
                },
            };

            Json json = JsonHelper.BuildJsonForObject(state);
            Assert.IsFalse(json.HasErrors, json.GetErrorReport());
            Assert.AreEqual(File.ReadAllText(FixturePath("wpf-showroom-docking-overview.rewritten.layout")).Replace("\r\n", "\n"), json.ToString());
        }

        [TestMethod]
        public void DockingSerialization_LayoutTypes_HaveGeneratedSerializers ()
        {
            // Making one runs the assembly's module initializer, which is where generated serializers register
            Assert.IsNotNull(new DockingSerialization.SerializationInfo());

            Assert.IsTrue(AJsonGeneratedDispatch.IsRegistered(typeof(DockingSerialization.SerializationInfo)), nameof(DockingSerialization.SerializationInfo));
            Assert.IsTrue(AJsonGeneratedDispatch.IsRegistered(typeof(DockingSerialization.CoreStorageData)), nameof(DockingSerialization.CoreStorageData));
            Assert.IsTrue(AJsonGeneratedDispatch.IsRegistered(typeof(DockingSerialization.WindowStorageData)), nameof(DockingSerialization.WindowStorageData));
        }

        // ===========[ Helpers ]===============================================
        private static string FixturePath (string fileName) => Path.Combine(AppContext.BaseDirectory, "_TestData", fileName);
    }
}
