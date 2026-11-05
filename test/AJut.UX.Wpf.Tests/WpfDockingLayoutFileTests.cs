namespace AJut.UX.Tests
{
    using System;
    using System.IO;
    using System.Text.RegularExpressions;
    using System.Windows;
    using AJut.TypeManagement;
    using AJut.UX.Controls;
    using AJut.UX.Docking;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// WPF's DockingManager reading and writing its own layout file. WPF objects need an STA thread, so each test runs on
    /// one, and nothing is ever shown.
    /// </summary>
    [TestClass]
    public class WpfDockingLayoutFileTests
    {
        // A file with no root zone in it has nothing to put in the root window. Loading it as a success would empty the root
        //  and keep the caller from falling back to a layout that has something in it.
        [TestMethod]
        [DataRow("{ }", DisplayName = "An empty document")]
        [DataRow("{ \"__ajson\": 2 }", DisplayName = "The AJson marker alone")]
        [DataRow("{ \"Core\": null }", DisplayName = "A null Core")]
        [DataRow("{ \"Core\": \"oops\" }", DisplayName = "A Core that is not a document")]
        [DataRow("{ \"Core\": { \"ZoneInfoByRoot\": { } }, \"Ancillary\": [ ] }", DisplayName = "A Core with no zones")]
        [DataRow("{ \"Core\": { \"ZoneInfoByRoot\": { \"main\": null } } }", DisplayName = "A Core whose only zone is null")]
        public void WpfDockLayout_WithNoRootZone_IsRefusedAndTheLayoutStays (string fileText)
        {
            StaTestThread.Run(() =>
            {
                string path = TempLayoutPath();
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    File.WriteAllText(path, fileText);

                    var zone = new DockZone();
                    var window = new Window { Content = zone };
                    DockingManager manager = MakeManagerWithOnePanel(window, zone);

                    Assert.IsFalse(manager.LoadDockLayoutFromFile(path), "A layout with no root zone loads nothing, so it must not report that it loaded");
                    Assert.AreEqual(1, zone.ViewModel.DockedContent.Count, "A refused layout leaves the one showing as it was");
                    manager.Dispose();
                }
                finally
                {
                    File.Delete(path);
                }
            });
        }

        // A file with no Ancillary entry has no tearoffs, the way the shared layout file reads one
        [TestMethod]
        public void WpfDockLayout_WithNoTearoffEntry_LoadsItsRootZones ()
        {
            StaTestThread.Run(() =>
            {
                string path = TempLayoutPath();
                try
                {
                    // The saved layout names its panel by this id, and the load builds the panel from it
                    TypeIdRegistrar.RegisterTypeId<FakeDockPanel>("ajut-tests-fake-dock-panel");

                    var savedZone = new DockZone();
                    var savedWindow = new Window { Content = savedZone };
                    DockingManager saver = MakeManagerWithOnePanel(savedWindow, savedZone);
                    Assert.IsTrue(saver.SaveDockLayoutToFile(path));
                    saver.Dispose();

                    string savedText = File.ReadAllText(path);
                    string textWithNoTearoffEntry = Regex.Replace(savedText, ",\\s*\"Ancillary\"\\s*:\\s*\\[\\s*\\]", String.Empty);
                    Assert.AreNotEqual(savedText, textWithNoTearoffEntry, "The saved layout has an empty Ancillary entry to take out");
                    File.WriteAllText(path, textWithNoTearoffEntry);

                    var zone = new DockZone();
                    var window = new Window { Content = zone };
                    var manager = new DockingManager(window, "wpf-dock-layout-tests", TempLayoutPath());
                    manager.RegisterDisplayFactory<FakeDockPanel>();
                    manager.RegisterRootDockZones(zone);

                    Assert.IsTrue(manager.LoadDockLayoutFromFile(path), "A layout with no tearoffs listed loads, as one with an empty list does");
                    Assert.AreEqual(1, zone.ViewModel.DockedContent.Count, "Its root zone's panel comes back");
                    manager.Dispose();
                }
                finally
                {
                    File.Delete(path);
                }
            });
        }

        // A layout saved with no root zone loads nothing, so it must never take the place of one that loads
        [TestMethod]
        public void WpfDockLayout_SaveAfterDispose_LeavesTheFileAlone ()
        {
            StaTestThread.Run(() =>
            {
                string path = TempLayoutPath();
                try
                {
                    var zone = new DockZone();
                    var window = new Window { Content = zone };
                    DockingManager manager = MakeManagerWithOnePanel(window, zone);
                    Assert.IsTrue(manager.SaveDockLayoutToFile(path));
                    byte[] before = File.ReadAllBytes(path);

                    // Dispose takes every root zone out of the registry
                    manager.Dispose();

                    Assert.IsFalse(manager.SaveDockLayoutToFile(path), "A save with no root zone to write reports that it saved nothing");
                    CollectionAssert.AreEqual(before, File.ReadAllBytes(path), "The layout already in the file is left byte for byte");
                }
                finally
                {
                    File.Delete(path);
                }
            });
        }

        // ===========[ Helpers ]===============================================
        private static DockingManager MakeManagerWithOnePanel (Window window, DockZone rootZone)
        {
            var manager = new DockingManager(window, "wpf-dock-layout-tests", TempLayoutPath());
            manager.RegisterDisplayFactory<FakeDockPanel>();
            manager.RegisterRootDockZones(rootZone);
            manager.DockNewPanel<FakeDockPanel>(rootZone.ViewModel);
            return manager;
        }

        private static string TempLayoutPath () => Path.Combine(Path.GetTempPath(), "ajut-dock-tests", Guid.NewGuid().ToString("N") + ".layout");
    }
}
