namespace AJutShowRoomWinUI.UI.Diagnostics
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Media;

    /// <summary>
    /// The measurement helpers every leak probe in the showroom shares. On WinUI3 a synchronous detach-then-GC is
    /// not a valid read: the framework releases detached element refs on a later dispatcher tick and controls queue
    /// deferred callbacks that capture the data being measured. A correct probe detaches, waits for the element to
    /// unload, drains the dispatcher, and only then collects, which is what these give you.
    /// </summary>
    public static class LeakProbeHelpers
    {
        // Polls IsLoaded rather than subscribing Loaded (no anonymous handler, and re-entrant safe).
        public static async Task WaitForLoaded (FrameworkElement element, int timeoutMs)
        {
            int waited = 0;
            while (element != null && !element.IsLoaded && waited < timeoutMs)
            {
                await Task.Delay(50);
                waited += 50;
            }
        }

        // Waits for the element to actually leave the live tree. WinUI releases its native /
        // item-container references during the Unloaded pass (a later dispatcher tick), not at
        // the moment it is detached - so measuring before this completes reads a still-pinned element
        // that would collect a beat later (the source of the flaky reads).
        public static async Task WaitForUnloaded (FrameworkElement element, int timeoutMs)
        {
            int waited = 0;
            while (element != null && element.IsLoaded && waited < timeoutMs)
            {
                await Task.Delay(50);
                waited += 50;
            }
        }

        // Deterministic collect: drain the dispatcher, then run a full GC, repeated. The drain
        // matters because some controls defer work via DispatcherQueue.TryEnqueue that captures
        // data being measured - e.g. PropertyGridItemRow.OnDataContextChanged enqueues a
        // (Normal-priority) ApplyEditorContent callback capturing the edit target -> source. Those
        // are self-cleaning once they run, but if still queued at GC time they pin what they
        // captured. Awaiting a Low-priority enqueue returns only after every higher-priority
        // queued callback has run, so the captures are gone before the measurement.
        public static async Task SettleAndCollectAsync ()
        {
            for (int i = 0; i < 6; ++i)
            {
                await DrainDispatcherAsync();
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                await Task.Delay(100);
            }
        }

        // Completes only after all higher-priority dispatcher work has run (Low runs last).
        public static Task DrainDispatcherAsync ()
        {
            var done = new TaskCompletionSource();
            var queue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
            bool enqueued = queue != null && queue.TryEnqueue(
                Microsoft.UI.Dispatching.DispatcherQueuePriority.Low,
                () => done.SetResult()
            );

            if (!enqueued)
            {
                done.SetResult();
            }

            return done.Task;
        }

        public static int CountDescendants<T> (DependencyObject root) where T : DependencyObject
        {
            if (root == null)
            {
                return 0;
            }

            int count = root is T ? 1 : 0;
            int childCount = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < childCount; ++i)
            {
                count += CountDescendants<T>(VisualTreeHelper.GetChild(root, i));
            }

            return count;
        }

        public static List<T> CollectDescendants<T> (DependencyObject root) where T : DependencyObject
        {
            var results = new List<T>();
            CollectDescendantsInto(root, results);
            return results;
        }

        public static void CollectDescendantsInto<T> (DependencyObject node, List<T> results) where T : DependencyObject
        {
            if (node == null)
            {
                return;
            }

            if (node is T match)
            {
                results.Add(match);
            }

            int childCount = VisualTreeHelper.GetChildrenCount(node);
            for (int i = 0; i < childCount; ++i)
            {
                CollectDescendantsInto(VisualTreeHelper.GetChild(node, i), results);
            }
        }

        // Counts only - the GC happens in SettleAndCollectAsync before this is called.
        public static string ReportSurvivors (List<DockingLeakProbeWeaks> weaks)
        {
            int zones = 0;
            int panels = 0;
            int pages = 0;
            int windows = 0;
            int builtLeaves = 0;
            foreach (var w in weaks)
            {
                if (w.RootZone.IsAlive) { ++zones; }
                if (w.PanelA != null && w.PanelA.IsAlive) { ++panels; }
                if (w.EditorPage.IsAlive) { ++pages; }
                if (w.ChildWindow != null && w.ChildWindow.IsAlive) { ++windows; }
                builtLeaves += w.BuiltLeaves;
            }

            // windows is informational only - WinUI is known to keep a closed Window alive a
            // while; the dock leak is zones/panels/pages surviving past Dispose. builtLeaves
            // proves the dock structure actually realized - builtLeaves=0 means a vacuous run.
            bool leaked = zones > 0 || panels > 0 || pages > 0;
            string detail = $"zones={zones}/{weaks.Count} panels={panels}/{weaks.Count} pages={pages}/{weaks.Count} windows={windows}/{weaks.Count} builtLeaves={builtLeaves}";
            if (builtLeaves == 0)
            {
                return $"INCONCLUSIVE - dock layout never realized (builtLeaves=0): {detail}";
            }

            return leaked
                ? $"FAIL - survivors after GC: {detail}"
                : $"PASS - all collected ({detail})";
        }

        /// <summary>
        /// The weak handles a docking leak probe keeps after teardown: one per node that must collect, plus the count
        /// of dock leaves that were actually built, which <see cref="ReportSurvivors"/> uses to tell a real pass from a
        /// vacuous one.
        /// </summary>
        public sealed class DockingLeakProbeWeaks
        {
            public WeakReference RootZone { get; set; }
            public WeakReference PanelA { get; set; }
            public WeakReference EditorPage { get; set; }
            public WeakReference ChildWindow { get; set; }
            public int BuiltLeaves { get; set; }
        }
    }
}
