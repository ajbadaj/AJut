namespace AJutShowRoomWinUI.UI.Diagnostics
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using AJut.UX;
    using AJut.UX.Docking;
    using AJutShowRoomWinUI.UI.Samples;
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;
    using Microsoft.UI.Xaml.Media;

    /// <summary>
    /// Spot-check probes for memory leak regressions in DockingManager, WindowManager and PropertyGrid. Each one
    /// builds the target against the main window, disposes it, drops the strong ref, collects with the WinUI3
    /// measurement discipline from <see cref="LeakProbeHelpers"/>, and reports whether anything survived.
    /// </summary>
    public sealed partial class LeakProbePage : Page
    {
        public LeakProbePage ()
        {
            this.InitializeComponent();
        }

        // ===========[ Leak Probe ]======================================================

        private void LeakProbe_OnDockingManagerClicked (object sender, RoutedEventArgs e)
        {
            bool collected = LeakProbe_BuildAndDisposeDockingManager(App.Instance.MainWindow);
            this.LeakProbe_AppendOutput("Probe DockingManager", collected
                ? "PASS - DockingManager was collected after Dispose"
                : "FAIL - DockingManager survived Dispose + GC (something is still pinning it)");
        }

        private void LeakProbe_OnWindowManagerClicked (object sender, RoutedEventArgs e)
        {
            bool collected = LeakProbe_BuildAndDisposeWindowManager(App.Instance.MainWindow);
            this.LeakProbe_AppendOutput("Probe WindowManager", collected
                ? "PASS - WindowManager was collected after Dispose"
                : "FAIL - WindowManager survived Dispose + GC (something is still pinning it)");
        }

        private void LeakProbe_OnCycleClicked (object sender, RoutedEventArgs e)
        {
            int passes = 0;
            for (int i = 0; i < 5; ++i)
            {
                if (LeakProbe_BuildAndDisposeDockingManager(App.Instance.MainWindow))
                {
                    ++passes;
                }
            }

            this.LeakProbe_AppendOutput("Probe x5 (open/close cycles)", passes == 5
                ? "PASS - all 5 cycles collected cleanly"
                : $"FAIL - only {passes}/5 cycles collected (later iterations should still pass if the first one does)");
        }

        // Build a real DockingManager against the live root window, dispose it, drop the
        // strong ref, and check the WeakReference. Static so the local `manager` variable
        // is the only place a strong ref lives - the JIT sometimes keeps locals from the
        // enclosing instance method alive longer than expected.
        private static bool LeakProbe_BuildAndDisposeDockingManager (Window root)
        {
            WeakReference weak = LeakProbe_BuildAndDisposeDockingManager_Inner(root);
            return LeakProbe_ConfirmCollected(weak);
        }

        private static WeakReference LeakProbe_BuildAndDisposeDockingManager_Inner (Window root)
        {
            var manager = new DockingManager(root, "leak-probe-" + Guid.NewGuid().ToString("N"));
            var weak = new WeakReference(manager);
            manager.Dispose();
            return weak;
        }

        private static bool LeakProbe_BuildAndDisposeWindowManager (Window root)
        {
            WeakReference weak = LeakProbe_BuildAndDisposeWindowManager_Inner(root);
            return LeakProbe_ConfirmCollected(weak);
        }

        private static WeakReference LeakProbe_BuildAndDisposeWindowManager_Inner (Window root)
        {
            var manager = new WindowManager();
            manager.Setup(root);
            var weak = new WeakReference(manager);
            manager.Dispose();
            return weak;
        }

        private static bool LeakProbe_ConfirmCollected (WeakReference weak)
        {
            // Two pass GC + finalizer wait. Single pass usually does it but second pass
            // catches anything that pulled itself off the finalizer queue on the way out.
            for (int i = 0; i < 2; ++i)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            }

            return !weak.IsAlive;
        }

        // ===========[ Leak Probe - Docking host scenario ]==============================
        // Models the residual docking leak: an editor whose layout is a DockingManager lives inside
        // a transient child Window's Frame, while the manager's RootWindow is the long-lived
        // main window. After the child window closes and the manager is disposed, the dock
        // zone, docked panels, and anything bound to the editor page must all collect. The
        // main-window variant is the control case - hosting the same graph in the main window
        // tree, which the prior m_dockZoneMapping fix already collects cleanly.

        private async void LeakProbe_OnChildWindowDockingClicked (object sender, RoutedEventArgs e)
        {
            const int kCycles = 4;
            var button = sender as Button;
            if (button != null) { button.IsEnabled = false; }
            try
            {
                var weaks = new List<LeakProbeHelpers.DockingLeakProbeWeaks>();
                for (int i = 0; i < kCycles; ++i)
                {
                    var probe = LeakProbe_BuildChildWindowDocking(App.Instance.MainWindow);

                    // Wait until the page actually loads so the dock layout realizes (template ->
                    // drop overlay + DockLeafLayout). Without this the probe may measure a window that
                    // closed before it ever built anything - a vacuous pass. builtLeaves in the
                    // report proves the structure was really there.
                    await LeakProbeHelpers.WaitForLoaded(probe.EditorPage, 3000);
                    await Task.Delay(200);

                    int leaves = LeakProbeHelpers.CountDescendants<AJut.UX.Controls.DockLeafLayout>(probe.RootZone);

                    // Hold a watch handle to the page so the probe can wait for its real unload after
                    // teardown nulls every probe-held strong ref.
                    FrameworkElement pageWatch = probe.EditorPage;

                    var weak = LeakProbe_TeardownDockingProbe(probe);
                    weak.BuiltLeaves = leaves;
                    weaks.Add(weak);
                    probe = null;

                    // Deterministic read: wait for WinUI to actually unload the page (it releases
                    // native / container refs during the Unloaded pass), then let deferred cleanup
                    // run, before collecting and measuring.
                    await LeakProbeHelpers.WaitForUnloaded(pageWatch, 3000);
                    pageWatch = null;
                    await Task.Delay(150);
                }

                await LeakProbeHelpers.SettleAndCollectAsync();
                this.LeakProbe_AppendOutput("Dock leak - child window", LeakProbeHelpers.ReportSurvivors(weaks));
            }
            finally
            {
                if (button != null) { button.IsEnabled = true; }
            }
        }

        private async void LeakProbe_OnMainWindowDockingClicked (object sender, RoutedEventArgs e)
        {
            const int kCycles = 4;
            var button = sender as Button;
            if (button != null) { button.IsEnabled = false; }
            try
            {
                var weaks = new List<LeakProbeHelpers.DockingLeakProbeWeaks>();
                for (int i = 0; i < kCycles; ++i)
                {
                    var probe = this.LeakProbe_BuildMainWindowDocking();

                    await LeakProbeHelpers.WaitForLoaded(probe.EditorPage, 3000);
                    await Task.Delay(200);

                    int leaves = LeakProbeHelpers.CountDescendants<AJut.UX.Controls.DockLeafLayout>(probe.RootZone);

                    FrameworkElement pageWatch = probe.EditorPage;

                    var weak = LeakProbe_TeardownDockingProbe(probe);
                    weak.BuiltLeaves = leaves;
                    weaks.Add(weak);
                    probe = null;

                    await LeakProbeHelpers.WaitForUnloaded(pageWatch, 3000);
                    pageWatch = null;
                    await Task.Delay(150);
                }

                await LeakProbeHelpers.SettleAndCollectAsync();
                this.LeakProbe_AppendOutput("Dock leak - main window (control)", LeakProbeHelpers.ReportSurvivors(weaks));
            }
            finally
            {
                if (button != null) { button.IsEnabled = true; }
            }
        }

        // Deterministic mechanism check for the drop-overlay back-ref fix. The native-peer pin that
        // makes the leak bite cannot be reproduced headless, but the FIX is exact and testable: after
        // DockingManager.Dispose, every DockDropInsertionDriverWidget must have had its InsertionZone
        // back-ref nulled and been detached from the zone's outer grid. This builds a real docked graph
        // (so templates apply and the 5 widgets per zone exist), grabs them while live, disposes, and
        // asserts the sever. Fails before the fix (InsertionZone still points at the zone), passes after.
        private async void LeakProbe_OnDropOverlaySeveredClicked (object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button != null) { button.IsEnabled = false; }
            try
            {
                var probe = this.LeakProbe_BuildMainWindowDocking();

                // Wait for the page to load so each DockZone applies its template and BuildDropOverlay
                // builds the driver widgets - the things the assertion below checks get severed.
                await LeakProbeHelpers.WaitForLoaded(probe.EditorPage, 3000);
                await Task.Delay(200);

                // Collect the driver widgets across the whole zone tree while they are still live in the
                // overlay. Hold strong refs so Dispose can't collect them out from under the assertion -
                // this probe checks the back-ref is nulled, not whether the widget itself collects.
                var widgets = LeakProbeHelpers.CollectDescendants<AJut.UX.Controls.DockDropInsertionDriverWidget>(probe.RootZone);
                int built = widgets.Count;
                int boundBefore = widgets.Count(w => w.InsertionZone != null);

                // The operation under test.
                probe.Manager.Dispose();

                int stillBound = widgets.Count(w => w.InsertionZone != null);
                int stillParented = widgets.Count(w => VisualTreeHelper.GetParent(w) != null);

                // Assertion done - drop the host parenting and strong refs.
                if (probe.MainHost != null) { probe.MainHost.Child = null; }
                if (probe.EditorPage is ContentControl page) { page.Content = null; }
                widgets.Clear();
                probe = null;

                string result;
                if (built == 0)
                {
                    result = "INCONCLUSIVE - no drop-overlay widgets realized (template never applied?)";
                }
                else if (stillBound == 0 && stillParented == 0)
                {
                    result = $"PASS - all {built} driver widgets severed (InsertionZone null) and detached after Dispose (boundBefore={boundBefore})";
                }
                else
                {
                    result = $"FAIL - after Dispose: {stillBound}/{built} still hold InsertionZone, {stillParented}/{built} still parented (boundBefore={boundBefore})";
                }

                this.LeakProbe_AppendOutput("Dock overlay severed (mechanism)", result);
            }
            finally
            {
                if (button != null) { button.IsEnabled = true; }
            }
        }

        // Builds the docking graph inside a fresh child Window (RootWindow stays the main window).
        private static DockingLeakProbe LeakProbe_BuildChildWindowDocking (Window mainRoot)
        {
            var childWindow = new Window();
            var frame = new Frame();
            var editorPage = LeakProbe_BuildEditorPageStandin(out var rootZone);

            frame.Content = editorPage;
            childWindow.Content = frame;
            childWindow.Activate();

            var probe = LeakProbe_StockDockingGraph(mainRoot, rootZone, editorPage);
            probe.ChildWindow = childWindow;
            probe.Frame = frame;
            probe.EditorPage = editorPage;
            return probe;
        }

        // Builds the same docking graph but hosts it in the main window's own visual tree.
        private DockingLeakProbe LeakProbe_BuildMainWindowDocking ()
        {
            var frame = new Frame();
            var editorPage = LeakProbe_BuildEditorPageStandin(out var rootZone);

            frame.Content = editorPage;
            this.LeakProbe_MainHostArea.Child = frame;

            var probe = LeakProbe_StockDockingGraph(App.Instance.MainWindow, rootZone, editorPage);
            probe.Frame = frame;
            probe.EditorPage = editorPage;
            probe.MainHost = this.LeakProbe_MainHostArea;
            return probe;
        }

        // The editor "page" stand-in: a ContentControl whose content is the root DockZone,
        // mirroring an editor Page whose layout root is an AJut dock zone.
        private static FrameworkElement LeakProbe_BuildEditorPageStandin (out AJut.UX.Controls.DockZone rootZone)
        {
            rootZone = new AJut.UX.Controls.DockZone { Name = "LeakProbeRootZone" };
            return new ContentControl
            {
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                VerticalContentAlignment = VerticalAlignment.Stretch,
                Content = rootZone,
            };
        }

        // Stands up a DockingManager against the given root window, registers the zone, docks a
        // couple of panels, and binds one panel back to the editor page (page as binding Source)
        // so a surviving panel pins the page exactly the way the real consumer's editor does.
        private static DockingLeakProbe LeakProbe_StockDockingGraph (Window root, AJut.UX.Controls.DockZone rootZone, FrameworkElement editorPage)
        {
            var manager = new DockingManager(root, "dock-leak-" + Guid.NewGuid().ToString("N"));
            manager.RegisterDisplayFactory<ShowRoomPanel>();
            manager.RegisterMainWindowRootDockZones(rootZone);

            ShowRoomPanel panelA = null;
            ShowRoomPanel panelB = null;

            var zone = manager.FindFirstAvailableDockZone();
            if (zone != null)
            {
                panelA = manager.DockNewPanel<ShowRoomPanel>(zone);
                panelA.Label = "Leak A";
                panelA.DockingAdapter.TitleContent = "Leak A";

                panelB = manager.DockNewPanel<ShowRoomPanel>(zone);
                panelB.Label = "Leak B";
                panelB.DockingAdapter.TitleContent = "Leak B";
            }

            // Bind a docked panel with the editor page as the binding Source. WinUI keeps a
            // binding's Source alive while the target lives, so a surviving panel keeps the page.
            if (panelA != null)
            {
                panelA.SetBinding(FrameworkElement.TagProperty, new Microsoft.UI.Xaml.Data.Binding { Source = editorPage });
            }

            return new DockingLeakProbe
            {
                Manager = manager,
                RootZone = rootZone,
                PanelA = panelA,
                PanelB = panelB,
            };
        }

        // Drives the app-side teardown (manager.Dispose + content null-out + window close),
        // snapshots weak references to every node that must collect, then drops all strong refs.
        private static LeakProbeHelpers.DockingLeakProbeWeaks LeakProbe_TeardownDockingProbe (DockingLeakProbe probe)
        {
            probe.Manager.Dispose();

            if (probe.Frame != null)
            {
                probe.Frame.Content = null;
            }

            if (probe.EditorPage is ContentControl pageContent)
            {
                pageContent.Content = null;
            }

            if (probe.MainHost != null)
            {
                probe.MainHost.Child = null;
            }

            if (probe.ChildWindow != null)
            {
                probe.ChildWindow.Content = null;
                probe.ChildWindow.Close();
            }

            var weaks = new LeakProbeHelpers.DockingLeakProbeWeaks
            {
                RootZone = new WeakReference(probe.RootZone),
                PanelA = probe.PanelA != null ? new WeakReference(probe.PanelA) : null,
                EditorPage = new WeakReference(probe.EditorPage),
                ChildWindow = probe.ChildWindow != null ? new WeakReference(probe.ChildWindow) : null,
            };

            // Drop every strong reference the probe holds so the only remaining handles are weak.
            probe.Manager = null;
            probe.RootZone = null;
            probe.PanelA = null;
            probe.PanelB = null;
            probe.EditorPage = null;
            probe.Frame = null;
            probe.MainHost = null;
            probe.ChildWindow = null;

            return weaks;
        }

        // ===========[ Leak Probe - Output ]=============================================
        // Every probe routes its result here. Newest entry is prepended so the latest run
        // is always visible at the top without scrolling.

        private void LeakProbe_AppendOutput (string title, string body)
        {
            string stamp = DateTime.Now.ToString("HH:mm:ss");
            string entry = $"[{stamp}] {title}\n    {body}";
            this.LeakProbe_Output.Text = this.LeakProbe_Output.Text.Length == 0
                ? entry
                : entry + "\n\n" + this.LeakProbe_Output.Text;
        }

        private void LeakProbe_OnCopyOutputClicked (object sender, RoutedEventArgs e)
        {
            var package = new Windows.ApplicationModel.DataTransfer.DataPackage();
            package.SetText(this.LeakProbe_Output.Text ?? string.Empty);
            Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(package);
        }

        private void LeakProbe_OnClearOutputClicked (object sender, RoutedEventArgs e)
        {
            this.LeakProbe_Output.Text = string.Empty;
        }

        // ===========[ Leak Probe - PropertyGrid ]=======================================
        // Builds a PropertyGrid against a fresh source object in the main window's tree,
        // lets it discover + render the source's properties, then disposes it and checks
        // that both the grid and the source object collect. A surviving source means the
        // grid (or its manager / row tree) is holding a reference past Dispose.

        private async void LeakProbe_OnPropertyGridClicked (object sender, RoutedEventArgs e)
        {
            const int kCycles = 4;
            var button = sender as Button;
            if (button != null) { button.IsEnabled = false; }
            try
            {
                var weaks = new List<PropertyGridLeakProbeWeaks>();
                for (int i = 0; i < kCycles; ++i)
                {
                    var probe = this.LeakProbe_BuildPropertyGrid();

                    // Let the grid load + apply its template and build its property-row tree.
                    await LeakProbeHelpers.WaitForLoaded(probe.Grid, 3000);
                    await Task.Delay(300);

                    FrameworkElement gridWatch = probe.Grid;

                    weaks.Add(LeakProbe_TeardownPropertyGrid(probe, this.LeakProbe_MainHostArea));
                    probe = null;

                    // Deterministic read: wait for the grid to actually unload, then let deferred
                    // WinUI cleanup run, before collecting.
                    await LeakProbeHelpers.WaitForUnloaded(gridWatch, 3000);
                    gridWatch = null;
                    await Task.Delay(150);
                }

                await LeakProbeHelpers.SettleAndCollectAsync();
                this.LeakProbe_AppendOutput("Probe PropertyGrid", LeakProbe_ReportPropertyGridSurvivors(weaks));
            }
            finally
            {
                if (button != null) { button.IsEnabled = true; }
            }
        }

        private PropertyGridLeakProbe LeakProbe_BuildPropertyGrid ()
        {
            var grid = new AJut.UX.Controls.PropertyGrid();
            var source = new ShowRoomAlpha();
            grid.SingleItemSource = source;
            this.LeakProbe_MainHostArea.Child = grid;

            return new PropertyGridLeakProbe
            {
                Grid = grid,
                Source = source,
            };
        }

        private static PropertyGridLeakProbeWeaks LeakProbe_TeardownPropertyGrid (PropertyGridLeakProbe probe, Border host)
        {
            probe.Grid.Dispose();
            host.Child = null;

            var weaks = new PropertyGridLeakProbeWeaks
            {
                Grid = new WeakReference(probe.Grid),
                Source = new WeakReference(probe.Source),
            };

            probe.Grid = null;
            probe.Source = null;
            return weaks;
        }

        // Counts only - the GC happens in LeakProbeHelpers.SettleAndCollectAsync before this is called.
        private static string LeakProbe_ReportPropertyGridSurvivors (List<PropertyGridLeakProbeWeaks> weaks)
        {
            int grids = 0;
            int sources = 0;
            foreach (var w in weaks)
            {
                if (w.Grid.IsAlive) { ++grids; }
                if (w.Source.IsAlive) { ++sources; }
            }

            bool leaked = grids > 0 || sources > 0;
            string detail = $"grids={grids}/{weaks.Count} sources={sources}/{weaks.Count}";
            return leaked
                ? $"FAIL - survivors after GC: {detail}"
                : $"PASS - all collected ({detail})";
        }

        // ===========[ Subclasses/structs ]==============================================

        private sealed class DockingLeakProbe
        {
            public DockingManager Manager { get; set; }
            public AJut.UX.Controls.DockZone RootZone { get; set; }
            public ShowRoomPanel PanelA { get; set; }
            public ShowRoomPanel PanelB { get; set; }
            public FrameworkElement EditorPage { get; set; }
            public Frame Frame { get; set; }
            public Border MainHost { get; set; }
            public Window ChildWindow { get; set; }
        }

        private sealed class PropertyGridLeakProbe
        {
            public AJut.UX.Controls.PropertyGrid Grid { get; set; }
            public object Source { get; set; }
        }

        private sealed class PropertyGridLeakProbeWeaks
        {
            public WeakReference Grid { get; set; }
            public WeakReference Source { get; set; }
        }
    }
}
