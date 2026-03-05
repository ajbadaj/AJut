namespace AJut.UX.Docking
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Collections.Specialized;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Threading.Tasks;
    using AJut;
    using AJut.Storage;
    using AJut.Tree;
    using AJut.TypeManagement;
    using AJut.UX;
    using AJut.UX.Controls;
    using Microsoft.UI.Windowing;
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;
    using Microsoft.UI.Xaml.Media;
    using Windows.Foundation;

    // ===========[ DockingManager ]==========================================
    // Central orchestrator for a single WinUI3 docking experience.
    //
    // Mirrors AJut.UX.Wpf.DockingManager but uses WinUI3 APIs:
    //   - Microsoft.UI.Xaml.Window for tearoff windows
    //   - AppWindow.Move / Resize instead of Window.Left / .Width etc.
    //   - AppWindow.Closing (cancelable) for tearoff close interception
    //   - Win32 GetCursorPos / GetAsyncKeyState P/Invokes for RunDragSearch
    //   - VisualTreeHelper.FindElementsInHostCoordinates for drop hit-testing
    //
    // Phase 3C note: RunDragSearch is implemented but DockDropInsertionDriverWidget
    // (the directional drop-target arrows shown on hover) are not yet present. The
    // loop will move the ghost and set DockZone.IsDirectDropTarget, but no actual
    // drop will execute until Phase 3C adds the insertion widgets.

    public class DockingManager : NotifyPropertyChanged, IDockingManager, IDisposable
    {
        // ===========[ Constants ]============================================
        private const double kTearoffDragOpacity = 0.65;

        // ===========[ Fields ]===============================================
        private readonly Dictionary<Type, DisplayBuilder> m_factory = new();
        private readonly ObservableCollection<DockZone> m_rootDockZones = new();
        private readonly Dictionary<Window, List<DockZone>> m_dockZoneMapping = new();
        private readonly Dictionary<Window, DockZoneViewModel> m_tearoffRootZones = new();
        private readonly WindowManager m_windowManager = new();
        private readonly List<Window> m_windowsToCloseSilently = new();
        private readonly List<Window> m_currentlyClosingWindows = new();
        private bool m_isZoneDragDropUnderway;
        private bool m_isReadyToTrackAutoSave;

        // ===========[ Construction / Disposal ]=========================================
        public DockingManager(
            Window rootWindow,
            string uniqueId,
            string persistentStorageFilePath = null,
            eDockingAutoSaveMethod autoSaveMethod = eDockingAutoSaveMethod.None)
        {
            this.RootWindow = rootWindow;
            this.UniqueId = uniqueId;
            this.DockLayoutPersistentStorageFile = persistentStorageFilePath
                ?? DockingSerialization.CreateApplicationPath(uniqueId);
            this.AutoSaveMethod = autoSaveMethod;

            this.UISyncVM = new DockPanelAddRemoveUISync();
            this.UISyncVM.SetManager(this);
            this.CreateNewTearoffWindowHandler = () => new Window();
            this.ShowTearoffWindowHandler = w => w.Activate();

            // WindowManager tracks tearoff window lifecycle and activation order.
            // CollectionChanged fires when a window is removed (closed) so we can
            // clean up stale docking-map entries even if our own close handler
            // didn't run (belt-and-suspenders against null AppWindow crashes).
            m_windowManager.Setup(rootWindow);
            ((INotifyCollectionChanged)m_windowManager).CollectionChanged += this.OnWindowManagerCollectionChanged;

            // When the root window closes, force-close all tearoff windows immediately
            // (bypass CanClose veto so orphaned tearoffs don't outlive the app).
            this.RootWindow.Closed += this.OnRootWindowClosed;

            // When the root window is re-activated (e.g. user clicks our taskbar entry),
            // bring all tearoff windows back to front in their relative activation order.
            this.RootWindow.Activated += this.OnRootWindowActivated;
        }

        public void Dispose()
        {
            this.CloseAll(force: true);

            this.CreateNewTearoffWindowHandler = null;
            this.ShowTearoffWindowHandler = null;

            foreach (DockZone zone in m_rootDockZones)
            {
                zone.Loaded += this.MainWindowDockZone_Loaded;
                zone.Unloaded += this.MainWindowDockZone_Unloaded;
            }

            m_rootDockZones.Clear();
            m_tearoffRootZones.Clear();
            m_windowsToCloseSilently.Clear();
            m_currentlyClosingWindows.Clear();
            m_factory.Clear();
        }

        // ===========[ Properties ]===========================================

        public Window RootWindow { get; }

        public string UniqueId { get; }

        public string DockLayoutPersistentStorageFile { get; set; }

        public eDockingAutoSaveMethod AutoSaveMethod { get; set; }

        /// <summary>Factory for creating new tearoff windows. Default creates a plain Window.</summary>
        public Func<Window> CreateNewTearoffWindowHandler { private get; set; }

        /// <summary>Action called to show a tearoff window. Default calls Window.Activate().</summary>
        public Action<Window> ShowTearoffWindowHandler { get; set; }

        public bool IsLoadingFromLayout { get; internal set; }

        public bool IsZoneDragDropUnderway
        {
            get => m_isZoneDragDropUnderway;
            private set => this.SetAndRaiseIfChanged(ref m_isZoneDragDropUnderway, value);
        }

        /// <summary>Minimum pixel dimension for dock zone panels during size recalculation. Default: 50.</summary>
        public double MinPanelDimension { get; set; } = 50.0;

        /// <summary>When true, each panel header shows an X close button. Default: true.</summary>
        public bool ShowPanelClose { get; set; } = true;

        /// <summary>When true, each tab strip header shows an X close button. Default: true.</summary>
        public bool ShowTabHeaderClose { get; set; } = true;

        /// <summary>When true, middle-clicking a tab header closes that panel. Default: true.</summary>
        public bool AllowMiddleMouseClose { get; set; } = true;

        /// <summary>
        /// Pixels the pointer must travel after press before a drag is initiated
        /// (header drag, title-bar drag, or tab drag to tearoff). Default: 8.
        /// </summary>
        public double DragThresholdPixels { get; set; } = 8.0;

        /// <summary>
        /// Title text displayed in the title bar of every tearoff window.
        /// Applied to DockTearoffContainerPanel.Title when the tearoff window is created.
        /// Empty string by default (title bar shows only the icon and decorative lines).
        /// </summary>
        public string FixedTearoffWindowTitle { get; set; } = string.Empty;

        public DockPanelAddRemoveUISync UISyncVM { get; }

        // ===========[ IDockingManager ]======================================

        IDockableDisplayElement IDockingManager.BuildNewDisplayElement(Type elementType)
            => this.BuildNewDisplayElement(elementType);

        public IEnumerable<DockZoneViewModel> GetAllRoots()
        {
            return m_rootDockZones.Select(z => z.ViewModel);
        }

        // ===========[ Factory Registration ]=================================

        public void RegisterDisplayFactory<T>(Func<T> customFactory = null, bool singleInstanceOnly = false)
            where T : IDockableDisplayElement
        {
            this.RegisterDisplayFactory<T>(
                new DockPanelRegistrationRules { SingleInstanceOnly = singleInstanceOnly },
                customFactory
            );
        }

        public void RegisterDisplayFactory<T>(DockPanelRegistrationRules rules, Func<T> customFactory = null)
            where T : IDockableDisplayElement
        {
            m_factory[typeof(T)] = new DisplayBuilder
            {
                Rules = rules,
                Builder = customFactory != null
                    ? () => customFactory() as IDockableDisplayElement
                    : () => BuildDefaultDisplayFor(typeof(T))
            };

            this.UISyncVM.AddEntry(typeof(T), rules);
        }

        // ===========[ Menu Management ]======================================

        /// <summary>
        /// Populate a <see cref="MenuBarItem"/> with toggle/add entries for each registered
        /// panel type. The menu stays synchronized with panel state - checked items indicate
        /// visible panels; clicking a toggle item shows/hides the panel.
        /// </summary>
        public void ManageMenu (MenuBarItem menuBarItem)
        {
            this.RebuildMenuItems(menuBarItem);
            this.UISyncVM.PanelStateChanged += (s, e) => this.UpdateMenuCheckedStates(menuBarItem);
            ((System.Collections.Specialized.INotifyCollectionChanged)this.UISyncVM.PanelTypeEntries)
                .CollectionChanged += (s, e) => this.RebuildMenuItems(menuBarItem);
        }

        private void RebuildMenuItems (MenuBarItem menuBarItem)
        {
            menuBarItem.Items.Clear();
            foreach (var desc in this.UISyncVM.GenerateMenuDescriptors())
            {
                if (desc.IsToggle)
                {
                    var toggle = new ToggleMenuFlyoutItem
                    {
                        Text = desc.DisplayName,
                        Tag = desc.PanelType,
                        IsChecked = desc.IsChecked,
                    };

                    toggle.Click += this.OnManagedToggleMenuItemClicked;
                    menuBarItem.Items.Add(toggle);
                }
                else
                {
                    var item = new MenuFlyoutItem
                    {
                        Text = $"Add {desc.DisplayName}",
                        Tag = desc.PanelType,
                    };

                    item.Click += this.OnManagedAddMenuItemClicked;
                    menuBarItem.Items.Add(item);
                }
            }
        }

        private void UpdateMenuCheckedStates (MenuBarItem menuBarItem)
        {
            foreach (var item in menuBarItem.Items)
            {
                if (item is ToggleMenuFlyoutItem toggle && toggle.Tag is Type panelType)
                {
                    DockPanelTypeEntry entry = this.UISyncVM.FindEntry(panelType);
                    if (entry != null)
                    {
                        toggle.IsChecked = entry.HasActiveInstance;
                    }
                }
            }
        }

        private void OnManagedToggleMenuItemClicked (object sender, RoutedEventArgs e)
        {
            if (sender is ToggleMenuFlyoutItem toggle && toggle.Tag is Type panelType)
            {
                this.UISyncVM.RequestSetToggleState(panelType);
            }
        }

        private void OnManagedAddMenuItemClicked (object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem item && item.Tag is Type panelType)
            {
                this.UISyncVM.RequestAdd(panelType);
            }
        }

        // ===========[ Zone Registration ]====================================

        public void RegisterMainWindowRootDockZones(params DockZone[] dockZones)
        {
            this.RegisterRootDockZones(this.RootWindow, dockZones);

            foreach (DockZone zone in dockZones)
            {
                zone.Loaded += this.MainWindowDockZone_Loaded;
                zone.Unloaded += this.MainWindowDockZone_Unloaded;
            }
        }

        private void MainWindowDockZone_Loaded(object sender, RoutedEventArgs e)
        {
            this.ShowAllTearoffWindows();
        }

        private void MainWindowDockZone_Unloaded(object sender, RoutedEventArgs e)
        {
            this.HideAllTearoffWindows();
        }

        public void RegisterRootDockZones(params DockZone[] dockZones)
            => this.RegisterRootDockZones(this.RootWindow, dockZones);

        public void RegisterRootDockZones(Window window, params DockZone[] dockZones)
        {
            foreach (var zone in dockZones)
            {
                // Auto-assign GroupId from the control's Name if not already set
                if (DockZone.GetGroupId(zone).IsNullOrEmpty() && zone.Name.IsNotNullOrEmpty())
                {
                    DockZone.SetGroupId(zone, zone.Name);
                }

                if (zone.IsSetup && zone.Manager != this && zone.Manager is DockingManager manager)
                {
                    manager.DeRegisterRootDockZones(zone);
                }

                m_rootDockZones.Add(zone);
                zone.Manager = this;

                if (zone.ViewModel == null)
                {
                    zone.ViewModel = new DockZoneViewModel(this);
                }

                if (!m_dockZoneMapping.TryGetValue(window, out var list))
                {
                    list = new List<DockZone>();
                    m_dockZoneMapping[window] = list;
                }

                list.Add(zone);

                // Rebuild layout now that the zone is fully registered in m_dockZoneMapping
                // so GetWindowForZone returns the correct window (fixes isInTearoff checks,
                // close button visibility, header-drag suppression for tearoff root zones).
                zone.TriggerLayoutRebuild();
            }
        }

        public void DeRegisterRootDockZones(params DockZone[] dockZones)
        {
            foreach (var zone in dockZones)
            {
                m_rootDockZones.Remove(zone);
                zone.Manager = null;
                foreach (var list in m_dockZoneMapping.Values)
                {
                    list.Remove(zone);
                }
            }
        }

        // ===========[ Display Building ]=====================================

        public T BuildNewDisplayElement<T>() where T : IDockableDisplayElement
            => (T)this.BuildNewDisplayElement(typeof(T));

        public IDockableDisplayElement BuildNewDisplayElement(Type elementType)
        {
            var displayElement = m_factory.TryGetValue(elementType, out var b)
                ? b.Builder()
                : BuildDefaultDisplayFor(elementType);
            return this.SetupAndReturnNew(displayElement);
        }

        // ===========[ Layout Control ]=======================================

        public void ShowAllTearoffWindows()
        {
            // The window manager preserves activation order, so iterating through windows in this way
            //  means we'll show our windows in the order they had activated them!
            m_windowManager.ShowAllWindows(includingRoot: true);
        }

        public void HideAllTearoffWindows()
        {
            List<Window> tearoffs = m_dockZoneMapping.Keys.Where(w => w != this.RootWindow).ToList();
            foreach (Window window in tearoffs)
            {
                window.Hide();
            }
        }

        public bool CloseAll(bool force = false)
        {
            if (!force)
            {
                var all = m_rootDockZones
                    .SelectMany(z => TreeTraversal<DockZone>.All(z))
                    .SelectMany(z => z.ViewModel == null
                        ? Enumerable.Empty<DockingContentAdapterModel>()
                        : (IEnumerable<DockingContentAdapterModel>)z.ViewModel.DockedContent)
                    .ToList();

                foreach (var adapter in all)
                {
                    if (!adapter.CheckCanClose())
                    {
                        return false;
                    }
                }
            }

            var tearoffs = m_dockZoneMapping.Keys.Where(w => w != this.RootWindow).ToList();
            m_windowsToCloseSilently.AddRange(tearoffs);
            foreach (Window window in tearoffs)
            {
                m_tearoffRootZones.Remove(window);
                m_dockZoneMapping.Remove(window);
                m_windowManager.StopTracking(window);
                window.Close();
            }

            m_windowsToCloseSilently.Clear();
            return true;
        }

        public void CleanZoneLayoutHierarchies()
        {
            var toVisit = new Queue<DockZoneViewModel>();
            foreach (var zone in m_rootDockZones.Select(z => z.ViewModel))
            {
                if (zone != null)
                {
                    toVisit.Enqueue(zone);
                }
            }

            while (toVisit.Count != 0)
            {
                var parentEval = toVisit.Dequeue();
                if (parentEval?.Children == null || parentEval.Children.Count == 0)
                {
                    continue;
                }

                for (int index = 0; index < parentEval.Children.Count; ++index)
                {
                    var child = parentEval.Children[index];
                    if (child.Orientation == parentEval.Orientation)
                    {
                        parentEval.RunChildZoneRemoval(child);
                        foreach (var grandchild in child.Children.Where(c => c != null).ToList())
                        {
                            parentEval.InsertChild(index++, grandchild);
                            toVisit.Enqueue(grandchild);
                        }
                    }
                    else
                    {
                        toVisit.Enqueue(child);
                    }
                }
            }
        }

        public DockZoneViewModel FindFirstAvailableDockZone()
        {
            foreach (DockZoneViewModel zone in m_rootDockZones
                .SelectMany(z => TreeTraversal<DockZone>.All(z))
                .Select(z => z.ViewModel))
            {
                if (zone != null && zone.Orientation.IsFlagInGroup(eDockOrientation.AnyLeafDisplay))
                {
                    return zone;
                }
            }

            Logger.LogError("DockingManager: tried to find first available dock zone but none exists - manager may be set up incorrectly");
            return null;
        }

        /// <summary>
        /// Find the first available leaf dock zone matching the given group id.
        /// Falls back to <see cref="FindFirstAvailableDockZone"/> if no match is found.
        /// </summary>
        public DockZoneViewModel FindFirstAvailableDockZoneForGroup(string groupId)
        {
            if (groupId.IsNotNullOrEmpty())
            {
                foreach (DockZone rootZone in m_rootDockZones)
                {
                    if (DockZone.GetGroupId(rootZone) == groupId)
                    {
                        foreach (DockZoneViewModel zone in TreeTraversal<DockZoneViewModel>.All(rootZone.ViewModel))
                        {
                            if (zone.Orientation.IsFlagInGroup(eDockOrientation.AnyLeafDisplay))
                            {
                                return zone;
                            }
                        }
                    }
                }
            }

            return this.FindFirstAvailableDockZone();
        }

        // ===========[ Panel Add/Toggle/Hide — delegates to shared UISync logic ]====

        public void AddPanel(Type panelType) => this.UISyncVM.AddPanel(panelType);
        public void TogglePanel(Type panelType) => this.UISyncVM.ShowPanel(panelType);
        public void RemoveOrHidePanel(DockingContentAdapterModel adapter) => this.UISyncVM.CloseOrHidePanel(adapter);

        // ===========[ IDockingManager Platform Hooks ]========================

        public DockPanelRegistrationRules? GetPanelRules(Type panelType)
        {
            return m_factory.TryGetValue(panelType, out DisplayBuilder builder) ? builder.Rules : null;
        }

        public DockZoneViewModel FindTargetZoneForGroup(string groupId)
        {
            return this.FindFirstAvailableDockZoneForGroup(groupId);
        }

        public HiddenPanelPlatformState CaptureHideState(DockingContentAdapterModel adapter)
        {
            DockZone dockZoneUI = adapter.Location?.UI as DockZone;
            Window hostWindow = dockZoneUI != null ? this.GetWindowForZone(dockZoneUI) : null;
            bool wasInTearoff = hostWindow != null && hostWindow != this.RootWindow;

            if (!wasInTearoff)
            {
                return null;
            }

            var appWindow = hostWindow.AppWindow;
            return new HiddenPanelPlatformState
            {
                WasInTearoff = true,
                NextDisplayLocationX = appWindow.Position.X,
                NextDisplayLocationY = appWindow.Position.Y,
                NextDisplayWidth = appWindow.Size.Width,
                NextDisplayHeight = appWindow.Size.Height,
                TearoffWindowRef = hostWindow,
            };
        }

        public bool TryRestoreFromHideState(object hideState, DockingContentAdapterModel adapter)
        {
            if (hideState is not HiddenPanelPlatformState state || !state.WasInTearoff)
            {
                return false;
            }

            // Check if the same tearoff window is still alive
            if (state.TearoffWindowRef is Window window && m_dockZoneMapping.ContainsKey(window))
            {
                if (m_tearoffRootZones.TryGetValue(window, out DockZoneViewModel tearoffRoot))
                {
                    tearoffRoot.AddDockedContent(adapter);
                    return true;
                }
            }

            return false;
        }

        public void AfterPanelHidden(object hideState)
        {
            if (hideState is not HiddenPanelPlatformState state || !state.WasInTearoff)
            {
                return;
            }

            if (state.TearoffWindowRef is Window hostWindow)
            {
                DockZone rootZoneUI = this.GetRootDockZoneForWindow(hostWindow);
                if (rootZoneUI != null)
                {
                    this.CloseSourceTearoffIfEmpty(hostWindow, rootZoneUI);
                }
            }
        }

        public bool CreateTearoffForPanel(DockingContentAdapterModel adapter, double x, double y, double width, double height)
        {
            if (x < 0)
            {
                x = m_windowManager.Root.AppWindow.Position.X;
            }
            if (y < 0)
            {
                y = m_windowManager.Root.AppWindow.Position.Y;
            }

            var newZone = new DockZoneViewModel(this);
            var windowResult = this.CreateAndStockTearoffWindow(
                newZone, new Point(x, y), new Size(width, height)
            );

            if (windowResult)
            {
                newZone.AddDockedContent(adapter);
                return true;
            }

            return false;
        }

        public bool IsTearoffRootThatWouldOrphan(DockZoneViewModel zone)
        {
            if (zone == null)
            {
                return false;
            }

            foreach (var (window, rootZone) in m_tearoffRootZones)
            {
                if (rootZone == zone)
                {
                    // It's a tearoff root — check if removing the last content would leave it empty
                    bool hasOnlyOneContent = zone.DockedContent.Count <= 1
                        && zone.Children.Count == 0;
                    return hasOnlyOneContent;
                }
            }

            return false;
        }

        public void CloseTearoffForRootZone(DockZoneViewModel rootZone)
        {
            foreach (var (window, rz) in m_tearoffRootZones)
            {
                if (rz == rootZone)
                {
                    this.SilentlyForceCloseTearoffWindow(window);
                    return;
                }
            }
        }

        // ===========[ Tearoff Window Ops ]===================================

        public Result<Window> DoTearoff(IDockableDisplayElement element, Point newWindowOrigin)
        {
            DockZoneSize dockSize = element.DockingAdapter.Location?.UI?.RenderSize ?? DockZoneSize.Empty;
            return this.DoTearoff(element, newWindowOrigin, new Size(dockSize.Width, dockSize.Height));
        }

        public Result<Window> DoTearoff(IDockableDisplayElement element, Point newWindowOrigin, Size newWindowSize)
        {
            try
            {
                // Check if tearing off will orphan the source tearoff window
                DockZoneViewModel sourceLocation = element.DockingAdapter.Location;

                if (!sourceLocation.RemoveDockedContent(element.DockingAdapter))
                {
                    var result = Result<Window>.Error("DockingManager: Tear off failed - could not remove content from zone");
                    Logger.LogError(result.GetErrorReport());
                    return result;
                }

                var newZone = new DockZoneViewModel(this);
                var windowResult = this.CreateAndStockTearoffWindow(newZone, newWindowOrigin, newWindowSize);
                if (windowResult)
                {
                    newZone.AddDockedContent(element);
                }

                return windowResult;
            }
            catch (Exception exc)
            {
                Logger.LogError("Window tearoff failed!", exc);
                return Result<Window>.Error("DockingManager: Window tearoff failed for unknown reason");
            }
        }

        public Result<Window> DoGroupTearoff(DockZoneViewModel sourceZone, Point newWindowOrigin)
        {
            var size = sourceZone.UI?.RenderSize ?? DockZoneSize.Empty;
            return this.DoGroupTearoff(sourceZone, newWindowOrigin, new Size(size.Width, size.Height));
        }

        public Result<Window> DoGroupTearoff(DockZoneViewModel sourceZone, Point newWindowOrigin, Size newTearoffWindowSize)
        {
            try
            {
                if (sourceZone.Parent != null)
                {
                    sourceZone.UnparentAndDistributeSibling();
                }
                else
                {
                    // Already root of a tearoff window - return its existing window
                    var existingWindow = m_tearoffRootZones
                        .Where(kv => kv.Value == sourceZone)
                        .Select(kv => kv.Key)
                        .FirstOrDefault();

                    if (existingWindow != null)
                    {
                        return Result<Window>.Success(existingWindow);
                    }

                    // Root zone on the main window - duplicate so the main zone keeps its slot
                    sourceZone = sourceZone.DuplicateAndClear();
                }

                return this.CreateAndStockTearoffWindow(sourceZone, newWindowOrigin, newTearoffWindowSize);
            }
            catch (Exception exc)
            {
                Logger.LogError("Window group tearoff failed!", exc);
                return Result<Window>.Error("DockingManager: Window tearoff failed for unknown reason");
            }
        }

        // Initiates a zone drag: tears the zone off into its own window first, then
        // immediately starts RunDragSearch with that tearoff window as the drag source.
        // This means the root window's zones become valid drop targets (they are no
        // longer the "source" window and are not skipped in hit-testing).
        //
        // pressScreenPos:    screen cursor position when the user first pressed (for correct
        //                    window origin placement). If null, current cursor pos is used.
        // cursorWindowOffset: where the cursor should appear within the tearoff window
        //                    (i.e. the window is placed so this offset lands under the cursor).
        //                    If null, defaults to (windowWidth/2, 15).
        internal async Task InitiateDrag(DockZone zone, Point? pressScreenPos = null, Point? cursorWindowOffset = null)
        {
            if (!User32WindowFuncs.GetCursorPos(out var pt))
            {
                return;
            }

            var renderSize = ((IDockZoneUI)zone).RenderSize;
            int windowWidth = (int)Math.Max(renderSize.Width, 200);
            int windowHeight = (int)Math.Max(renderSize.Height, 50);

            // Resolve anchor and cursor-in-window offset
            double anchorX = pressScreenPos.HasValue ? pressScreenPos.Value.X : pt.X;
            double anchorY = pressScreenPos.HasValue ? pressScreenPos.Value.Y : pt.Y;

            double offsetX = cursorWindowOffset.HasValue
                ? Math.Clamp(cursorWindowOffset.Value.X, 0, windowWidth)
                : windowWidth / 2.0;
            double offsetY = cursorWindowOffset.HasValue
                ? Math.Clamp(cursorWindowOffset.Value.Y, 0, windowHeight)
                : 15.0;

            var origin = new Point(anchorX - offsetX, anchorY - offsetY);

            var tearoffResult = this.DoGroupTearoff(
                zone.ViewModel,
                origin,
                new Size(windowWidth, windowHeight)
            );

            if (!tearoffResult)
            {
                return;
            }

            var tearoffWindow = tearoffResult.Value;

            if (!m_dockZoneMapping.TryGetValue(tearoffWindow, out var tearoffZones)
                || tearoffZones.Count == 0)
            {
                return;
            }

            await this.RunDragSearch(tearoffWindow, tearoffZones[0], new Point(offsetX, offsetY));
        }

        // Initiates a drag for a single content element: tears only that element off into
        // its own window, then starts RunDragSearch. Called from tab-button drag threshold.
        // pressScreenPos / cursorWindowOffset: same semantics as InitiateDrag.
        internal async Task InitiateDragForContent(IDockableDisplayElement element, Point? pressScreenPos = null, Point? cursorWindowOffset = null)
        {
            if (element?.DockingAdapter == null)
            {
                return;
            }

            if (!User32WindowFuncs.GetCursorPos(out var pt))
            {
                return;
            }

            var locationSize = element.DockingAdapter.Location?.UI?.RenderSize ?? new DockZoneSize(400, 300);
            int windowWidth = (int)Math.Max(locationSize.Width, 200);
            int windowHeight = (int)Math.Max(locationSize.Height, 50);

            double anchorX = pressScreenPos.HasValue ? pressScreenPos.Value.X : pt.X;
            double anchorY = pressScreenPos.HasValue ? pressScreenPos.Value.Y : pt.Y;

            double offsetX = cursorWindowOffset.HasValue
                ? Math.Clamp(cursorWindowOffset.Value.X, 0, windowWidth)
                : windowWidth / 2.0;
            double offsetY = cursorWindowOffset.HasValue
                ? Math.Clamp(cursorWindowOffset.Value.Y, 0, windowHeight)
                : 15.0;

            var origin = new Point(anchorX - offsetX, anchorY - offsetY);

            var tearoffResult = this.DoTearoff(
                element,
                origin,
                new Size(windowWidth, windowHeight)
            );

            if (!tearoffResult)
            {
                return;
            }

            var tearoffWindow = tearoffResult.Value;

            if (!m_dockZoneMapping.TryGetValue(tearoffWindow, out var tearoffZones)
                || tearoffZones.Count == 0)
            {
                return;
            }

            await this.RunDragSearch(tearoffWindow, tearoffZones[0], new Point(offsetX, offsetY));
        }

        // Drag search: moves the tearoff window with the cursor while polling for drop targets.
        // Hit-tests all registered windows EXCEPT the drag source for DockDropInsertionDriverWidget
        // elements (directional arrows shown when DockZone.IsDirectDropTarget=true).
        //
        // On mouse-up over a widget  → DropAddSiblingIntoDock; close tearoff if now empty.
        // On mouse-up elsewhere      → tearoff stays at current position as a detached window.
        //
        // Called from InitiateDrag (UI thread); Task.Delay resumes on the UI thread because
        // WinUI3 registers a DispatcherQueueSynchronizationContext.
        //
        // cursorWindowOffset: where the cursor appears within the dragged window.
        // Defaults to (windowWidth/2, 15) when not supplied.
        public async Task RunDragSearch(Window dragSourceWindow, DockZone sourceDockZone, Point? cursorWindowOffset = null)
        {
            DockZone lastDropTarget = null;
            DockDropInsertionDriverWidget lastInsertionWidget = null;

            // Layered-window style on the drag source manages its opacity dynamically.
            // Starts at full opacity; dims to kTearoffDragOpacity only when the cursor is
            // over a valid drop-target window so the user can see through the ghost.
            nint hwnd = WinRT.Interop.WindowNative.GetWindowHandle(dragSourceWindow);
            nint originalExStyle = User32WindowFuncs.GetWindowLongPtr(hwnd, User32WindowFuncs.kGwlExStyle);
            User32WindowFuncs.SetWindowLongPtr(hwnd, User32WindowFuncs.kGwlExStyle, originalExStyle | User32WindowFuncs.kWsExLayered);
            User32WindowFuncs.SetLayeredWindowAttributes(hwnd, 0, 255, User32WindowFuncs.kLwaAlpha); // start fully opaque

            var sourceAppWindow = dragSourceWindow.AppWindow;

            // Drag-source dynamic-opacity state: true = kTearoffDragOpacity, false = full
            bool isSemiTransparent = false;

            // Hover-to-bring-forward: cursor dwelling on a non-source window for ≥0.75 s starts
            // a flash; at ≥1.5 s the window is raised to the top of the Z-order.
            Window hoverTargetWindow = null;
            int    hoverFrameCount   = 0;
            bool   isHoverArming     = false;
            nint   hoverHwnd         = 0;
            nint   hoverOrigExStyle  = 0;

            // Snapshot WindowManager activation order once. Index 0 = most recently activated
            // (frontmost); higher index = further back. Only windows behind the drag source
            // (higher index) are eligible for hover-bring-forward - windows already in front
            // need no assistance. If the drag source isn't tracked, all windows are eligible.
            var windowManagerOrder = m_windowManager.ToList();
            int dragSourceOrderIdx = windowManagerOrder.IndexOf(dragSourceWindow);

            // Pre-compute the windowManagerOrder index of the globally frontmost non-drag dock
            // window (the minimum index > dragSourceOrderIdx). Hovering over this window should
            // never trigger bring-forward - it is already the topmost accessible window.
            int frontmostNonDragIdx = m_dockZoneMapping.Keys
                .Where(w => w != dragSourceWindow)
                .Select(w => windowManagerOrder.IndexOf(w))
                .Where(i => i > dragSourceOrderIdx)
                .DefaultIfEmpty(int.MaxValue)
                .Min();

            // Resolve the cursor-in-window offset once; fall back to centre-top default.
            double resolvedOffsetX = cursorWindowOffset.HasValue ? cursorWindowOffset.Value.X : -1;
            double resolvedOffsetY = cursorWindowOffset.HasValue ? cursorWindowOffset.Value.Y : 15;

            this.IsZoneDragDropUnderway = true;
            try
            {
                // --- Poll loop ---
                while (true)
                {
                    await Task.Delay(16);

                    // Exit when left mouse button is released
                    if ((User32WindowFuncs.GetAsyncKeyState(User32WindowFuncs.kVkLButton) & 0x8000) == 0)
                    {
                        break;
                    }

                    if (!User32WindowFuncs.GetCursorPos(out var pt))
                    {
                        break;
                    }

                    // Move tearoff so cursor stays at the captured offset within the window.
                    var sourceSize = sourceAppWindow.Size;
                    double offsetX = resolvedOffsetX >= 0 ? resolvedOffsetX : sourceSize.Width / 2.0;
                    sourceAppWindow.Move(new Windows.Graphics.PointInt32(
                        (int)(pt.X - offsetX),
                        (int)(pt.Y - resolvedOffsetY)
                    ));

                    // Hit-test all windows except the tearoff source
                    DockZone currentDropTarget = null;
                    DockDropInsertionDriverWidget currentInsertionWidget = null;

                    // Hover-candidate tracking: find the FRONTMOST (minimum-index) non-drag
                    // window whose client rect contains the cursor. Cannot take first-found
                    // because m_dockZoneMapping iterates in arbitrary dict order - A might be
                    // visited before B even when B is in front of A at the cursor position.
                    Window hoverCandidateWindow = null;
                    int    hoverCandidateIdx    = int.MaxValue;

                    // Iterate non-drag windows frontmost-first (ascending windowManagerOrder index)
                    // so both the drop hit-test and hover-candidate tracking always pick the
                    // topmost visible window at the cursor - not whichever window happens to
                    // be first in the dictionary. The drop hit-test breaks on the first hit, so
                    // iterating frontmost-first ensures a foreground window's zones are preferred
                    // over background zones that overlap the same screen coordinates.
                    foreach (var window in m_dockZoneMapping.Keys
                        .Where(w => w != dragSourceWindow)
                        .OrderBy(w => { int i = windowManagerOrder.IndexOf(w); return i >= 0 ? i : int.MaxValue; }))
                    {
                        var zones = m_dockZoneMapping[window];

                        // Convert screen cursor position to window client coordinates via Win32
                        // (handles title-bar chrome offset automatically), then divide by the
                        // XAML DPI scale to get device-independent pixel coordinates that
                        // FindElementsInHostCoordinates expects.
                        nint winHwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
                        var screenPt = new User32WindowFuncs.POINT { X = pt.X, Y = pt.Y };
                        if (!User32WindowFuncs.ScreenToClient(winHwnd, ref screenPt) || screenPt.X < 0 || screenPt.Y < 0)
                        {
                            continue;
                        }

                        // Track the frontmost (minimum-index) non-drag window whose client rect
                        // contains the cursor. Evaluated for every non-skipped window so dict
                        // iteration order does not bias the result toward a background window.
                        int windowOrderIdx = windowManagerOrder.IndexOf(window);
                        var winSize        = window.AppWindow.Size;
                        if (windowOrderIdx > dragSourceOrderIdx
                            && screenPt.X < winSize.Width && screenPt.Y < winSize.Height
                            && windowOrderIdx < hoverCandidateIdx)
                        {
                            hoverCandidateWindow = window;
                            hoverCandidateIdx    = windowOrderIdx;
                        }

                        double dpiScale = (window.Content as Microsoft.UI.Xaml.FrameworkElement)
                            ?.XamlRoot?.RasterizationScale ?? 1.0;

                        foreach (var rootZone in zones)
                        {
                            var localPt = new Point(screenPt.X / dpiScale, screenPt.Y / dpiScale);
                            var hits = Microsoft.UI.Xaml.Media.VisualTreeHelper
                                .FindElementsInHostCoordinates(localPt, rootZone);

                            // Check insertion widget first - takes priority over zone hit.
                            // Skip disabled widgets (e.g. the center "+" on split zones).
                            var hitWidget = hits.OfType<DockDropInsertionDriverWidget>().FirstOrDefault(w => w.IsEnabled);
                            if (hitWidget != null)
                            {
                                currentInsertionWidget = hitWidget;
                                currentDropTarget = hitWidget.InsertionZone;
                                break;
                            }

                            // Fall back to DockZone hit (shows overlay but no committed direction)
                            var hitZone = hits.OfType<DockZone>().FirstOrDefault();
                            if (hitZone != null)
                            {
                                currentDropTarget = hitZone;
                                break;
                            }
                        }

                        if (currentDropTarget != null)
                        {
                            break;
                        }
                    }

                    // Derive currentHoverWindow from the candidates collected above.
                    // Only elevate if the candidate is NOT the globally frontmost non-drag dock
                    // window - that window is already accessible and needs no bring-forward.
                    // Hovering over it (or over a background window it occludes) should be silent.
                    Window currentHoverWindow = (hoverCandidateIdx > frontmostNonDragIdx)
                        ? hoverCandidateWindow
                        : null;

                    // Update insertion widget engagement state
                    if (currentInsertionWidget != lastInsertionWidget)
                    {
                        if (lastInsertionWidget != null)
                        {
                            lastInsertionWidget.IsEngaged = false;
                        }

                        lastInsertionWidget = currentInsertionWidget;

                        if (lastInsertionWidget != null)
                        {
                            lastInsertionWidget.IsEngaged = true;
                        }
                    }

                    // Update drop-target zone highlight
                    if (currentDropTarget != lastDropTarget)
                    {
                        if (lastDropTarget != null)
                        {
                            lastDropTarget.IsDirectDropTarget = false;
                        }

                        lastDropTarget = currentDropTarget;

                        if (lastDropTarget != null)
                        {
                            lastDropTarget.IsDirectDropTarget = true;
                        }
                    }

                    // Dim the drag source when it is over a drop-target window so the user can
                    // see through the ghost to the target zone beneath; restore full opacity when
                    // hovering over empty space where there is no drop candidate.
                    bool shouldBeSemiTransparent = currentDropTarget != null;
                    if (shouldBeSemiTransparent != isSemiTransparent)
                    {
                        byte alpha = shouldBeSemiTransparent ? (byte)(255 * kTearoffDragOpacity) : (byte)255;
                        User32WindowFuncs.SetLayeredWindowAttributes(hwnd, 0, alpha, User32WindowFuncs.kLwaAlpha);
                        isSemiTransparent = shouldBeSemiTransparent;
                    }

                    // Hover-to-bring-forward: track how long the cursor dwells on each non-source
                    // window. After 0.75 s it starts flashing (opacity pulse) to warn; after 1.5 s
                    // the window is raised to the top of the Z-order and the counter resets.
                    if (currentHoverWindow != hoverTargetWindow)
                    {
                        // Cursor moved to a different window - cancel any active arming
                        if (isHoverArming)
                        {
                            User32WindowFuncs.SetLayeredWindowAttributes(hoverHwnd, 0, 255, User32WindowFuncs.kLwaAlpha);
                            User32WindowFuncs.SetWindowLongPtr(hoverHwnd, User32WindowFuncs.kGwlExStyle, hoverOrigExStyle);
                            isHoverArming    = false;
                            hoverHwnd        = 0;
                        }
                        hoverTargetWindow = currentHoverWindow;
                        hoverFrameCount   = 0;
                    }
                    else if (hoverTargetWindow != null)
                    {
                        hoverFrameCount++;

                        const int kArmingStartFrames  = 47; // ≈0.75 s at 16 ms/frame
                        const int kBringForwardFrames = 94; // ≈1.5 s at 16 ms/frame
                        const int kFlashPeriodFrames  = 15; // ≈0.25 s per flash phase (≈4 pulses)

                        // 0.75 s mark: attach WS_EX_LAYERED to the hovered window and begin flashing
                        if (hoverFrameCount == kArmingStartFrames)
                        {
                            isHoverArming    = true;
                            hoverHwnd        = WinRT.Interop.WindowNative.GetWindowHandle(hoverTargetWindow);
                            hoverOrigExStyle = User32WindowFuncs.GetWindowLongPtr(hoverHwnd, User32WindowFuncs.kGwlExStyle);
                            User32WindowFuncs.SetWindowLongPtr(hoverHwnd, User32WindowFuncs.kGwlExStyle, hoverOrigExStyle | User32WindowFuncs.kWsExLayered);
                        }

                        // Pulse opacity while arming: dim/bright toggle every kFlashPeriodFrames
                        if (isHoverArming && hoverFrameCount < kBringForwardFrames)
                        {
                            int  armFrame   = hoverFrameCount - kArmingStartFrames;
                            byte flashAlpha = ((armFrame / kFlashPeriodFrames) % 2 == 0)
                                ? (byte)(255 * 0.70) // dim phase
                                : (byte)255;          // bright phase
                            User32WindowFuncs.SetLayeredWindowAttributes(hoverHwnd, 0, flashAlpha, User32WindowFuncs.kLwaAlpha);
                        }

                        // 1.5 s mark: restore opacity and raise the window
                        if (hoverFrameCount >= kBringForwardFrames)
                        {
                            if (isHoverArming)
                            {
                                User32WindowFuncs.SetLayeredWindowAttributes(hoverHwnd, 0, 255, User32WindowFuncs.kLwaAlpha);
                                User32WindowFuncs.SetWindowLongPtr(hoverHwnd, User32WindowFuncs.kGwlExStyle, hoverOrigExStyle);
                                isHoverArming = false;
                                hoverHwnd     = 0;
                            }
                            hoverTargetWindow.AppWindow.MoveInZOrderAtTop();
                            dragSourceWindow.AppWindow.MoveInZOrderAtTop(); // keep drag source above the raised window
                            hoverFrameCount = 0; // reset so a continued hover doesn't re-trigger immediately

                            // MoveInZOrderAtTop does not fire WindowManager activation events, so
                            // the snapshot stays stale and would re-qualify the raised window as a
                            // bring-forward candidate the moment the cursor returns to it.
                            // Manually move it to right behind the drag source in our local list,
                            // making it the new frontmostNonDragIdx so it is treated as already-on-top.
                            windowManagerOrder.Remove(hoverTargetWindow);
                            windowManagerOrder.Insert(dragSourceOrderIdx + 1, hoverTargetWindow);
                            frontmostNonDragIdx = m_dockZoneMapping.Keys
                                .Where(w => w != dragSourceWindow)
                                .Select(w => windowManagerOrder.IndexOf(w))
                                .Where(i => i > dragSourceOrderIdx)
                                .DefaultIfEmpty(int.MaxValue)
                                .Min();
                        }
                    }
                }

                // Execute drop if a widget was engaged when button was released
                if (lastInsertionWidget?.InsertionZone?.ViewModel != null)
                {
                    // Capture the target window NOW, before the drop restructures the zone
                    // tree and before CloseTearoffWindow removes the drag source from
                    // m_dockZoneMapping. GetWindowForZone is reliable at this point.
                    var dropTargetWindow = this.GetWindowForZone(lastInsertionWidget.InsertionZone);

                    lastInsertionWidget.InsertionZone.ViewModel.DropAddSiblingIntoDock(
                        sourceDockZone.ViewModel,
                        lastInsertionWidget.Direction
                    );

                    // Always close the tearoff after a successful drop. For tab-add drops the
                    // source zone is emptied; for directional splits the source ViewModel is
                    // transplanted into the root hierarchy so DockedContent stays non-empty -
                    // but the tearoff window is no longer needed in either case.
                    this.SilentlyForceCloseTearoffWindow(dragSourceWindow);

                    // When the drag source closes, the OS activates whichever window it
                    // considers "next" - often the root window, not the drop target.
                    // Explicitly activate the target window so it stays in front.
                    dropTargetWindow?.Activate();
                }
                // No widget → tearoff stays at its current position as a detached window
            }
            finally
            {
                this.IsZoneDragDropUnderway = false;

                // Restore tearoff opacity. If CloseSourceTearoffIfEmpty already closed the
                // window these Win32 calls are harmless no-ops on the stale HWND.
                User32WindowFuncs.SetLayeredWindowAttributes(hwnd, 0, 255, User32WindowFuncs.kLwaAlpha);
                User32WindowFuncs.SetWindowLongPtr(hwnd, User32WindowFuncs.kGwlExStyle, originalExStyle);

                // Restore hovered-window opacity if arming was still active when drag ended
                if (isHoverArming && hoverHwnd != 0)
                {
                    User32WindowFuncs.SetLayeredWindowAttributes(hoverHwnd, 0, 255, User32WindowFuncs.kLwaAlpha);
                    User32WindowFuncs.SetWindowLongPtr(hoverHwnd, User32WindowFuncs.kGwlExStyle, hoverOrigExStyle);
                }

                if (lastInsertionWidget != null)
                {
                    lastInsertionWidget.IsEngaged = false;
                }

                if (lastDropTarget != null)
                {
                    lastDropTarget.IsDirectDropTarget = false;
                }
            }
        }

        // ===========[ Serialization ]========================================

        public bool SetupDefaultAndFallbackTo(string fallbackLayoutFilePath)
        {
            if (this.ReloadDockLayoutFromPersistentStorage())
            {
                this.UISyncVM.EnforceGuaranteedOnStart();
                m_isReadyToTrackAutoSave = true;
                return true;
            }
            else if (fallbackLayoutFilePath != null && this.LoadDockLayoutFromFile(fallbackLayoutFilePath))
            {
                this.UISyncVM.EnforceGuaranteedOnStart();
                this.SaveDockLayoutToPersistentStorage();
                m_isReadyToTrackAutoSave = true;
                return true;
            }

            this.UISyncVM.EnforceGuaranteedOnStart();
            return false;
        }

        public bool ReloadDockLayoutFromPersistentStorage()
        {
            try
            {
                return DockingSerialization.ResetFromState(this.DockLayoutPersistentStorageFile, this);
            }
            catch (Exception e)
            {
                Logger.LogError($"Docking: Error loading from layout file '{this.DockLayoutPersistentStorageFile ?? "<null>"}'", e);
                return false;
            }
        }

        public bool LoadDockLayoutFromFile(string filePath)
            => DockingSerialization.ResetFromState(filePath, this);

        public bool SaveDockLayoutToPersistentStorage()
            => this.SaveDockLayoutToFile(this.DockLayoutPersistentStorageFile);

        public bool SaveDockLayoutToFile(string filePath = null)
            => DockingSerialization.SerializeStateTo(filePath, this);

        public string GenerateDefaultStateStorageAutoSaveTempPath()
            => this.DockLayoutPersistentStorageFile + "~";

        // ===========[ Internal - used by DockingSerialization ]==============

        internal DockZoneViewModel GetFallbackRootZone()
            => m_rootDockZones.FirstOrDefault()?.ViewModel;

        internal DockZoneViewModel GetRootZone(string groupId)
            => m_rootDockZones.FirstOrDefault(z => DockZone.GetGroupId(z) == groupId)?.ViewModel;

        internal void ClearForLoadingFromState()
        {
            var tearoffs = m_dockZoneMapping.Keys.Where(w => w != this.RootWindow).ToList();
            m_windowsToCloseSilently.AddRange(tearoffs);

            foreach (var w in tearoffs)
            {
                m_tearoffRootZones.Remove(w);
                m_dockZoneMapping.Remove(w);
                m_windowManager.StopTracking(w);
                w.Close();
            }

            m_windowsToCloseSilently.Clear();

            foreach (var root in m_rootDockZones)
            {
                root.ViewModel?.ForceCloseAllAndClear();
            }
        }

        internal DockingSerialization.CoreStorageData BuildSerializationInfoForRoot()
        {
            var storage = new DockingSerialization.CoreStorageData();

            if (m_dockZoneMapping.TryGetValue(this.RootWindow, out var rootZones))
            {
                foreach (var rootZone in rootZones)
                {
                    string groupId = DockZone.GetGroupId(rootZone) ?? string.Empty;
                    if (!storage.ZoneInfoByRoot.TryAdd(groupId, rootZone.ViewModel.GenerateSerializationState()))
                    {
                        Logger.LogError($"Docking Serialization: Duplicate root zone group id '{groupId}' - serialization skipped for that zone");
                    }
                }
            }

            return storage;
        }

        internal IEnumerable<DockingSerialization.WindowStorageData> BuildSerializationInfoForAncillaryWindows()
        {
            // Snapshot the mapping so we iterate a stable list. Skip the root window and any
            // entry whose AppWindow is null (window already closed / in teardown).
            foreach (var (window, zones) in m_dockZoneMapping.ToList())
            {
                if (window == this.RootWindow)
                {
                    continue;
                }

                if (zones.Count == 0 || window.AppWindow == null)
                {
                    continue;
                }

                var appWindow = window.AppWindow;
                var pos = appWindow.Position;
                var size = appWindow.Size;
                var presenter = appWindow.Presenter;

                bool isMaximized = (presenter is Microsoft.UI.Windowing.OverlappedPresenter op)
                    && op.State == Microsoft.UI.Windowing.OverlappedPresenterState.Maximized;
                bool isFullscreen = presenter is Microsoft.UI.Windowing.FullScreenPresenter;

                yield return new DockingSerialization.WindowStorageData
                {
                    WindowX = pos.X,
                    WindowY = pos.Y,
                    WindowWidth = size.Width,
                    WindowHeight = size.Height,
                    IsMaximized = isMaximized,
                    IsFullscreen = isFullscreen,
                    State = zones.FirstOrDefault()?.ViewModel?.GenerateSerializationState()
                };
            }
        }

        internal Result<Window> CreateAndStockTearoffWindow(
            DockZoneViewModel rootZone,
            Point origin,
            Size size)
        {
            Window window = null;
            try
            {
                window = this.CreateNewTearoffWindowHandler();

                // Tool-window presenter: keep the resize border so the user can resize
                // the tearoff, but remove the OS title bar. DockTearoffContainerPanel
                // provides a custom title bar with drag handle and close button.
                var overlappedPresenter = Microsoft.UI.Windowing.OverlappedPresenter.Create();
                overlappedPresenter.SetBorderAndTitleBar(hasBorder: true, hasTitleBar: false);
                window.AppWindow.SetPresenter(overlappedPresenter);

                var newDockZone = new DockZone { ViewModel = rootZone };
                var panel = new DockTearoffContainerPanel
                {
                    DockZoneContent = newDockZone,
                    DragThresholdPixels = this.DragThresholdPixels,
                    Title = this.FixedTearoffWindowTitle,
                };

                panel.TitleBarDragInitiated += this.OnTearoffPanelDragInitiated;
                panel.CloseRequested += this.OnTearoffPanelCloseRequested;

                // Apply theme BEFORE assigning panel as window content. When window.Content
                // is set, the visual tree builds immediately (OnApplyTemplate chain fires) and
                // {ThemeResource} Style Setter values resolve using the element's current
                // ActualTheme. Setting RequestedTheme here ensures DockLeafLayout and all
                // other controls in the tearoff render with the correct theme on first paint.
                if (m_windowManager.Root?.Content is FrameworkElement rootContent)
                {
                    panel.RequestedTheme = rootContent.ActualTheme;
                }

                window.Content = panel;
                panel.SetupForWindow(window); // wire activation tracking + maximize-glyph updates
                m_windowManager.ApplyTheme(window); // belt-and-suspenders for any later theme change

                var appWindow = window.AppWindow;
                appWindow.Resize(new Windows.Graphics.SizeInt32((int)size.Width, (int)size.Height));
                appWindow.Move(new Windows.Graphics.PointInt32((int)origin.X, (int)origin.Y));
                appWindow.Closing += this.TearoffWindowAppWindow_EXTERNAL_OnClosing;

                m_tearoffRootZones[window] = rootZone;
                this.ShowTearoffWindowHandler(window);
                m_windowManager.Track(window);  // lifecycle tracking + bring-to-front ordering
                window.TrackActivation();        // activation visual state (active/inactive)
                this.RegisterRootDockZones(window, newDockZone);

                rootZone.ClearPassAlongUISize();
                return Result<Window>.Success(window);
            }
            catch (Exception exc)
            {
                window?.Close();
                Logger.LogError("Window tearoff creation failed!", exc);
                return Result<Window>.Error("DockingManager: Window tearoff failed");
            }
        }
        
        /// <summary>
        /// This WILL NOT WORK for programatically called Close() it will only work if the user creates
        /// a windows event routed here
        /// </summary>
        private void TearoffWindowAppWindow_EXTERNAL_OnClosing(AppWindow sender, AppWindowClosingEventArgs e)
        {
            Window window = m_windowManager.FirstOrDefault(w => w.AppWindow == sender);
            if (!this.DoTearoffWindowClose(window))
            {
                e.Cancel = true;
            }
        }

        // ===========[ Internal Helpers ]=====================================

        // Called by DockZone.OnHeaderPointerPressed to resolve the host Window.
        // Traverses m_dockZoneMapping; falls back to RootWindow if not found.
        internal Window GetWindowForZone(DockZone zone)
        {
            foreach (var (window, zones) in m_dockZoneMapping)
            {
                foreach (var rootZone in zones)
                {
                    if ((rootZone == zone)
                        || TreeTraversal<DockZone>.All(rootZone).Any(z => z == zone))
                    {
                        return window;
                    }
                }
            }

            return this.RootWindow;
        }

        // Returns the root DockZone registered for a given window, or null if
        // the window is not in m_dockZoneMapping. Used by OnHeaderPointerPressed
        // so that dragging a panel header inside a tearoff moves the whole tearoff
        // rather than tearing the panel out into yet another window.
        internal DockZone GetRootDockZoneForWindow(Window window)
            => m_dockZoneMapping.TryGetValue(window, out var zones) ? zones.FirstOrDefault() : null;

        // Exposes Win32 GetCursorPos to DockZone without duplicating the P/Invoke.
        internal bool TryGetCursorScreenPos(out Point pt)
        {
            if (User32WindowFuncs.GetCursorPos(out var raw))
            {
                pt = new Point(raw.X, raw.Y);
                return true;
            }

            pt = default;
            return false;
        }

        // ===========[ Private Helpers ]======================================

        private async void OnTearoffPanelDragInitiated(object sender, Point localPressPt)
        {
            var panel = (DockTearoffContainerPanel)sender;
            var sourceWindow = this.GetWindowForZone(panel.DockZoneContent);

            // If the tearoff is currently maximized, restore it before the drag begins.
            // DoGroupTearoff returns the existing window as-is (no size/position change),
            // so RunDragSearch would attempt to move a maximized window, which the OS ignores.
            // Restoring first lets AppWindow.Move work correctly.
            if (sourceWindow?.AppWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter ovPresenter
                && ovPresenter.State == Microsoft.UI.Windowing.OverlappedPresenterState.Maximized)
            {
                ovPresenter.Restore();

                // Place the restored window so the cursor appears near the left of the title bar.
                // localPressPt.X was captured in maximized (full-screen) coordinates and is likely
                // wider than the restored window, so use a fixed modest offset instead.
                const double kRestoredOffsetX = 40.0;
                if (User32WindowFuncs.GetCursorPos(out var cursorPt))
                {
                    sourceWindow.AppWindow.Move(new Windows.Graphics.PointInt32(
                        (int)(cursorPt.X - kRestoredOffsetX),
                        (int)(cursorPt.Y - localPressPt.Y)));
                }

                await this.RunDragSearch(sourceWindow, panel.DockZoneContent,
                    new Point(kRestoredOffsetX, localPressPt.Y));
                return;
            }

            await this.InitiateDrag(panel.DockZoneContent, cursorWindowOffset: localPressPt);
        }

        private void OnTearoffPanelCloseRequested(object sender, EventArgs e)
        {
            var panel = (DockTearoffContainerPanel)sender;
            if (this.GetWindowForZone(panel.DockZoneContent) is Window window)
            {
                this.DoTearoffWindowClose(window);
            }
        }

        // Unconditionally close and deregister a tearoff window (e.g. after a successful drop).
        private void SilentlyForceCloseTearoffWindow(Window sourceWindow)
        {
            if (!m_windowManager.Contains(sourceWindow) || sourceWindow == this.RootWindow)
            {
                return;
            }

            m_windowsToCloseSilently.Add(sourceWindow);
            try
            {
                m_tearoffRootZones.Remove(sourceWindow);
                m_dockZoneMapping.Remove(sourceWindow);
                m_windowManager.StopTracking(sourceWindow);  // unregisters Window.Closed so auto-cleanup won't double-fire
                sourceWindow.Close();
            }
            finally
            {
                m_windowsToCloseSilently.Remove(sourceWindow);
            }
        }

        // Conditionally close a tearoff window only when all docked content has been removed.
        // Kept for callers that need the empty-check (e.g. if we re-introduce partial drops).
        private void CloseSourceTearoffIfEmpty(Window sourceWindow, DockZone sourceDockZone)
        {
            bool hasContent = TreeTraversal<DockZone>.All(sourceDockZone)
                .Any(z => z.ViewModel?.DockedContent.Count > 0);

            if (!hasContent)
            {
                this.SilentlyForceCloseTearoffWindow(sourceWindow);
            }
        }

        private bool DoTearoffWindowClose(Window window)
        {
            if (m_windowsToCloseSilently.Contains(window) || m_currentlyClosingWindows.Contains(window))
            {
                return false;
            }

            m_currentlyClosingWindows.Add(window);
            try
            {
                if (!m_tearoffRootZones.TryGetValue(window, out DockZoneViewModel root))
                {
                    return false;
                }

                if (root.IsActivelyAttemptingClose)
                {
                    return false;
                }

                // Route HideDontClose panels through RemoveOrHidePanel so they become
                // hidden (not destroyed) and the UISyncVM toggle state stays correct.
                // Mark this window as closing-silently first so AfterPanelHidden won't
                // try to re-close it when the last hideable panel is removed.
                m_windowsToCloseSilently.Add(window);

                var allAdapters = TreeTraversal<DockZoneViewModel>.All(root)
                    .SelectMany(z => z.DockedContent).ToList();
                var hideable = allAdapters.Where(a => a.HideDontClose).ToList();
                foreach (var adapter in hideable)
                {
                    this.UISyncVM.CloseOrHidePanel(adapter);
                }

                // Close remaining (non-hideable) panels normally
                var closeResult = root.RequestCloseAllAndClear();
                if (closeResult.HasErrors)
                {
                    return false;
                }

                m_tearoffRootZones.Remove(window);
                m_dockZoneMapping.Remove(window);
                m_windowManager.StopTracking(window);
                this.RootWindow?.Activate();

                return true;
            }
            finally
            {
                m_currentlyClosingWindows.Remove(window);
                m_windowsToCloseSilently.Remove(window);
            }
        }

        // WindowManager Remove events fire both for actual window closes AND for
        // in-place reordering (Remove + immediate re-Insert when activation changes).
        // Removing from m_dockZoneMapping here would erase live tearoff entries on
        // every activation reorder. Cleanup is handled exclusively by
        // OnTearoffWindowClosing (AppWindow.Closing), CloseAll, and OnRootWindowClosed.
        private void OnWindowManagerCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            // Intentional no-op - see comment above.
        }

        // Force-close all tearoff windows when the root window closes so no
        // orphaned tool windows linger after the app exits. We bypass the normal
        // CanClose veto path by adding windows to m_windowsToCloseSilently first,
        // which causes OnTearoffWindowClosing to exit early without cancelling.
        private void OnRootWindowClosed(object sender, WindowEventArgs e)
        {
            var tearoffs = m_dockZoneMapping.Keys.Where(w => w != this.RootWindow).ToList();
            m_windowsToCloseSilently.AddRange(tearoffs);
            foreach (var w in tearoffs)
            {
                m_tearoffRootZones.Remove(w);
                m_dockZoneMapping.Remove(w);
                m_windowManager.StopTracking(w);
                w.Close();
            }

            m_windowsToCloseSilently.Clear();
        }

        // When the root window is re-activated (user clicks our taskbar entry, switches
        // back from another app, etc.), bring all tearoff windows back to front so they
        // don't stay buried behind unrelated app windows.
        // Windows are brought back-to-front (least-recently-activated first) so the
        // most-recently-activated tearoff ends up closest to the root; root is raised
        // last to stay on top of all tearoffs.
        private void OnRootWindowActivated(object sender, WindowActivatedEventArgs e)
        {
            if (e.WindowActivationState == WindowActivationState.Deactivated)
            {
                return;
            }

            // Snapshot in WindowManager's activation order (index 0 = most recently active).
            // Iterate in reverse so the most-recently-active tearoff is raised last.
            var tearoffs = m_windowManager
                .Where(w => w != this.RootWindow && m_dockZoneMapping.ContainsKey(w))
                .Reverse()
                .ToList();

            foreach (var w in tearoffs)
            {
                try { w.AppWindow?.MoveInZOrderAtTop(); }
                catch { /* window may be in teardown */ }
            }

            // Keep root window on top (it was just activated by the user).
            try { this.RootWindow.AppWindow?.MoveInZOrderAtTop(); }
            catch { }
        }

        private void TriggerLayoutAutoSave()
        {
            if (this.IsLoadingFromLayout || !m_isReadyToTrackAutoSave)
            {
                return;
            }

            if (this.AutoSaveMethod == eDockingAutoSaveMethod.AutoSaveOnAllChanges)
            {
                this.SaveDockLayoutToPersistentStorage();
            }
            else if (this.AutoSaveMethod == eDockingAutoSaveMethod.AutoSaveToTemp)
            {
                this.SaveDockLayoutToFile(this.GenerateDefaultStateStorageAutoSaveTempPath());
            }
        }

        private IDockableDisplayElement SetupAndReturnNew(IDockableDisplayElement displayElement)
        {
            if (displayElement == null)
            {
                return null;
            }

            var adapter = new DockingContentAdapterModel(this);
            adapter.TitleContent = StringXT.ConvertToFriendlyEn(displayElement.GetType().Name);
            displayElement.Setup(adapter);
            displayElement.DockingAdapter.Display = displayElement;
            return displayElement;
        }

        private static IDockableDisplayElement BuildDefaultDisplayFor(Type elementType)
            => AJutActivator.CreateInstanceOf(elementType) as IDockableDisplayElement;

        // ===========[ Sub-classes ]===========================================

        private class DisplayBuilder
        {
            public DockPanelRegistrationRules Rules { get; init; }
            public bool IsSingleInstanceOnly => this.Rules.SingleInstanceOnly;
            public Func<IDockableDisplayElement> Builder { get; init; }
        }

        // All user32.dll P/Invokes and their associated constants live here so
        // they are self-contained and not scattered throughout DockingManager.
        private static class User32WindowFuncs
        {
            internal const int kGwlExStyle = -20;
            internal const int kWsExLayered = 0x80000;
            internal const uint kLwaAlpha = 0x2;
            internal const int kVkLButton = 0x01;

            [DllImport("user32.dll")]
            internal static extern bool GetCursorPos(out POINT lpPoint);

            [DllImport("user32.dll")]
            internal static extern short GetAsyncKeyState(int vKey);

            [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
            internal static extern nint GetWindowLongPtr(nint hWnd, int nIndex);

            [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
            internal static extern nint SetWindowLongPtr(nint hWnd, int nIndex, nint dwNewLong);

            [DllImport("user32.dll")]
            internal static extern bool SetLayeredWindowAttributes(nint hwnd, uint crKey, byte bAlpha, uint dwFlags);

            [DllImport("user32.dll")]
            internal static extern bool ScreenToClient(nint hWnd, ref POINT lpPoint);

            [StructLayout(LayoutKind.Sequential)]
            internal struct POINT { public int X; public int Y; }
        }
    }
}
