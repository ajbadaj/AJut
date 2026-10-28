namespace AJut.UX.Tests
{
    using System;
    using System.IO;
    using System.Linq;
    using AJut.Text.AJson;
    using AJut.TypeManagement;
    using AJut.UX.Docking;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    // ===========[ Fixture panels ]========================================
    // Stand-ins for the showroom panels named in the fixture layouts, registered under the type ids those files use

    /// <summary>The state the WinUI3 showroom's panels save</summary>
    public class ShowRoomFixturePanelState
    {
        public string Label { get; set; }
        public int Value { get; set; }
    }

    /// <summary>A panel that keeps the label its saved state gave it</summary>
    public class ShowRoomFixturePanel : FakeDockPanel, IDockableDisplayElement
    {
        public string AppliedLabel { get; private set; }

        public void ApplyState (object state) => this.AppliedLabel = (state as ShowRoomFixturePanelState)?.Label;
    }

    public class HiddenInfraFixturePanel : ShowRoomFixturePanel { }
    public class SingleInstanceFixturePanel : ShowRoomFixturePanel { }
    public class ColorControllerFixturePanel : FakeDockPanel { }
    public class CommandSenderFixturePanel : FakeDockPanel { }

    // ===========[ Tests ]=================================================

    /// <summary>
    /// Layout files the WPF and WinUI3 showrooms saved, kept as they were written, so the shared layout reader is held to
    /// what real files hold rather than only to what it writes itself
    /// </summary>
    [TestClass]
    public class DockLayoutFixtureTests
    {
        [ClassInitialize]
        public static void RegisterFixtureTypeIds (TestContext _)
        {
            TypeIdRegistrar.RegisterTypeId<ShowRoomFixturePanelState>("ShowRoomPanelState");
            TypeIdRegistrar.RegisterTypeId<ShowRoomFixturePanel>("ShowRoomPanel");
            TypeIdRegistrar.RegisterTypeId<HiddenInfraFixturePanel>("ShowRoomPanel-hidden-infra");
            TypeIdRegistrar.RegisterTypeId<SingleInstanceFixturePanel>("ShowRoomPanel-single-inst");
            TypeIdRegistrar.RegisterTypeId<ColorControllerFixturePanel>("ColorController");
            TypeIdRegistrar.RegisterTypeId<CommandSenderFixturePanel>("CommandSender");
        }

        [TestMethod]
        public void DockLayoutFixture_WinUI3ShowRoom_LoadsItsZoneTreeAndPanelState ()
        {
            const string kWinUI3ShowRoomLayout = "winui3-showroom-dock.layout";
            var manager = new TestDockingManager { UseRealLayoutFiles = true };
            DockZoneViewModel root = manager.RegisterRoot("TestDockZone").ViewModel;

            Assert.IsTrue(manager.LoadDockLayoutFromFile(FixturePath(kWinUI3ShowRoomLayout)));

            Assert.AreEqual(eDockOrientation.Vertical, root.Orientation);
            Assert.AreEqual(2, root.Children.Count);

            DockZoneViewModel tabs = root.Children[0];
            Assert.AreEqual(eDockOrientation.Tabbed, tabs.Orientation);
            CollectionAssert.AreEqual(
                new[] { "Panel A", "Panel B", "Panel" },
                tabs.DockedContent.Select(a => ((ShowRoomFixturePanel)a.Display).AppliedLabel).ToList()
            );
            Assert.AreEqual(0, tabs.SelectedIndex);

            DockZoneViewModel split = root.Children[1];
            Assert.AreEqual(eDockOrientation.Horizontal, split.Orientation);
            Assert.AreEqual(2, split.Children.Count);
            Assert.AreEqual(eDockOrientation.Single, split.Children[0].Orientation);
            Assert.IsInstanceOfType(split.Children[0].DockedContent.Single().Display, typeof(HiddenInfraFixturePanel));
            Assert.AreEqual("Hidden Infra", ((ShowRoomFixturePanel)split.Children[0].DockedContent.Single().Display).AppliedLabel);
            Assert.AreEqual(eDockOrientation.Single, split.Children[1].Orientation);
            Assert.IsInstanceOfType(split.Children[1].DockedContent.Single().Display, typeof(SingleInstanceFixturePanel));

            CollectionAssert.AreEqual(new[] { manager.RootWindow }, manager.AllWindows.ToList());
        }

        // A layout with no tearoffs is the same shape on WPF and WinUI3, so the shared reader takes WPF's as it is
        [TestMethod]
        public void DockLayoutFixture_WpfShowRoom_LoadsWithTheSharedReader ()
        {
            const string kWpfShowRoomLayout = "wpf-showroom-docking-overview.layout";
            var manager = new TestDockingManager { UseRealLayoutFiles = true };
            DockZoneViewModel root = manager.RegisterRoot("RootZone").ViewModel;

            Assert.IsTrue(manager.LoadDockLayoutFromFile(FixturePath(kWpfShowRoomLayout)));

            Assert.AreEqual(eDockOrientation.Horizontal, root.Orientation);
            Assert.AreEqual(2, root.Children.Count);

            DockZoneViewModel left = root.Children[0];
            Assert.AreEqual(eDockOrientation.Single, left.Orientation);
            Assert.IsInstanceOfType(left.DockedContent.Single().Display, typeof(ColorControllerFixturePanel));

            DockZoneViewModel right = root.Children[1];
            Assert.AreEqual(eDockOrientation.Vertical, right.Orientation);
            Assert.AreEqual(2, right.Children.Count);
            Assert.IsInstanceOfType(right.Children[0].DockedContent.Single().Display, typeof(ColorControllerFixturePanel));
            Assert.IsInstanceOfType(right.Children[1].DockedContent.Single().Display, typeof(CommandSenderFixturePanel));

            CollectionAssert.AreEqual(new[] { manager.RootWindow }, manager.AllWindows.ToList());
        }

        // The WinUI3 showroom's layout, read and written back out with a tearoff added, is held to the text AJson wrote for it
        //  through reflection, before the layout types had generated serializers, so a save writes what it always has
        [TestMethod]
        public void DockLayoutFixture_WinUI3ShowRoom_WritesBackTheSameText ()
        {
            DockLayoutFile.Contents contents;
            using (Stream stream = File.OpenRead(FixturePath("winui3-showroom-dock.layout")))
            {
                contents = DockLayoutFile.Read(stream);
            }

            Assert.IsNotNull(contents);
            contents.Ancillary = new[]
            {
                new DockLayoutFile.TearoffLayout
                {
                    WindowX = 120,
                    WindowY = 80,
                    WindowWidth = 900,
                    WindowHeight = 700,
                    IsFullscreen = true,
                    State = contents.Core.ZoneInfoByRoot["TestDockZone"].ChildZones[0],
                },
            };

            Json json = JsonHelper.BuildJsonForObject(contents);
            Assert.IsFalse(json.HasErrors, json.GetErrorReport());
            Assert.AreEqual(ReadFixtureText("winui3-showroom-dock.rewritten.layout"), json.ToString());
        }

        // ===========[ Helpers ]===============================================
        private static string FixturePath (string fileName) => Path.Combine(AppContext.BaseDirectory, "_TestData", fileName);

        /// <summary>A fixture's text with its line endings as AJson writes them, whatever a checkout made of them</summary>
        private static string ReadFixtureText (string fileName) => File.ReadAllText(FixturePath(fileName)).Replace("\r\n", "\n");
    }
}
