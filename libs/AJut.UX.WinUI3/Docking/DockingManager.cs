namespace AJut.UX.Docking
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Threading.Tasks;
    using AJut;
    using AJut.Storage;
    using AJut.Tree;
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
    // The registry, the display factory, tearoff bookkeeping and every close path live in
    // DockingManagerBase in AJut.UX, shared with the other toolkits. This adds the WinUI3 parts:
    //   - Microsoft.UI.Xaml.Window for tearoff windows
    //   - AppWindow.Move / Resize instead of Window.Left / .Width etc.
    //   - AppWindow.Closing (cancelable) for tearoff close interception
    //   - Win32 GetCursorPos / GetAsyncKeyState P/Invokes for RunDragSearch
    //   - VisualTreeHelper.FindElementsInHostCoordinates for drop hit-testing
    //   - The managed MenuBarItem
    public class DockingManager : DockingManagerBase<Window, DockZone>
    {
        // ===========[ Constants ]============================================
        private const double kTearoffDragOpacity = 0.65;

        // ===========[ Fields ]===============================================
        private readonly WindowManager m_windowManager = new();
        private MenuBarItem m_managedMenuBarItem;

        // ===========[ Construction / Disposal ]=========================================
        public DockingManager(
            Window rootWindow,
            string uniqueId,
            string persistentStorageFilePath = null,
            eDockingAutoSaveMethod autoSaveMethod = eDockingAutoSaveMethod.None)
            : base(rootWindow, uniqueId, persistentStorageFilePath, autoSaveMethod)
        {
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

        protected override void OnDisposing ()
        {
            // Drop the menu bar subscriptions before everything else - if the menu was
            // wired up via ManageMenu, those subs are the loudest stragglers because
            // they capture the MenuBarItem (which lives on RootWindow).
            if (m_managedMenuBarItem != null)
            {
                this.UISyncVM.PanelStateChanged -= this.OnUISyncVMPanelStateChanged;
                ((INotifyCollectionChanged)this.UISyncVM.PanelTypeEntries).CollectionChanged -= this.OnUISyncVMPanelTypeEntriesChanged;

                // The MenuFlyoutItem / ToggleMenuFlyoutItem children added by RebuildMenuItems
                // each have a Click handler whose target is this manager. Drop them now or
                // they will pin this manager for as long as the MenuBarItem stays alive.
                this.DetachAndRemoveManagedMenuItems(m_managedMenuBarItem);
                m_managedMenuBarItem = null;
            }

            // Unsubscribe from root window events to break the reference chain
            // that keeps this DockingManager (and everything it references) alive
            // as long as the root window exists.
            this.RootWindow.Closed -= this.OnRootWindowClosed;
            this.RootWindow.Activated -= this.OnRootWindowActivated;
            ((INotifyCollectionChanged)m_windowManager).CollectionChanged -= this.OnWindowManagerCollectionChanged;

            // The window manager itself subscribes to RootWindow events during Setup.
            // Without this Dispose call its subs would keep RootWindow -> WindowManager ->
            // DockingManager pinned for the life of the application.
            m_windowManager.Dispose();

            this.CreateNewTearoffWindowHandler = null;
            this.ShowTearoffWindowHandler = null;
        }

        protected override void OnRootZoneDisposing (DockZone zone)
        {
            zone.Loaded -= this.MainWindowDockZone_Loaded;
            zone.Unloaded -= this.MainWindowDockZone_Unloaded;

            // The drop overlay's 5 DockDropInsertionDriverWidgets each hold InsertionZone = their
            // zone. A widget whose native peer lingers past teardown would otherwise root the whole
            // docked graph through that back-ref. Sever it on every zone in the tree, not just root.
            foreach (DockZone treeZone in TreeTraversal<DockZone>.All(zone))
            {
                treeZone.ReleaseDropOverlay();
            }
        }

        // ===========[ Properties ]===========================================

        /// <summary>Factory for creating new tearoff windows. Default creates a plain Window.</summary>
        public Func<Window> CreateNewTearoffWindowHandler { private get; set; }

        /// <summary>Action called to show a tearoff window. Default calls Window.Activate().</summary>
        public Action<Window> ShowTearoffWindowHandler { get; set; }

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
        public double DragThresholdPixels { get; set; } = DockDragRules.kDefaultDragThreshold;

        /// <summary>
        /// Title text displayed in the title bar of every tearoff window.
        /// Applied to DockTearoffContainerPanel.Title when the tearoff window is created.
        /// Empty string by default (title bar shows only the icon and decorative lines).
        /// </summary>
        public string FixedTearoffWindowTitle { get; set; } = string.Empty;

        // ===========[ Menu Management ]======================================

        /// <summary>
        /// Populate a <see cref="MenuBarItem"/> with toggle/add entries for each registered
        /// panel type. The menu stays synchronized with panel state - checked items indicate
        /// visible panels; clicking a toggle item shows/hides the panel.
        /// </summary>
        public void ManageMenu (MenuBarItem menuBarItem)
        {
            // If the caller hands us a different bar than last time, drop the prior subs
            // first so the old bar isn't held hostage and we don't end up double-firing.
            if (m_managedMenuBarItem != null && m_managedMenuBarItem != menuBarItem)
            {
                this.UISyncVM.PanelStateChanged -= this.OnUISyncVMPanelStateChanged;
                ((INotifyCollectionChanged)this.UISyncVM.PanelTypeEntries).CollectionChanged -= this.OnUISyncVMPanelTypeEntriesChanged;
                this.DetachAndRemoveManagedMenuItems(m_managedMenuBarItem);
            }

            m_managedMenuBarItem = menuBarItem;
            this.RebuildMenuItems(menuBarItem);

            // Defensive -= before += so re-entry into ManageMenu (same bar) cannot double-subscribe.
            this.UISyncVM.PanelStateChanged -= this.OnUISyncVMPanelStateChanged;
            this.UISyncVM.PanelStateChanged += this.OnUISyncVMPanelStateChanged;

            var entries = (INotifyCollectionChanged)this.UISyncVM.PanelTypeEntries;
            entries.CollectionChanged -= this.OnUISyncVMPanelTypeEntriesChanged;
            entries.CollectionChanged += this.OnUISyncVMPanelTypeEntriesChanged;
        }

        private void OnUISyncVMPanelStateChanged (object sender, DockPanelAddRemoveUISync.PanelStateChangedEventArgs e)
        {
            if (m_managedMenuBarItem == null)
            {
                return;
            }

            if (e.IsStructuralChange)
            {
                this.RebuildMenuItems(m_managedMenuBarItem);
            }
            else
            {
                this.UpdateMenuCheckedStates(m_managedMenuBarItem);
            }
        }

        private void OnUISyncVMPanelTypeEntriesChanged (object sender, NotifyCollectionChangedEventArgs e)
        {
            if (m_managedMenuBarItem != null)
            {
                this.RebuildMenuItems(m_managedMenuBarItem);
            }
        }

        // Drops every menu item that RebuildMenuItems added, unsubscribing each Click
        // handler before removing the item. Without the -= step, the removed items
        // would still hold strong refs back to this manager via their handler targets
        // until GC collected them. Used by Dispose, ManageMenu (when re-targeting to
        // a different bar), and RebuildMenuItems (which wipes before rebuilding).
        private void DetachAndRemoveManagedMenuItems (MenuBarItem menuBarItem)
        {
            for (int i = menuBarItem.Items.Count - 1; i >= 0; --i)
            {
                var item = menuBarItem.Items[i];
                if (item is ToggleMenuFlyoutItem toggle)
                {
                    toggle.Click -= this.OnManagedToggleMenuItemClicked;
                }
                else if (item is MenuFlyoutItem flyout)
                {
                    flyout.Click -= this.OnManagedAddMenuItemClicked;
                }
                menuBarItem.Items.RemoveAt(i);
            }
        }

        private void RebuildMenuItems (MenuBarItem menuBarItem)
        {
            this.DetachAndRemoveManagedMenuItems(menuBarItem);

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

                zone.Manager = this;

                if (zone.ViewModel == null)
                {
                    zone.ViewModel = new DockZoneViewModel(this);
                }

                this.RecordRootZone(window, zone);

                // Rebuild layout now that the zone is fully registered with its window
                // so GetWindowForZone returns the correct window (fixes isInTearoff checks,
                // close button visibility, header-drag suppression for tearoff root zones).
                zone.TriggerLayoutRebuild();
            }
        }

        protected override void OnRootZoneDeregistered (DockZone zone)
        {
            zone.Manager = null;
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
            List<Window> tearoffs = this.WindowsWithZones.Where(w => w != this.RootWindow).ToList();
            foreach (Window window in tearoffs)
            {
                window.Hide();
            }
        }

        // ===========[ Tearoff Window Ops ]===================================

        public Result<Window> DoTearoff(IDockableDisplayElement element, Point newWindowOrigin)
        {
            DockZoneSize dockSize = element.DockingAdapter.Location?.UI?.RenderSize ?? DockZoneSize.Empty;
            return this.DoTearoff(element, newWindowOrigin, new Size(dockSize.Width, dockSize.Height));
        }

        public Result<Window> DoTearoff(IDockableDisplayElement element, Point newWindowOrigin, Size newWindowSize)
            => this.DoTearoffCore(element, new DockWindowBounds(newWindowOrigin.X, newWindowOrigin.Y, newWindowSize.Width, newWindowSize.Height));

        public Result<Window> DoGroupTearoff(DockZoneViewModel sourceZone, Point newWindowOrigin)
        {
            var size = sourceZone.UI?.RenderSize ?? DockZoneSize.Empty;
            return this.DoGroupTearoff(sourceZone, newWindowOrigin, new Size(size.Width, size.Height));
        }

        public Result<Window> DoGroupTearoff(DockZoneViewModel sourceZone, Point newWindowOrigin, Size newTearoffWindowSize)
            => this.DoGroupTearoffCore(sourceZone, new DockWindowBounds(newWindowOrigin.X, newWindowOrigin.Y, newTearoffWindowSize.Width, newTearoffWindowSize.Height));

        // Initiates a zone drag: tears the zone off into its own window first, then
        // immediately starts RunDragSearch with that tearoff window as the drag source.
        // This means the root window's zones become valid drop targets (they are no
        // longer the "source" window and are not skipped in hit-testing).
        //
        // pressScreenPos:    screen cursor position when the user first pressed (for correct
        //                    window origin placement). If null, current cursor pos is used.
        // cursorWindowOffset: where the cursor should appear within the tearoff window, in XAML
        //                    units (i.e. the window is placed so this offset lands under the cursor).
        //                    If null, defaults to (windowWidth/2, 15). The placement scales it to
        //                    the physical pixels the cursor and AppWindow measure in.
        internal async Task InitiateDrag(DockZone zone, Point? pressScreenPos = null, Point? cursorWindowOffset = null)
        {
            if (!User32WindowFuncs.GetCursorPos(out var pt))
            {
                return;
            }

            DockTearoffPlacement placement = DockDragRules.PlaceTearoff(
                ((IDockZoneUI)zone).RenderSize,
                ToDockPoint(pressScreenPos ?? new Point(pt.X, pt.Y)),
                cursorWindowOffset.HasValue ? ToDockPoint(cursorWindowOffset.Value) : null,
                this.GetDisplayScale()
            );

            // A tearoff's own root zone moves its existing window, so a canceled drag has nowhere to go back to and
            // only stops. Anything else is torn out of a zone tree, and goes back there on cancel.
            Window hostWindow = this.GetWindowForZone(zone);
            bool isExistingTearoffRoot = (hostWindow != this.RootWindow) && (this.GetRootDockZoneForWindow(hostWindow) == zone);
            DockDragOrigin origin = isExistingTearoffRoot ? null : DockDragOrigin.ForZone(zone.ViewModel);

            var tearoffResult = this.DoGroupTearoff(
                zone.ViewModel,
                new Point(placement.Origin.X, placement.Origin.Y),
                new Size(placement.Width, placement.Height)
            );

            if (!tearoffResult)
            {
                return;
            }

            var tearoffWindow = tearoffResult.Value;

            if (!this.TryGetZonesForWindow(tearoffWindow, out var tearoffZones)
                || tearoffZones.Count == 0)
            {
                return;
            }

            await this.RunDragSearch(tearoffWindow, tearoffZones[0], new Point(placement.ScreenCursorOffset.X, placement.ScreenCursorOffset.Y), origin);
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

            DockTearoffPlacement placement = DockDragRules.PlaceTearoff(
                element.DockingAdapter.Location?.UI?.RenderSize ?? DockDragRules.kFallbackPanelSize,
                ToDockPoint(pressScreenPos ?? new Point(pt.X, pt.Y)),
                cursorWindowOffset.HasValue ? ToDockPoint(cursorWindowOffset.Value) : null,
                this.GetDisplayScale()
            );

            DockDragOrigin origin = DockDragOrigin.ForPanel(element.DockingAdapter);
            var tearoffResult = this.DoTearoff(
                element,
                new Point(placement.Origin.X, placement.Origin.Y),
                new Size(placement.Width, placement.Height)
            );

            if (!tearoffResult)
            {
                return;
            }

            var tearoffWindow = tearoffResult.Value;

            if (!this.TryGetZonesForWindow(tearoffWindow, out var tearoffZones)
                || tearoffZones.Count == 0)
            {
                return;
            }

            await this.RunDragSearch(tearoffWindow, tearoffZones[0], new Point(placement.ScreenCursorOffset.X, placement.ScreenCursorOffset.Y), origin);
        }

        // Drag search: moves the tearoff window with the cursor while polling for drop targets.
        // Hit-tests all registered windows EXCEPT the drag source for DockDropInsertionDriverWidget
        // elements (directional arrows shown when DockZone.IsDirectDropTarget=true).
        //
        // On mouse-up over a widget: DropAddSiblingIntoDock; close tearoff if now empty.
        // On mouse-up elsewhere: tearoff stays at current position as a detached window.
        // On Escape: the drag is canceled. What the drag tore off goes back where it came from
        // and the tearoff closes; a tearoff that existed before the drag just stays put.
        //
        // Called from InitiateDrag (UI thread); Task.Delay resumes on the UI thread because
        // WinUI3 registers a DispatcherQueueSynchronizationContext.
        //
        // cursorWindowOffset: where the cursor appears within the dragged window, in physical pixels
        // like the cursor position it is kept from. Defaults to the window's center across and 15
        // XAML units down, scaled to pixels, when not supplied.
        public Task RunDragSearch(Window dragSourceWindow, DockZone sourceDockZone, Point? cursorWindowOffset = null)
            => this.RunDragSearch(dragSourceWindow, sourceDockZone, cursorWindowOffset, null);

        // origin: where what the drag tore off came from, which a canceled drag returns it to; null
        // when the drag moves a tearoff that already existed.
        private async Task RunDragSearch(Window dragSourceWindow, DockZone sourceDockZone, Point? cursorWindowOffset, DockDragOrigin origin)
        {
            // The shared session decides which overlay shows, which widget is engaged, when the drag
            // source dims, and when a window the cursor rests on flashes and is raised. The host does
            // each of those with Win32 layered-window alpha and AppWindow z-order.
            var host = new DragSearchHost(this, dragSourceWindow);
            var session = new DockDragSession<DockZone, DockDropInsertionDriverWidget, Window>(host);
            var sourceAppWindow = dragSourceWindow.AppWindow;

            // Resolve the cursor-in-window offset once; fall back to the centered title bar default.
            double resolvedOffsetX = cursorWindowOffset.HasValue ? cursorWindowOffset.Value.X : -1;
            double resolvedOffsetY = cursorWindowOffset.HasValue ? cursorWindowOffset.Value.Y : DockDragRules.kDefaultCursorOffsetY * this.GetDisplayScale();

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

                    // Escape cancels: nothing drops, whatever the button does next
                    if ((User32WindowFuncs.GetAsyncKeyState(User32WindowFuncs.kVkEscape) & 0x8000) != 0)
                    {
                        session.Cancel();
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
                    // because the registered windows iterate in arbitrary dict order - A might be
                    // visited before B even when B is in front of A at the cursor position.
                    Window hoverCandidateWindow = null;
                    int    hoverCandidateIdx    = int.MaxValue;

                    // Iterate non-drag windows frontmost-first (ascending window order index)
                    // so both the drop hit-test and hover-candidate tracking always pick the
                    // topmost visible window at the cursor - not whichever window happens to
                    // be first in the dictionary. The drop hit-test breaks on the first hit, so
                    // iterating frontmost-first ensures a foreground window's zones are preferred
                    // over background zones that overlap the same screen coordinates.
                    foreach (var window in this.WindowsWithZones
                        .Where(w => w != dragSourceWindow)
                        .OrderBy(w => { int i = host.WindowOrder.IndexOf(w); return i >= 0 ? i : int.MaxValue; }))
                    {
                        this.TryGetZonesForWindow(window, out var zones);

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
                        int windowOrderIdx = host.WindowOrder.IndexOf(window);
                        var winSize        = window.AppWindow.Size;
                        if (windowOrderIdx > host.DragSourceOrderIndex
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

                    // Only elevate if the candidate is NOT the globally frontmost non-drag dock
                    // window - that window is already accessible and needs no bring-forward.
                    // Hovering over it (or over a background window it occludes) should be silent.
                    Window currentHoverWindow = (hoverCandidateIdx > host.FrontmostNonDragOrderIndex)
                        ? hoverCandidateWindow
                        : null;

                    session.Update(currentDropTarget, currentInsertionWidget, currentHoverWindow);
                }

                // Execute drop if a widget was engaged when button was released
                DockDropInsertionDriverWidget engagedWidget = session.EngagedWidget;
                if (engagedWidget?.InsertionZone?.ViewModel != null)
                {
                    // Capture the target window NOW, before the drop restructures the zone
                    // tree and before closing the drag source removes it from the registry.
                    // GetWindowForZone is reliable at this point.
                    var dropTargetWindow = this.GetWindowForZone(engagedWidget.InsertionZone);

                    this.CommitDrop(
                        engagedWidget.InsertionZone.ViewModel,
                        sourceDockZone.ViewModel,
                        engagedWidget.Direction,
                        dragSourceWindow,
                        dropTargetWindow
                    );
                }
                else if (session.IsCanceled && (origin != null))
                {
                    // Canceled: what the drag tore off is dropped back where it came from, which closes the
                    // tearoff the drag made, then gets its tab position, selection and split sizes back
                    this.ReturnCanceledDrag(origin, sourceDockZone.ViewModel, dragSourceWindow);
                }
                // No widget, or canceled with nowhere to go back to: tearoff stays at its current position as a detached window
            }
            finally
            {
                this.IsZoneDragDropUnderway = false;

                // Ends any flash, disengages the widget and hides the overlay. Restoring the
                // tearoff's own opacity and style after a drop already closed the window is a
                // harmless no-op on the stale HWND.
                session.End();
                host.RestoreDragSource();
            }
        }

        // ===========[ DockingManagerBase Implementations ]===================

        protected override DockZoneViewModel GetZoneViewModel (DockZone zone) => zone.ViewModel;

        protected override string GetZoneGroupId (DockZone zone) => DockZone.GetGroupId(zone);

        protected override IEnumerable<DockZone> EnumerateZoneTree (DockZone rootZone) => TreeTraversal<DockZone>.All(rootZone);

        // Window placement on WinUI3 is in physical pixels, from AppWindow
        protected override DockWindowBounds GetWindowBounds (Window window)
        {
            var appWindow = window.AppWindow;
            return new DockWindowBounds(appWindow.Position.X, appWindow.Position.Y, appWindow.Size.Width, appWindow.Size.Height);
        }

        // A tearoff is created at a zone's logical size and CreateAndStockTearoffWindow scales it by the DPI into the
        // physical pixels AppWindow places windows in. Placement bounds (from AppWindow, layout files and hidden panels)
        // are already physical, so their size comes back down by the same scale first, or every load and re-show would
        // grow the window by it. The position is physical on both sides and stays as it is.
        protected override DockWindowBounds ToTearoffCreationBounds (DockWindowBounds placementBounds)
        {
            double dpiScale = (this.RootWindow.Content as FrameworkElement)?.XamlRoot?.RasterizationScale ?? 1.0;
            return new DockWindowBounds(placementBounds.X, placementBounds.Y, placementBounds.Width / dpiScale, placementBounds.Height / dpiScale);
        }
        protected override bool IsWindowTracked (Window window) => m_windowManager.Contains(window);

        protected override void StopTrackingWindow (Window window) => m_windowManager.StopTracking(window);

        protected override void CloseWindow (Window window) => window.Close();

        protected override void ActivateWindow (Window window) => window.Activate();

        // Leaves out a window whose AppWindow is already gone (closed, or in teardown)
        protected override bool TryCaptureWindowLayoutState (Window window, out DockWindowLayoutState state)
        {
            var appWindow = window.AppWindow;
            if (appWindow == null)
            {
                state = default;
                return false;
            }

            var presenter = appWindow.Presenter;
            bool isMaximized = (presenter is Microsoft.UI.Windowing.OverlappedPresenter op)
                && op.State == Microsoft.UI.Windowing.OverlappedPresenterState.Maximized;
            bool isFullscreen = presenter is Microsoft.UI.Windowing.FullScreenPresenter;

            state = new DockWindowLayoutState(this.GetWindowBounds(window), isMaximized, isFullscreen);
            return true;
        }

        protected override void ApplyWindowLayoutState (Window window, DockWindowLayoutState state)
        {
            var appWindow = window.AppWindow;
            if (state.IsFullscreen)
            {
                appWindow.SetPresenter(AppWindowPresenterKind.FullScreen);
            }
            else
            {
                (appWindow.Presenter as OverlappedPresenter)?.Maximize();
            }
        }

        protected override Result<Window> CreateAndStockTearoffWindow (DockZoneViewModel rootZone, DockWindowBounds bounds)
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

                // RenderSize (the source of size) is in effective/logical pixels, but
                // AppWindow.Resize operates in raw/physical pixels. Scale by DPI.
                double dpiScale = (this.RootWindow.Content as FrameworkElement)
                    ?.XamlRoot?.RasterizationScale ?? 1.0;

                appWindow.Resize(new Windows.Graphics.SizeInt32(
                    (int)(bounds.Width * dpiScale),
                    (int)(bounds.Height * dpiScale)));
                appWindow.Move(new Windows.Graphics.PointInt32((int)bounds.X, (int)bounds.Y));
                appWindow.Closing += this.TearoffWindowAppWindow_EXTERNAL_OnClosing;
                window.Activated += this.OnTearoffWindowActivated;

                this.RecordTearoffRootZone(window, rootZone);
                this.ShowTearoffWindowHandler(window);
                m_windowManager.Track(window);  // lifecycle tracking + bring-to-front ordering
                window.TrackActivation();        // activation visual state (active/inactive)
                this.RegisterRootDockZones(window, newDockZone);

                rootZone.ClearPassAlongUISize();
                return Result<Window>.Success(window);
            }
            catch (Exception exc)
            {
                if (window != null)
                {
                    this.DetachTearoffSubscriptions(window);
                    window.Close();
                }

                Logger.LogError("Window tearoff creation failed!", exc);
                return Result<Window>.Error("DockingManager: Window tearoff failed");
            }
        }

        // Drops the four subs DockingManager owns on a tearoff (AppWindow.Closing, Window.Activated
        // and the two panel-level events). Without this, the window's invocation lists keep a
        // strong ref to DockingManager until the OS finalizes the window object - which is
        // never deterministic and pins everything for the duration. Always call this just
        // before the window.Close() (or before the panel/window is otherwise abandoned).
        protected override void DetachTearoffSubscriptions(Window window)
        {
            if (window == null)
            {
                return;
            }

            window.Activated -= this.OnTearoffWindowActivated;

            // AppWindow can be null late in teardown; guard so this is safe to call from
            // any of the close paths.
            AppWindow appWindow = null;
            try { appWindow = window.AppWindow; }
            catch { /* window already destroyed */ }
            if (appWindow != null)
            {
                appWindow.Closing -= this.TearoffWindowAppWindow_EXTERNAL_OnClosing;
            }

            if (window.Content is DockTearoffContainerPanel panel)
            {
                panel.TitleBarDragInitiated -= this.OnTearoffPanelDragInitiated;
                panel.CloseRequested -= this.OnTearoffPanelCloseRequested;

                // Drop the panel's own AppWindow.Changed sub and its caption buttons' Window.Activated
                // subs, so the closing window's native peer can't keep the panel + DockZone graph rooted.
                panel.TeardownWindowHooks();
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
        // Searches every window's registered zones; falls back to RootWindow if not found.
        internal new Window GetWindowForZone(DockZone zone)
            => base.GetWindowForZone(zone);

        // Returns the root DockZone registered for a given window, or null if
        // the window has none registered. Used by OnHeaderPointerPressed
        // so that dragging a panel header inside a tearoff moves the whole tearoff
        // rather than tearing the panel out into yet another window.
        internal DockZone GetRootDockZoneForWindow(Window window)
            => this.GetRootZoneForWindow(window);

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

        // Physical pixels per XAML unit on the root window's display: what AppWindow and GetCursorPos
        // measure in, against what XAML sizes and pointer points measure in
        private double GetDisplayScale()
            => (this.RootWindow.Content as FrameworkElement)?.XamlRoot?.RasterizationScale ?? 1.0;

        private static DockPoint ToDockPoint(Point point) => new DockPoint(point.X, point.Y);

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
                // wider than the restored window, so use a fixed modest offset instead. Both are in
                // XAML units, and the cursor and AppWindow are in physical pixels.
                const double kRestoredOffsetX = 40.0;
                double displayScale = this.GetDisplayScale();
                var screenOffset = new Point(kRestoredOffsetX * displayScale, localPressPt.Y * displayScale);
                if (User32WindowFuncs.GetCursorPos(out var cursorPt))
                {
                    sourceWindow.AppWindow.Move(new Windows.Graphics.PointInt32(
                        (int)(cursorPt.X - screenOffset.X),
                        (int)(cursorPt.Y - screenOffset.Y)));
                }

                await this.RunDragSearch(sourceWindow, panel.DockZoneContent, screenOffset);
                return;
            }

            await this.InitiateDrag(panel.DockZoneContent, cursorWindowOffset: localPressPt);
        }

        private void OnTearoffPanelCloseRequested(object sender, EventArgs e)
        {
            var panel = (DockTearoffContainerPanel)sender;
            if (this.GetWindowForZone(panel.DockZoneContent) is Window window)
            {
                // DoTearoffWindowClose cleans up tracking but doesn't close the window itself
                // (it's designed for the AppWindow.Closing path where the OS handles the close),
                // so the base closes it once the close goes through.
                this.CloseTearoffWindowOnRequest(window);
            }
        }

        // WindowManager Remove events fire both for actual window closes AND for
        // in-place reordering (Remove + immediate re-Insert when activation changes).
        // Dropping a window's registered zones here would erase live tearoff entries on
        // every activation reorder. Cleanup is handled exclusively by the close paths
        // (AppWindow.Closing, CloseAll, and the root window closing).
        private void OnWindowManagerCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            // Intentional no-op - see comment above.
        }

        // Force-close all tearoff windows when the root window closes so no
        // orphaned tool windows linger after the app exits. The base adds them to
        // the close-silently list first, which makes the AppWindow.Closing handler
        // exit early without cancelling.
        private void OnRootWindowClosed(object sender, WindowEventArgs e)
        {
            this.CloseTearoffsForRootWindowClosed();
        }

        // When the root window is re-activated (user clicks our taskbar entry, switches
        // back from another app, etc.), bring all tearoff windows back to front so they
        // don't stay buried behind unrelated app windows.
        // Windows are brought back-to-front (least-recently-activated first) so the
        // most-recently-activated tearoff ends up closest to the root; root is raised
        // last to stay on top of all tearoffs.
        private void OnRootWindowActivated(object sender, WindowActivatedEventArgs e)
        {
            this.RefreshPanelFocusInWindow(this.RootWindow, e.WindowActivationState != WindowActivationState.Deactivated);
            if (e.WindowActivationState == WindowActivationState.Deactivated)
            {
                return;
            }

            // Snapshot in WindowManager's activation order (index 0 = most recently active).
            // Iterate in reverse so the most-recently-active tearoff is raised last.
            var tearoffs = m_windowManager
                .Where(w => w != this.RootWindow && this.TryGetZonesForWindow(w, out _))
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

        private void OnTearoffWindowActivated(object sender, WindowActivatedEventArgs e)
        {
            if (sender is Window window)
            {
                this.RefreshPanelFocusInWindow(window, e.WindowActivationState != WindowActivationState.Deactivated);
            }
        }

        // A WinUI window keeps its focused element when it deactivates, so the zones are told, and the panel being typed
        // into stops showing it while its window is not the active one, as it does on WPF
        private void RefreshPanelFocusInWindow(Window window, bool isActive)
        {
            if (!this.TryGetZonesForWindow(window, out IReadOnlyList<DockZone> rootZones))
            {
                return;
            }

            foreach (DockZone rootZone in rootZones)
            {
                foreach (DockZone zone in TreeTraversal<DockZone>.All(rootZone))
                {
                    zone.RefreshPanelFocus(isActive);
                }
            }
        }

        // ===========[ Sub-classes ]===========================================

        // The WinUI3 side of one drag search, for the shared DockDragSession. Dimming and flashing
        // use Win32 layered-window alpha, which needs WS_EX_LAYERED on the window: the drag source
        // gets it for the whole drag, and a hovered window only while it flashes, each put back
        // the way it was afterward. Raising a window also keeps this run's snapshot of the window
        // order current, since MoveInZOrderAtTop does not fire WindowManager activation events.
        private sealed class DragSearchHost : IDockDragSessionHost<DockZone, DockDropInsertionDriverWidget, Window>
        {
            private readonly DockingManager m_manager;
            private readonly Window m_dragSourceWindow;
            private readonly nint m_dragSourceHwnd;
            private readonly nint m_dragSourceOriginalExStyle;
            private nint m_hoverHwnd;
            private nint m_hoverOriginalExStyle;

            public DragSearchHost(DockingManager manager, Window dragSourceWindow)
            {
                m_manager = manager;
                m_dragSourceWindow = dragSourceWindow;

                // Layered-window style on the drag source manages its opacity dynamically.
                // Starts at full opacity; dims to kTearoffDragOpacity only when the cursor is
                // over a valid drop-target window so the user can see through the ghost.
                m_dragSourceHwnd = WinRT.Interop.WindowNative.GetWindowHandle(dragSourceWindow);
                m_dragSourceOriginalExStyle = User32WindowFuncs.GetWindowLongPtr(m_dragSourceHwnd, User32WindowFuncs.kGwlExStyle);
                User32WindowFuncs.SetWindowLongPtr(m_dragSourceHwnd, User32WindowFuncs.kGwlExStyle, m_dragSourceOriginalExStyle | User32WindowFuncs.kWsExLayered);
                User32WindowFuncs.SetLayeredWindowAttributes(m_dragSourceHwnd, 0, 255, User32WindowFuncs.kLwaAlpha);

                // Snapshot WindowManager activation order once. Index 0 = most recently activated
                // (frontmost); higher index = further back. Only windows behind the drag source
                // (higher index) are eligible for hover-bring-forward - windows already in front
                // need no assistance. If the drag source isn't tracked, all windows are eligible.
                this.WindowOrder = manager.m_windowManager.ToList();
                this.DragSourceOrderIndex = this.WindowOrder.IndexOf(dragSourceWindow);
                this.RecalculateFrontmostNonDragOrderIndex();
            }

            /// <summary>This drag's snapshot of the window order, frontmost first</summary>
            public List<Window> WindowOrder { get; }

            /// <summary>Where the drag source sits in <see cref="WindowOrder"/>, or -1 if the window manager doesn't track it</summary>
            public int DragSourceOrderIndex { get; }

            /// <summary>
            /// Where the frontmost docking window behind the drag source sits in <see cref="WindowOrder"/>. Hovering it
            /// never raises it, since it is already the topmost window the cursor can reach.
            /// </summary>
            public int FrontmostNonDragOrderIndex { get; private set; }

            public void ShowDropTarget(DockZone zone, bool isTarget) => zone.IsDirectDropTarget = isTarget;

            public void EngageWidget(DockDropInsertionDriverWidget widget, bool isEngaged) => widget.IsEngaged = isEngaged;

            public void DimDragSource(bool isDimmed)
            {
                byte alpha = isDimmed ? (byte)(255 * kTearoffDragOpacity) : (byte)255;
                User32WindowFuncs.SetLayeredWindowAttributes(m_dragSourceHwnd, 0, alpha, User32WindowFuncs.kLwaAlpha);
            }

            public void BeginHoverFlash(Window window)
            {
                m_hoverHwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
                m_hoverOriginalExStyle = User32WindowFuncs.GetWindowLongPtr(m_hoverHwnd, User32WindowFuncs.kGwlExStyle);
                User32WindowFuncs.SetWindowLongPtr(m_hoverHwnd, User32WindowFuncs.kGwlExStyle, m_hoverOriginalExStyle | User32WindowFuncs.kWsExLayered);
            }

            public void SetHoverFlashOpacity(Window window, double opacity)
            {
                User32WindowFuncs.SetLayeredWindowAttributes(m_hoverHwnd, 0, (byte)(255 * opacity), User32WindowFuncs.kLwaAlpha);
            }

            public void EndHoverFlash(Window window)
            {
                if (m_hoverHwnd == 0)
                {
                    return;
                }

                User32WindowFuncs.SetLayeredWindowAttributes(m_hoverHwnd, 0, 255, User32WindowFuncs.kLwaAlpha);
                User32WindowFuncs.SetWindowLongPtr(m_hoverHwnd, User32WindowFuncs.kGwlExStyle, m_hoverOriginalExStyle);
                m_hoverHwnd = 0;
            }

            public void RaiseHoverWindow(Window window)
            {
                window.AppWindow.MoveInZOrderAtTop();
                m_dragSourceWindow.AppWindow.MoveInZOrderAtTop(); // keep drag source above the raised window

                // The raised window moves to right behind the drag source in this run's order, which
                // makes it the frontmost non-drag window, so returning to it doesn't raise it again.
                this.WindowOrder.Remove(window);
                this.WindowOrder.Insert(this.DragSourceOrderIndex + 1, window);
                this.RecalculateFrontmostNonDragOrderIndex();
            }

            /// <summary>Puts the drag source's opacity and window style back the way they were before the drag</summary>
            public void RestoreDragSource()
            {
                User32WindowFuncs.SetLayeredWindowAttributes(m_dragSourceHwnd, 0, 255, User32WindowFuncs.kLwaAlpha);
                User32WindowFuncs.SetWindowLongPtr(m_dragSourceHwnd, User32WindowFuncs.kGwlExStyle, m_dragSourceOriginalExStyle);
            }

            private void RecalculateFrontmostNonDragOrderIndex()
            {
                this.FrontmostNonDragOrderIndex = m_manager.WindowsWithZones
                    .Where(w => w != m_dragSourceWindow)
                    .Select(w => this.WindowOrder.IndexOf(w))
                    .Where(i => i > this.DragSourceOrderIndex)
                    .DefaultIfEmpty(int.MaxValue)
                    .Min();
            }
        }

        // All user32.dll P/Invokes and their associated constants live here so
        // they are self-contained and not scattered throughout DockingManager.
        private static class User32WindowFuncs
        {
            internal const int kGwlExStyle = -20;
            internal const int kWsExLayered = 0x80000;
            internal const uint kLwaAlpha = 0x2;
            internal const int kVkLButton = 0x01;
            internal const int kVkEscape = 0x1B;

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
