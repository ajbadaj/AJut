namespace AJut.UX.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using AJut.Storage;
    using AJut.TypeManagement;
    using AJut.UX.Docking;

    // ===========[ Docking test doubles ]==================================
    // AJut.UX.Wpf.Tests compiles this file too, for its tests of WPF's docking over the shared manager

    /// <summary>A window as the docking manager sees it: somewhere on screen, and open or closed</summary>
    public class FakeDockWindow
    {
        public FakeDockWindow (string name, DockWindowBounds bounds)
        {
            this.Name = name;
            this.Bounds = bounds;
        }

        public string Name { get; }
        public DockWindowBounds Bounds { get; set; }
        public bool IsClosed { get; set; }
        public int ActivationCount { get; set; }
        public bool IsMaximized { get; set; }
        public bool IsFullscreen { get; set; }

        public override string ToString () => this.Name;
    }

    /// <summary>A root zone control: a view model, a group id, and any zone controls nested in it</summary>
    public class FakeDockZone : IDockZoneUI
    {
        public FakeDockZone (DockZoneViewModel viewModel, string groupId = null)
        {
            this.ViewModel = viewModel;
            this.GroupId = groupId;
        }

        public DockZoneViewModel ViewModel { get; set; }
        public string GroupId { get; set; }
        public List<FakeDockZone> NestedZones { get; } = new List<FakeDockZone>();
        public bool WasDeregistered { get; set; }
        public DockZoneSize RenderSize { get; set; }
        public List<List<double>> TargetSizeCalls { get; } = new List<List<double>>();

        public void SetTargetSizeAsync (List<double> sizes) => this.TargetSizeCalls.Add(sizes);
    }

    /// <summary>A panel that keeps the adapter it was set up with</summary>
    [TypeId("ajut-tests-fake-dock-panel")]
    public class FakeDockPanel : IDockableDisplayElement
    {
        public DockingContentAdapterModel DockingAdapter { get; private set; }
        public bool? IsClosableSeenInSetup { get; private set; }

        public void Setup (DockingContentAdapterModel adapter)
        {
            this.DockingAdapter = adapter;
            this.IsClosableSeenInSetup = adapter.IsClosable;
        }
    }

    public class OtherFakeDockPanel : FakeDockPanel { }

    /// <summary>A panel that can veto its own close</summary>
    public class VetoingFakeDockPanel : FakeDockPanel
    {
        public bool AllowClose { get; set; }

        public void HookVeto ()
        {
            this.DockingAdapter.CanClose -= this.OnCanClose;
            this.DockingAdapter.CanClose += this.OnCanClose;
        }

        private void OnCanClose (object sender, IsReadyToCloseEventArgs e)
        {
            e.IsReadyToClose = this.AllowClose;
        }
    }

    /// <summary>
    /// A DockingManagerBase over fake windows and zones: tearoffs are created and closed in memory, and every hook call is
    /// recorded, so the shared orchestration runs with no UI toolkit
    /// </summary>
    public class TestDockingManager : DockingManagerBase<FakeDockWindow, FakeDockZone>
    {
        private readonly HashSet<FakeDockWindow> m_tracked = new HashSet<FakeDockWindow>();
        private int m_tearoffCount;

        public TestDockingManager (string persistentPath = "persistent.layout")
            : base(new FakeDockWindow("root", new DockWindowBounds(100, 200, 1000, 800)), "test", persistentPath, eDockingAutoSaveMethod.None)
        {
            m_tracked.Add(this.RootWindow);
        }

        public List<string> HookLog { get; } = new List<string>();
        public bool FailNextTearoffWindow { get; set; }
        public Func<string, bool> OnLoad { get; set; } = _ => false;
        public Func<string, bool> OnSave { get; set; } = _ => true;
        public List<string> Loaded { get; } = new List<string>();
        public List<string> Saved { get; } = new List<string>();
        public bool UseRealLayoutFiles { get; set; }

        /// <summary>When set, windows are placed in pixels this many times the size creation takes, the way WinUI3 places them by DPI</summary>
        public double PlacementScale { get; set; } = 1.0;

        /// <summary>When set, window positions are placed in units this many times the ones creation takes too, the way AppKit's backing scale does</summary>
        public double PositionScale { get; set; } = 1.0;
        public bool WasLoadingDuringLoad { get; private set; }
        public int DisposingCount { get; private set; }

        public FakeDockZone RegisterRoot (string groupId = null, FakeDockWindow window = null)
        {
            var zone = new FakeDockZone(new DockZoneViewModel(this), groupId);
            this.RecordRootZone(window ?? this.RootWindow, zone);
            return zone;
        }

        // Exposes the base's protected helpers to the tests
        public Result<FakeDockWindow> DoTearoff (IDockableDisplayElement element, DockWindowBounds bounds) => this.DoTearoffCore(element, bounds);
        public Result<FakeDockWindow> DoGroupTearoff (DockZoneViewModel zone, DockWindowBounds bounds) => this.DoGroupTearoffCore(zone, bounds);
        public void Drop (DockZoneViewModel target, DockZoneViewModel source, eDockInsertionDirection direction, FakeDockWindow sourceWindow, FakeDockWindow targetWindow)
            => this.CommitDrop(target, source, direction, sourceWindow, targetWindow);
        public bool ReturnCanceled (DockDragOrigin origin, DockZoneViewModel tornOffRoot, FakeDockWindow window) => this.ReturnCanceledDrag(origin, tornOffRoot, window);
        public bool UserClosesTearoff (FakeDockWindow window) => this.DoTearoffWindowClose(window);
        public void TearoffCloseButton (FakeDockWindow window) => this.CloseTearoffWindowOnRequest(window);
        public void RootWindowClosed () => this.CloseTearoffsForRootWindowClosed();
        public void ClearForLoad () => this.ClearForLoadingFromState();
        public void AutoSaveNow () => this.TriggerLayoutAutoSave();
        public FakeDockWindow WindowFor (FakeDockZone zone) => this.GetWindowForZone(zone);
        public FakeDockZone RootZoneFor (FakeDockWindow window) => this.GetRootZoneForWindow(window);
        public IReadOnlyList<FakeDockZone> AllRootZones => this.RootZones;
        public IReadOnlyCollection<FakeDockWindow> AllWindows => this.WindowsWithZones;
        public bool IsTracked (FakeDockWindow window) => m_tracked.Contains(window);

        public IReadOnlyList<FakeDockZone> ZonesFor (FakeDockWindow window)
        {
            this.TryGetZonesForWindow(window, out IReadOnlyList<FakeDockZone> zones);
            return zones;
        }

        public FakeDockWindow TearoffHosting (DockingContentAdapterModel adapter)
            => this.AllWindows.FirstOrDefault(w => w != this.RootWindow && this.ZonesFor(w).Any(z => z.ViewModel == adapter.Location));

        protected override DockZoneViewModel GetZoneViewModel (FakeDockZone zone) => zone.ViewModel;
        protected override string GetZoneGroupId (FakeDockZone zone) => zone.GroupId;

        protected override IEnumerable<FakeDockZone> EnumerateZoneTree (FakeDockZone rootZone)
        {
            yield return rootZone;
            foreach (FakeDockZone nested in rootZone.NestedZones)
            {
                foreach (FakeDockZone zone in this.EnumerateZoneTree(nested))
                {
                    yield return zone;
                }
            }
        }

        protected override Result<FakeDockWindow> CreateAndStockTearoffWindow (DockZoneViewModel rootZone, DockWindowBounds bounds)
        {
            if (this.FailNextTearoffWindow)
            {
                this.FailNextTearoffWindow = false;
                return Result<FakeDockWindow>.Error("The test asked this tearoff to fail");
            }

            var window = new FakeDockWindow($"tearoff{++m_tearoffCount}", new DockWindowBounds(bounds.X * this.PositionScale, bounds.Y * this.PositionScale, bounds.Width * this.PlacementScale, bounds.Height * this.PlacementScale));
            this.HookLog.Add($"create {window}");
            this.RecordTearoffRootZone(window, rootZone);
            m_tracked.Add(window);
            this.RecordRootZone(window, new FakeDockZone(rootZone));
            rootZone.ClearPassAlongUISize();
            return Result<FakeDockWindow>.Success(window);
        }

        protected override DockWindowBounds GetWindowBounds (FakeDockWindow window) => window.Bounds;
        protected override bool IsWindowTracked (FakeDockWindow window) => m_tracked.Contains(window);

        protected override void StopTrackingWindow (FakeDockWindow window)
        {
            this.HookLog.Add($"stop tracking {window}");
            m_tracked.Remove(window);
        }

        protected override void DetachTearoffSubscriptions (FakeDockWindow window) => this.HookLog.Add($"detach {window}");

        protected override void CloseWindow (FakeDockWindow window)
        {
            this.HookLog.Add($"close {window}");
            window.IsClosed = true;
        }

        protected override void ActivateWindow (FakeDockWindow window) => ++window.ActivationCount;

        protected override bool ResetLayoutFromFile (string filePath)
        {
            this.Loaded.Add(filePath);
            this.WasLoadingDuringLoad = this.IsLoadingFromLayout;
            return this.UseRealLayoutFiles ? base.ResetLayoutFromFile(filePath) : this.OnLoad(filePath);
        }

        protected override bool SaveLayoutToFile (string filePath)
        {
            this.Saved.Add(filePath);
            return this.UseRealLayoutFiles ? base.SaveLayoutToFile(filePath) : this.OnSave(filePath);
        }

        protected override bool TryCaptureWindowLayoutState (FakeDockWindow window, out DockWindowLayoutState state)
        {
            state = new DockWindowLayoutState(window.Bounds, window.IsMaximized, window.IsFullscreen);
            return !window.IsClosed;
        }

        protected override void ApplyWindowLayoutState (FakeDockWindow window, DockWindowLayoutState state)
        {
            window.IsMaximized = state.IsMaximized;
            window.IsFullscreen = state.IsFullscreen;
        }

        protected override DockWindowBounds ToTearoffCreationBounds (DockWindowBounds placementBounds)
            => new DockWindowBounds(placementBounds.X / this.PositionScale, placementBounds.Y / this.PositionScale, placementBounds.Width / this.PlacementScale, placementBounds.Height / this.PlacementScale);

        protected override void OnRootZoneDeregistered (FakeDockZone zone) => zone.WasDeregistered = true;
        protected override void OnDisposing () => ++this.DisposingCount;
    }
}
