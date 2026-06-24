namespace AJutShowRoomWinUI.UseCaseTest
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.UI.Dispatching;

    // ===========[ UseCaseLeakRegistry ]=========================================
    // Bridges the editor page's teardown to the landing page's leak probe. When the
    // editor page navigates away it records weak references to every object that MUST
    // collect (the page, its docking manager, root zone, both panels, the flat tree +
    // its store, the property grid, the tree-source root, and a sample tool item). The
    // landing page later forces a settle + GC and reports which of those survived.
    //
    // Everything here is weak by construction, so the registry itself never pins what it
    // is measuring. Built deliberately self-contained so a consumer can lift the whole
    // UseCaseTest folder as a starting point for its own teardown regression tests.
    public static class UseCaseLeakRegistry
    {
        private static readonly List<Snapshot> g_snapshots = new List<Snapshot>();

        public static void Reset ()
        {
            g_snapshots.Clear();
        }

        // Called from the editor page's teardown AFTER it has disposed/unhooked everything
        // and dropped its own strong references. builtRows proves the editor actually
        // realized (non-zero) so a vacuous run can't read as a pass.
        public static void CaptureEditorTeardown (
            object page,
            object manager,
            object rootZone,
            object itemsPanel,
            object propertiesPanel,
            object flatTree,
            object propertyGrid,
            object treeRoot,
            object sampleItem,
            int builtRows)
        {
            g_snapshots.Add(new Snapshot
            {
                Page = new WeakReference(page),
                Manager = new WeakReference(manager),
                RootZone = new WeakReference(rootZone),
                ItemsPanel = new WeakReference(itemsPanel),
                PropertiesPanel = new WeakReference(propertiesPanel),
                FlatTree = new WeakReference(flatTree),
                PropertyGrid = new WeakReference(propertyGrid),
                TreeRoot = new WeakReference(treeRoot),
                SampleItem = new WeakReference(sampleItem),
                BuiltRows = builtRows,
            });
        }

        public static bool HasSnapshots => g_snapshots.Count > 0;

        // Drains the dispatcher and forces a full GC several times, then tallies survivors.
        public static async Task<string> SettleAndReportAsync ()
        {
            if (g_snapshots.Count == 0)
            {
                return "INCONCLUSIVE - no editor teardown recorded yet. Open the editor, then come back and probe.";
            }

            for (int i = 0; i < 6; ++i)
            {
                await DrainDispatcherAsync();
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                await Task.Delay(100);
            }

            int total = g_snapshots.Count;
            int pages = 0, managers = 0, zones = 0, itemsPanels = 0, propsPanels = 0;
            int trees = 0, grids = 0, treeRoots = 0, items = 0, builtRows = 0;
            foreach (Snapshot s in g_snapshots)
            {
                if (s.Page.IsAlive) { ++pages; }
                if (s.Manager.IsAlive) { ++managers; }
                if (s.RootZone.IsAlive) { ++zones; }
                if (s.ItemsPanel.IsAlive) { ++itemsPanels; }
                if (s.PropertiesPanel.IsAlive) { ++propsPanels; }
                if (s.FlatTree.IsAlive) { ++trees; }
                if (s.PropertyGrid.IsAlive) { ++grids; }
                if (s.TreeRoot.IsAlive) { ++treeRoots; }
                if (s.SampleItem.IsAlive) { ++items; }
                builtRows += s.BuiltRows;
            }

            string detail =
                $"cycles={total} builtRows={builtRows} | survivors: page={pages} manager={managers} zone={zones} "
                + $"itemsPanel={itemsPanels} propsPanel={propsPanels} flatTree={trees} propGrid={grids} "
                + $"treeRoot={treeRoots} toolItem={items}";

            if (builtRows == 0)
            {
                return $"INCONCLUSIVE - editor never realized rows (builtRows=0): {detail}";
            }

            bool leaked = pages > 0 || managers > 0 || zones > 0 || itemsPanels > 0 || propsPanels > 0
                || trees > 0 || grids > 0 || treeRoots > 0 || items > 0;

            return leaked
                ? $"FAIL - something survived teardown + GC:\n    {detail}"
                : $"PASS - everything collected:\n    {detail}";
        }

        // Completes only after all higher-priority dispatcher work has run (Low runs last),
        // so deferred WinUI cleanup callbacks have fired before we measure.
        private static Task DrainDispatcherAsync ()
        {
            var done = new TaskCompletionSource();
            DispatcherQueue queue = DispatcherQueue.GetForCurrentThread();
            bool enqueued = queue != null && queue.TryEnqueue(DispatcherQueuePriority.Low, () => done.SetResult());
            if (!enqueued)
            {
                done.SetResult();
            }

            return done.Task;
        }

        private sealed class Snapshot
        {
            public WeakReference Page { get; set; }
            public WeakReference Manager { get; set; }
            public WeakReference RootZone { get; set; }
            public WeakReference ItemsPanel { get; set; }
            public WeakReference PropertiesPanel { get; set; }
            public WeakReference FlatTree { get; set; }
            public WeakReference PropertyGrid { get; set; }
            public WeakReference TreeRoot { get; set; }
            public WeakReference SampleItem { get; set; }
            public int BuiltRows { get; set; }
        }
    }
}
