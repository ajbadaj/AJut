namespace AJut.UX.Docking
{
    using AJut.Storage;
    using AJut.Tree;
    using AJut.UX.AttachedProperties;
    using AJut.UX.Controls;
    using System;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.ComponentModel;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Input;
    using System.Windows.Media;
    using System.Windows.Media.Imaging;
    using System.Windows.Threading;

    // ===========[ DockingManager ]==========================================
    // The registry, the display factory, tearoff bookkeeping and every close path live in
    // DockingManagerBase in AJut.UX, shared with the other toolkits. This adds the WPF parts:
    //   - System.Windows.Window tearoffs, placed by Left / Top / Width / Height
    //   - Window.Closing (cancelable) for tearoff close interception
    //   - The WindowManager that tracks tearoffs and routes commands back to their sources
    //   - RunDragSearch, the managed MenuItem and auto-save on zone size changes
    //   - WPF's own layout file (DockingSerialization), and where a hidden panel re-shows from
    /// <summary>
    /// The centralized manager for a single docking experience
    /// </summary>
    public class DockingManager : DockingManagerBase<Window, DockZone>
    {
        private readonly List<UIElement> m_commandSources = new List<UIElement>();
        private MenuItem m_managedMenuItem;

        // ===========[ Setup/Construction/Teardown ]===========================
        /// <summary>
        /// Construct a new <see cref="DockingManager"/> instance.
        /// </summary>
        /// <param name="rootWindow">The main window that the manager will track</param>
        /// <param name="uniqueId">The unique id (human readability is optional, it will form the default layout storage file path if none is specified, so it should also be file safe)</param>
        /// <param name="persistentStorageFilePath">The (optionally specified) path to the default state save for the docking experience in which this manager represents</param>
        /// <param name="autoSaveMethod">How should the manager auto save to the default state storage path as layout changes occur (default = <see cref="eDockingAutoSaveMethod.None"/>)</param>
        public DockingManager (Window rootWindow, string uniqueId, string persistentStorageFilePath = null, eDockingAutoSaveMethod autoSaveMethod = eDockingAutoSaveMethod.None)
            : base(rootWindow, uniqueId, persistentStorageFilePath, autoSaveMethod)
        {
            this.CreateNewTearoffWindowHandler = () => new DefaultDockTearoffWindow(this);
            this.ShowTearoffWindowHandler = w => w.Show();
            this.Windows = new WindowManager(rootWindow);
        }

        protected override void OnDisposing ()
        {
            // Drop the menu bar subs (if any) - they capture the MenuItem which lives on
            // the root window, so they outlive everything else if not unhooked.
            if (m_managedMenuItem != null)
            {
                this.UISyncVM.PanelStateChanged -= this.OnUISyncVMPanelStateChanged;
                ((INotifyCollectionChanged)this.UISyncVM.PanelTypeEntries).CollectionChanged -= this.OnUISyncVMPanelTypeEntriesChanged;

                // Each MenuItem RebuildMenuItems added has a Click handler whose target is this manager. Drop them now
                // or they pin this manager for as long as the menu stays alive.
                this.DetachAndRemoveManagedMenuItems(m_managedMenuItem);
                m_managedMenuItem = null;
            }

            // Without this Dispose call the window manager's subs to root window events
            // (StateChanged / Activated / Closed) keep RootWindow -> WindowManager ->
            // DockingManager pinned for the life of the application.
            this.Windows.Dispose();

            this.CreateNewTearoffWindowHandler = null;
            this.ShowTearoffWindowHandler = null;
            m_commandSources.Clear();
        }

        // Symmetric -= for every TrackSizingChanges. Walks every reachable zone - the root zone
        // plus every descendant - so split / tabbed hierarchies are covered.
        protected override void OnRootZoneDisposing (DockZone zone)
        {
            foreach (DockZone treeZone in TreeTraversal<DockZone>.All(zone))
            {
                if (treeZone != null)
                {
                    this.StopTrackingSizingChanges(treeZone);
                }
            }
        }

        // ===========[ Properties ]============================================
        /// <summary>
        /// The window tracking for all tearoff and associated docking windows
        /// </summary>
        public WindowManager Windows { get; }

        /// <summary>
        /// An (optional) method for specifying creation of a tearoff window (default is null which will create a <see cref="DefaultDockTearoffWindow"/>)
        /// </summary>
        public Func<Window> CreateNewTearoffWindowHandler { private get; set; }

        /// <summary>
        /// An (optional) method for specifying how a tearoff window is shown (default is null which will result in <see cref="Window.Show"/>)
        /// </summary>
        public Action<Window> ShowTearoffWindowHandler { get; set; }

        // ===========[ Menu Management ]=======================================
        /// <summary>
        /// Populate a <see cref="MenuItem"/> with toggle entries for each registered
        /// panel type. Checked items indicate visible panels; clicking a toggle item
        /// shows/hides the panel.
        /// </summary>
        public void ManageMenu (MenuItem menuItem)
        {
            // Drop prior subs if the caller hands us a different menu so the old one isn't
            // held captive and we don't accumulate stale handlers.
            if (m_managedMenuItem != null && m_managedMenuItem != menuItem)
            {
                this.UISyncVM.PanelStateChanged -= this.OnUISyncVMPanelStateChanged;
                ((INotifyCollectionChanged)this.UISyncVM.PanelTypeEntries).CollectionChanged -= this.OnUISyncVMPanelTypeEntriesChanged;
                this.DetachAndRemoveManagedMenuItems(m_managedMenuItem);
            }

            m_managedMenuItem = menuItem;
            this.RebuildMenuItems(menuItem);

            // Defensive -= before += so re-entry into ManageMenu cannot double-subscribe.
            this.UISyncVM.PanelStateChanged -= this.OnUISyncVMPanelStateChanged;
            this.UISyncVM.PanelStateChanged += this.OnUISyncVMPanelStateChanged;

            var entries = (INotifyCollectionChanged)this.UISyncVM.PanelTypeEntries;
            entries.CollectionChanged -= this.OnUISyncVMPanelTypeEntriesChanged;
            entries.CollectionChanged += this.OnUISyncVMPanelTypeEntriesChanged;
        }

        private void OnUISyncVMPanelStateChanged (object sender, DockPanelAddRemoveUISync.PanelStateChangedEventArgs e)
        {
            if (m_managedMenuItem == null)
            {
                return;
            }

            if (e.IsStructuralChange)
            {
                this.RebuildMenuItems(m_managedMenuItem);
            }
            else
            {
                this.UpdateMenuCheckedStates(m_managedMenuItem);
            }
        }

        private void OnUISyncVMPanelTypeEntriesChanged (object sender, NotifyCollectionChangedEventArgs e)
        {
            if (m_managedMenuItem != null)
            {
                this.RebuildMenuItems(m_managedMenuItem);
            }
        }

        // Drops every menu item that RebuildMenuItems added, unsubscribing each Click handler before removing the item.
        // Without the -= step, a removed item still holds a strong ref back to this manager through its handler, for as
        // long as anything holds the item. Used by Dispose, ManageMenu (when handed a different menu), and
        // RebuildMenuItems (which wipes before rebuilding).
        private void DetachAndRemoveManagedMenuItems (MenuItem menuItem)
        {
            for (int i = menuItem.Items.Count - 1; i >= 0; --i)
            {
                if (menuItem.Items[i] is MenuItem item)
                {
                    item.Click -= this.OnManagedMenuItemClicked;
                }

                menuItem.Items.RemoveAt(i);
            }
        }

        private void RebuildMenuItems (MenuItem menuItem)
        {
            this.DetachAndRemoveManagedMenuItems(menuItem);
            foreach (var desc in this.UISyncVM.GenerateMenuDescriptors())
            {
                var item = new MenuItem
                {
                    Header = desc.IsToggle ? desc.DisplayName : $"Add {desc.DisplayName}",
                    Tag = desc.PanelType,
                    IsCheckable = desc.IsToggle,
                    IsChecked = desc.IsChecked,
                };

                item.Click += this.OnManagedMenuItemClicked;
                menuItem.Items.Add(item);
            }
        }

        private void UpdateMenuCheckedStates (MenuItem menuItem)
        {
            foreach (var item in menuItem.Items)
            {
                if (item is MenuItem mi && mi.Tag is Type panelType)
                {
                    DockPanelTypeEntry entry = this.UISyncVM.FindEntry(panelType);
                    if (entry != null)
                    {
                        mi.IsChecked = entry.HasActiveInstance;
                    }
                }
            }
        }

        private void OnManagedMenuItemClicked (object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem mi && mi.Tag is Type panelType)
            {
                DockPanelTypeEntry entry = this.UISyncVM.FindEntry(panelType);
                if (entry != null && entry.IsSingleInstance)
                {
                    this.UISyncVM.RequestSetToggleState(panelType);
                }
                else
                {
                    this.UISyncVM.RequestAdd(panelType);
                }
            }
        }

        // ===========[ Public Interface Methods ]==============================
        /// <summary>
        /// Register root <see cref="DockZone"/>(s) that are fixed dock locations. Each DockingManager requires at least one root level zone.
        /// </summary>
        public void RegisterRootDockZones (params DockZone[] dockZones)
        {
            this.RegisterRootDockZones(this.RootWindow, dockZones);
        }

        /// <summary>
        /// Register root <see cref="DockZone"/>(s) that are fixed dock locations. Each DockingManager requires at least one root level zone.
        /// </summary>
        public void RegisterRootDockZones (Window window, params DockZone[] dockZones)
        {
            foreach (var zone in dockZones)
            {
                if (DockZone.GetGroupId(zone).IsNullOrEmpty() && zone.Name.IsNotNullOrEmpty())
                {
                    DockZone.SetGroupId(zone, zone.Name);
                }

                if (zone.IsSetup && zone.Manager != this)
                {
                    zone.Manager?.DeRegisterRootDockZones(zone);
                }

                this.RecordRootZone(window, zone);
                zone.Manager = this;
                if (zone.ViewModel == null)
                {
                    zone.ViewModel = new DockZoneViewModel(this);
                }
            }
        }

        /// <summary>
        /// Drags the given window around alpha'd out, and as the cursor moves highlights drop opportunities, executing
        /// the drop if the user releases.
        /// </summary>
        /// <param name="dragSourceWindow">The window which will be dragged</param>
        /// <param name="sourceDockZone">The dock zone on that window which will be dragged</param>
        public Task RunDragSearch (Window dragSourceWindow, DockZone sourceDockZone)
            => this.RunDragSearch(dragSourceWindow, sourceDockZone, screenCursorOffset: null, origin: null);

        // screenCursorOffset: where the pointer sits within the dragged window, which it keeps when the drag ends. Null leaves
        //  the window's top-left corner on the pointer.
        // origin: where what the drag tore off came from, which a canceled drag (Escape) puts it back to. Null when the drag
        //  moves a tearoff that already existed, where Escape only stops the drag.
        internal async Task RunDragSearch (Window dragSourceWindow, DockZone sourceDockZone, DockPoint? screenCursorOffset, DockDragOrigin origin)
        {
            const double kMouseOffset = 20.0;

            // WinUI3's drag polls every 16ms, and the session counts its timing (how long the pointer rests on a window) in
            //  those ticks
            const int kTickMilliseconds = 16;

            // The drag's other windows, frontmost first: the window manager keeps them in activation order. Snapshot once, so
            //  a raise during the drag (which activates nothing) can move a window up in it.
            List<Window> windowOrder = this.Windows.Where(w => w != dragSourceWindow).ToList();

            // The shared session decides which zone's overlay shows, which widget is engaged, when the ghost dims, and when
            //  a window the pointer rests on flashes and is raised; the host does each of those on the WPF windows and controls
            var host = new DragSearchHost(windowOrder);
            var session = new DockDragSession<DockZone, DockDropInsertionDriverWidget, Window>(host);

            // The ghost follows the mouse through AsyncDragMove, which moves it as mouse moves arrive. The session wants one
            //  update per tick whether the mouse moves or not, so a timer at WinUI3's poll rate hit tests under the pointer
            //  and hands that over, and watches for Escape, which cancels the move.
            var escape = new CancellationTokenSource();
            var tickTimer = new DispatcherTimer(DispatcherPriority.Normal) { Interval = TimeSpan.FromMilliseconds(kTickMilliseconds) };
            Window draggerWindow = null;
            try
            {
                this.IsZoneDragDropUnderway = true;

                // The ghost starts at full opacity and dims over a drop target. It stays topmost, above any window the drag
                //  raises.
                draggerWindow = new Window();
                draggerWindow.WindowStyle = WindowStyle.None;
                draggerWindow.AllowsTransparency = true;
                draggerWindow.Opacity = 1.0;
                draggerWindow.Topmost = true;
                draggerWindow.Style = null;
                host.Ghost = draggerWindow;

                Point mouseLoc = Mouse.GetPosition(dragSourceWindow);
                mouseLoc.X += dragSourceWindow.Left;
                mouseLoc.Y += dragSourceWindow.Top;
                draggerWindow.Left = mouseLoc.X + kMouseOffset;
                draggerWindow.Top = mouseLoc.Y + kMouseOffset;
                draggerWindow.Width = dragSourceWindow.ActualWidth;
                draggerWindow.Height = dragSourceWindow.ActualHeight;

                var img = new Image();
                // We'll need to wait for render to happen because we may be tearing these things off,
                //  and shoving them into windows, and then running this before that window has time
                //  to even render for the first time.
                await dragSourceWindow.Dispatcher.InvokeAsync(() =>
                {
                    using (var stream = dragSourceWindow.RenderToPng())
                    {
                        var imgSource = new BitmapImage();
                        imgSource.BeginInit();
                        imgSource.StreamSource = stream;
                        imgSource.CacheOption = BitmapCacheOption.OnLoad;
                        imgSource.EndInit();
                        img.Source = imgSource;
                    }
                }, System.Windows.Threading.DispatcherPriority.Render);

                draggerWindow.Content = img;
                draggerWindow.Show();
                try
                {
                    dragSourceWindow.Visibility = Visibility.Collapsed;
                    tickTimer.Tick += _OnDragTick;
                    tickTimer.Start();
                    await draggerWindow.AsyncDragMove(escape.Token, onMove: _OnGhostMoved);
                }
                catch (OperationCanceledException)
                {
                    session.Cancel();
                }
                finally
                {
                    tickTimer.Stop();
                    tickTimer.Tick -= _OnDragTick;
                    draggerWindow.Close();

                    // A drag canceled with somewhere to go back to shows nothing more of the tearoff it made
                    if (!session.IsCanceled || (origin == null))
                    {
                        _ShowSourceWhereTheGhostIs();
                    }
                }

                // Canceled: what the drag tore off is dropped back where it came from, which closes the tearoff the drag
                //  made, then gets its tab position, selection and split sizes back. With nowhere to go back to, the
                //  tearoff stays where the drag left it.
                if (session.IsCanceled)
                {
                    if ((origin != null) && !this.ReturnCanceledDrag(origin, sourceDockZone.ViewModel, dragSourceWindow))
                    {
                        _ShowSourceWhereTheGhostIs();
                    }

                    return;
                }

                DockDropInsertionDriverWidget engagedWidget = session.EngagedWidget;
                if (engagedWidget != null)
                {
                    // Ensure no bugs will inadvertently cause a break
                    if (engagedWidget.InsertionZone?.ViewModel != null)
                    {
                        // Do the drop
                        engagedWidget.InsertionZone.ViewModel.DropAddSiblingIntoDock(sourceDockZone.ViewModel, engagedWidget.Direction);

                        // If it's a docking tearoff window and this was the last thing, close it. Unlike the shared
                        //  drop commit, which always closes the drag source, a source tearoff holding more than one
                        //  zone stays open here (AJU-103).
                        if (DockWindowConfig.GetIsDockingTearoffWindow(dragSourceWindow))
                        {
                            int numZones = dragSourceWindow.GetVisualChildren().OfType<DockZone>().Count();
                            if ((numZones == 0) || (numZones == 1))
                            {
                                this.SilentlyForceCloseTearoffWindow(dragSourceWindow);
                            }
                        }
                    }
                }
            }
            finally
            {
                this.IsZoneDragDropUnderway = false;

                // Disengages the widget and hides the overlay
                session.End();
                escape.Dispose();
            }

            // The ghost sits kMouseOffset down and right of the pointer, so the dragged window goes where the ghost puts the
            //  pointer, at its cursor offset
            void _ShowSourceWhereTheGhostIs ()
            {
                dragSourceWindow.Left = draggerWindow.Left - kMouseOffset - (screenCursorOffset?.X ?? 0.0);
                dragSourceWindow.Top = draggerWindow.Top - kMouseOffset - (screenCursorOffset?.Y ?? 0.0);
                dragSourceWindow.Visibility = Visibility.Visible;
            }

            // The ticks do the hit testing, so a mouse move has nothing more to do once the ghost has moved
            static void _OnGhostMoved () { }

            void _OnDragTick (object sender, EventArgs e)
            {
                if (Keyboard.IsKeyDown(Key.Escape))
                {
                    escape.Cancel();
                    return;
                }

                _HitTestUnderThePointer();
            }

            // Hit tests the other windows' zones at the pointer, frontmost window first: the first zone found is the drop
            //  target, and the insertion widget under the pointer in it (if it is enabled) is the one a drop would use. Also
            //  finds the window the pointer rests on, for raising.
            void _HitTestUnderThePointer ()
            {
                DockZone hitZone = null;
                DockDropInsertionDriverWidget hitWidget = null;
                foreach (DockZone rootZone in windowOrder.SelectMany(w => this.TryGetZonesForWindow(w, out IReadOnlyList<DockZone> zones) ? zones : Enumerable.Empty<DockZone>()))
                {
                    var result = VisualTreeHelper.HitTest(rootZone, Mouse.PrimaryDevice.GetPosition(rootZone));
                    if (result?.VisualHit == null)
                    {
                        continue;
                    }

                    hitZone = result.VisualHit as DockZone ?? result.VisualHit.GetFirstParentOf<DockZone>();
                    if (hitZone != null)
                    {
                        var widget = result.VisualHit as DockDropInsertionDriverWidget ?? result.VisualHit.GetFirstParentOf<DockDropInsertionDriverWidget>();
                        hitWidget = (widget?.IsEnabled == true) ? widget : null;
                        break;
                    }
                }

                session.Update(hitZone, hitWidget, _FindWindowToRaise());
            }

            // The frontmost window under the pointer, when it isn't already the frontmost window (resting on that one needs
            //  no raise), as WinUI3's drag picks it. The root window is left out when it owns the tearoffs, since an owner
            //  always sits behind the windows it owns, so raising it does nothing.
            Window _FindWindowToRaise ()
            {
                var pointer = new Point(draggerWindow.Left - kMouseOffset, draggerWindow.Top - kMouseOffset);
                Window frontmost = null;
                foreach (Window window in windowOrder)
                {
                    if (!window.IsVisible
                        || ((window == this.RootWindow) && this.Windows.SetOwnerAsRootForEachChildWindow))
                    {
                        continue;
                    }

                    frontmost ??= window;
                    var bounds = new Rect(window.Left, window.Top, window.ActualWidth, window.ActualHeight);
                    if (bounds.Contains(pointer))
                    {
                        return window == frontmost ? null : window;
                    }
                }

                return null;
            }
        }

        /// <summary>
        /// Performs a tearoff for only the given display element (not the rest of the elements in the zone ui) by pulling
        /// it out of the UI it's embedded in, and creating + placing it in a new UI for it in a tearoff window that matches
        /// in size to the current zone ui.
        /// </summary>
        public Result<Window> DoTearoff (IDockableDisplayElement element, Point newWindowOrigin)
        {
            DockZoneSize dockSize = element.DockingAdapter.Location.UI?.RenderSize ?? DockZoneSize.Empty;
            return this.DoTearoff(element, newWindowOrigin, new Size(dockSize.Width, dockSize.Height));
        }

        /// <summary>
        /// Performs a tearoff for only the given display element (not the rest of the elements in the zone ui) by pulling
        /// it out of the UI it's embedded in, and creating + placing it in a new UI for it in a tearoff window that is set
        /// to whatever size you specify.
        /// </summary>
        public Result<Window> DoTearoff (IDockableDisplayElement element, Point newWindowOrigin, Size newWindowSize)
            => this.DoTearoffCore(element, new DockWindowBounds(newWindowOrigin.X, newWindowOrigin.Y, newWindowSize.Width, newWindowSize.Height));

        /// <summary>
        /// Tears off a <see cref="DockZoneViewModel"/>, which means itself, all it's docked content, and/or child zones - and places
        /// that into a new tearoff window, collapsing the location it was pulled from. New window is set to have the same size as the
        /// zone ui that it is made from.
        /// </summary>
        /// <param name="sourceZone">What zone</param>
        /// <param name="newWindowOrigin">Where to place the new window</param>
        /// <returns>The result containing the window that was created and stocked</returns>
        public Result<Window> DoGroupTearoff (DockZoneViewModel sourceZone, Point newWindowOrigin)
        {
            return this.DoGroupTearoff(sourceZone, newWindowOrigin, new Size(sourceZone.UI.RenderSize.Width, sourceZone.UI.RenderSize.Height));
        }

        /// <summary>
        /// Tears off a <see cref="DockZoneViewModel"/>, which means itself, all it's docked content, and/or child zones - and places
        /// that into a new tearoff window, collapsing the location it was pulled from. New window has whatever size you specify.
        /// </summary>
        /// <param name="sourceZone">What zone</param>
        /// <param name="newWindowOrigin">Where to place the new window</param>
        /// <param name="newTearoffWindowSize">The tearoff window's size</param>
        /// <returns>The result containing the window that was created and stocked</returns>
        public Result<Window> DoGroupTearoff (DockZoneViewModel sourceZone, Point newWindowOrigin, Size newTearoffWindowSize)
            => this.DoGroupTearoffCore(sourceZone, new DockWindowBounds(newWindowOrigin.X, newWindowOrigin.Y, newTearoffWindowSize.Width, newTearoffWindowSize.Height));

        /// <summary>
        /// Captures where a panel was hidden from, so it can be re-shown there. A panel hidden from the root window re-shows
        /// in a tearoff of its zone's size at the root window's position.
        /// </summary>
        public override HiddenPanelPlatformState CaptureHideState (DockingContentAdapterModel adapter)
        {
            // The shared version keeps nothing for a panel hidden from the root window, so it re-shows in a default sized
            //  tearoff at the root window's position. WPF keeps the size of the zone it was hidden from. Where a hidden
            //  panel should come back, ideally the spot it was hidden from, is still open (AJU-96), so WPF keeps its own
            //  until then.
            DockZone dockZoneUI = adapter.Location?.UI as DockZone;
            Window hostWindow = dockZoneUI != null ? Window.GetWindow(dockZoneUI) : null;
            bool wasInTearoff = hostWindow != null && DockWindowConfig.GetIsDockingTearoffWindow(hostWindow);

            if (!wasInTearoff)
            {
                return new HiddenPanelPlatformState
                {
                    WasInTearoff = true,
                    NextDisplayLocationX = -1,
                    NextDisplayLocationY = -1,
                    NextDisplayWidth = dockZoneUI.ActualWidth,
                    NextDisplayHeight = dockZoneUI.ActualHeight,
                };
            }

            return new HiddenPanelPlatformState
            {
                WasInTearoff = true,
                NextDisplayLocationX = hostWindow.Left,
                NextDisplayLocationY = hostWindow.Top,
                NextDisplayWidth = hostWindow.Width,
                NextDisplayHeight = hostWindow.Height,
                TearoffWindowRef = hostWindow,
            };
        }

        /// <summary>
        /// Adds an element whose command bindings every tearoff window made from here on gets too, so a command raised in a
        /// tearoff still reaches it
        /// </summary>
        public void AddCommandSource (UIElement source)
        {
            if (source != null)
            {
                m_commandSources.Add(source);
            }
        }

        // ===========[ DockingManagerBase Implementations ]====================
        protected override DockZoneViewModel GetZoneViewModel (DockZone zone) => zone.ViewModel;

        protected override string GetZoneGroupId (DockZone zone) => DockZone.GetGroupId(zone);

        protected override IEnumerable<DockZone> EnumerateZoneTree (DockZone rootZone) => TreeTraversal<DockZone>.All(rootZone);

        // Window placement on WPF is in device independent pixels, so creation takes the same bounds
        protected override DockWindowBounds GetWindowBounds (Window window)
            => new DockWindowBounds(window.Left, window.Top, window.ActualWidth, window.ActualHeight);

        protected override bool IsWindowTracked (Window window) => this.Windows.Contains(window);

        protected override void StopTrackingWindow (Window window) => this.Windows.StopTracking(window);

        protected override void CloseWindow (Window window) => window.Close();

        // Gives the window focus. Tearoffs are owned by the root window by default, which keeps the root behind them
        //  whatever its z-order, so focus is what a tearoff closing hands back to it.
        protected override void ActivateWindow (Window window) => window.Focus();

        protected override bool TryCaptureWindowLayoutState (Window window, out DockWindowLayoutState state)
        {
            state = new DockWindowLayoutState(this.GetWindowBounds(window), window.WindowState == WindowState.Maximized, WindowXTA.GetIsFullscreen(window));
            return true;
        }

        protected override void ApplyWindowLayoutState (Window window, DockWindowLayoutState state)
        {
            if (state.IsFullscreen)
            {
                WindowXTA.SetIsFullscreen(window, true);
            }
            else
            {
                window.WindowState = WindowState.Maximized;
            }
        }

        protected override Result<Window> CreateAndStockTearoffWindow (DockZoneViewModel rootZone, DockWindowBounds bounds)
        {
            Window window = null;
            try
            {
                window = this.CreateNewTearoffWindowHandler();
                window.CommandBindings.AddEach(this.EnumerateAllCommandBindings());
                DockWindowConfig.SetDockingTearoffWindowRootZone(window, rootZone);
                this.Windows.Track(window);

                DockZone newRoot = new DockZone() { ViewModel = rootZone };
                window.Content = newRoot;
                window.WindowStartupLocation = WindowStartupLocation.Manual;
                window.Left = bounds.X;
                window.Top = bounds.Y;
                window.Width = bounds.Width;
                window.Height = bounds.Height;
                window.Closing += this.OnDockTearoffWindowClosing;
                this.RecordTearoffRootZone(window, rootZone);
                this.ShowTearoffWindowHandler(window);
                this.RegisterRootDockZones(window, newRoot);

                rootZone.ClearPassAlongUISize();
                return Result<Window>.Success(window);
            }
            catch (Exception exc)
            {
                // Make sure a stray window isn't left open. Its closing handler goes first: the window was never fully
                //  set up as a tearoff, so the user close path would refuse it and cancel the close.
                if (window != null)
                {
                    this.DetachTearoffSubscriptions(window);
                    this.Windows.StopTracking(window);
                    window.Close();
                }

                Logger.LogError("Window tearoff failed!", exc);
                return Result<Window>.Error("DockingManager: Window tearoff failed for unknown reason");
            }
        }

        // Drops the Window.Closing sub DockingManager owns on a tearoff. Without this, the
        // window's invocation list keeps a strong ref to DockingManager until the window
        // object itself is collected - which is non-deterministic and pins everything for
        // the duration. The base calls this just before window.Close() on every tearoff path.
        protected override void DetachTearoffSubscriptions (Window window)
        {
            if (window != null)
            {
                window.Closing -= this.OnDockTearoffWindowClosing;
            }
        }

        protected override void OnRootZoneDeregistered (DockZone zone)
        {
            zone.Manager = null;
        }

        // Asks every panel rather than stopping at the first refusal, so each one that refuses can say so in its zone
        protected override bool CheckPanelsCanCloseForCloseAll (IReadOnlyList<DockingContentAdapterModel> panels)
        {
            bool anyDissenters = false;
            foreach (DockingContentAdapterModel adapter in panels)
            {
                if (!adapter.CheckCanClose())
                {
                    var closeSupression = new RoutedEventArgs(DockZone.NotifyCloseSupressionEvent);
                    ((DockZone)adapter.Location.UI).RaiseEvent(closeSupression);
                    anyDissenters = true;
                }
            }

            return !anyDissenters;
        }

        // WPF still reads its own layout file shape, which only differs from the shared one in its tearoff entries.
        //  Moving it onto the shared, versioned format waits on layout files with tearoffs from both showrooms to
        //  test against (AJU-99).
        protected override bool ResetLayoutFromFile (string filePath)
        {
            Logger.LogInfo($"Docking: Loading state for file '{filePath ?? "<null>"}'");
            bool result = this.ResetLayoutFromWpfLayoutFile(filePath);
            Logger.LogInfo($"Docking: Loading state for file '{filePath ?? "<null>"}' complete.");
            return result;
        }

        protected override bool SaveLayoutToFile (string filePath)
        {
            var state = new DockingSerialization.SerializationInfo
            {
                Core = this.BuildWpfCoreLayout(),
                Ancillary = this.BuildWpfTearoffLayouts().ToArray(),
            };

            return DockingSerialization.WriteFile(filePath, state);
        }

        // ===========[ Internal Helpers ]======================================
        // Places a new tearoff for something of zoneSize pressed at pressPoint (in pressedElement's own coordinates), by the
        //  shared placement rules: at least the minimum tearoff size, with the pointer centered across it near its top.
        //
        //  WPF places windows in device independent pixels (DIPs), but PointToScreen gives device pixels, and on a scaled
        //  display the two differ by the display's scale. So the press point goes through the presentation source's
        //  TransformFromDevice on its way in, and the placement works in DIPs throughout (a display scale of 1).
        internal static DockTearoffPlacement PlaceTearoffFor (DockZoneSize zoneSize, UIElement pressedElement, Point pressPoint)
        {
            Point anchor = pressedElement.PointToScreen(pressPoint);
            PresentationSource source = PresentationSource.FromVisual(pressedElement);
            if (source?.CompositionTarget != null)
            {
                anchor = source.CompositionTarget.TransformFromDevice.Transform(anchor);
            }

            return DockDragRules.PlaceTearoff(zoneSize, new DockPoint(anchor.X, anchor.Y), cursorOffset: null, displayScale: 1.0);
        }

        internal void TrackSizingChanges (DockZone dockZone)
        {
            dockZone.SizeChanged -= this.DockZone_OnSizeChanged;
            dockZone.SizeChanged += this.DockZone_OnSizeChanged;
        }

        internal void StopTrackingSizingChanges (DockZone dockZone)
        {
            dockZone.SizeChanged -= this.DockZone_OnSizeChanged;
        }

        // ===========[ Private Helpers ]=======================================
        private void OnDockTearoffWindowClosing (object sender, CancelEventArgs e)
        {
            if (!this.DoTearoffWindowClose((Window)sender))
            {
                e.Cancel = true;
            }
        }

        private void DockZone_OnSizeChanged (object sender, SizeChangedEventArgs e)
        {
            this.TriggerLayoutAutoSave();
        }

        private IEnumerable<CommandBinding> EnumerateAllCommandBindings ()
        {
            foreach (UIElement source in m_commandSources)
            {
                foreach (CommandBinding cmd in source.CommandBindings.OfType<CommandBinding>().Where(c => c != null))
                {
                    yield return cmd;
                }
            }
        }

        private bool ResetLayoutFromWpfLayoutFile (string filePath)
        {
            DockZoneViewModel defaultLoadZone = this.GetFallbackRootZone();
            if (defaultLoadZone == null)
            {
                Logger.LogError("Dock Loading Error: Load from state failed as NO ROOT ZONES EXIST for provided manager");
                return false;
            }

            DockingSerialization.SerializationInfo state = DockingSerialization.ReadFile(filePath);
            if (state == null)
            {
                return false;
            }

            try
            {
                this.ClearForLoadingFromState();
                if (state.Core != null)
                {
                    foreach (string groupId in state.Core.ZoneInfoByRoot.Keys)
                    {
                        DockZoneViewModel zone = this.GetRootZoneForGroup(groupId);
                        if (zone == null)
                        {
                            Logger.LogError($"Dock Loading Error: No zone for group id '{groupId}' found, possibly one of that name used to be registered but now is not - falling back to default to avoid data loss.");
                            zone = defaultLoadZone;
                        }

                        this.ApplyZoneLayout(zone, state.Core.ZoneInfoByRoot[groupId]);
                    }

                    foreach (DockingSerialization.WindowStorageData ancillaryWindowData in state.Ancillary)
                    {
                        var zone = new DockZoneViewModel(this);
                        this.ApplyZoneLayout(zone, ancillaryWindowData.State);

                        var bounds = new DockWindowBounds(ancillaryWindowData.WindowLocation.X, ancillaryWindowData.WindowLocation.Y, ancillaryWindowData.WindowSize.Width, ancillaryWindowData.WindowSize.Height);
                        Result<Window> windowResult = this.CreateAndStockTearoffWindow(zone, bounds);
                        if (windowResult.HasErrors)
                        {
                            Logger.LogError("Dock Loading Error: Failed to intepret ancillary window state for docking");
                            Logger.LogError(windowResult.GetErrorReport());
                            continue;
                        }

                        if (ancillaryWindowData.WindowIsFullscreened)
                        {
                            WindowXTA.SetIsFullscreen(windowResult.Value, true);
                        }
                        else
                        {
                            windowResult.Value.WindowState = ancillaryWindowData.WindowState;
                        }
                    }
                }

                this.CleanZoneLayoutHierarchies();
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError("Error in docking serialization", ex);
                return false;
            }
        }

        private DockingSerialization.CoreStorageData BuildWpfCoreLayout ()
        {
            // Core window info:
            //  - Split by root zones w/ zone ids
            //  - normal serialization window
            var storage = new DockingSerialization.CoreStorageData();
            if (this.TryGetZonesForWindow(this.RootWindow, out IReadOnlyList<DockZone> rootZones))
            {
                foreach (DockZone rootZone in rootZones)
                {
                    string groupId = DockZone.GetGroupId(rootZone) ?? String.Empty;
                    if (!storage.ZoneInfoByRoot.TryAdd(groupId, rootZone.ViewModel.GenerateSerializationState()))
                    {
                        Logger.LogError($"Docking Serialization: Failed to build serialization data properly, as two ore more root zone groups share id of '{groupId}' - zone serialization info was SKIPPED");
                    }
                }
            }

            return storage;
        }

        private IEnumerable<DockingSerialization.WindowStorageData> BuildWpfTearoffLayouts ()
        {
            // For each docking tearoff window...
            //  - window location and display info (state, size, etc)
            //  - single root zone tree info
            foreach (Window window in this.WindowsWithZones.ToList())
            {
                if (window == this.RootWindow
                    || !this.TryGetZonesForWindow(window, out IReadOnlyList<DockZone> zones)
                    || zones.Count == 0)
                {
                    continue;
                }

                yield return new DockingSerialization.WindowStorageData
                {
                    WindowSize = window.RenderSize,
                    WindowState = window.WindowState,
                    WindowLocation = new Point(window.Left, window.Top),
                    WindowIsFullscreened = WindowXTA.GetIsFullscreen(window),
                    State = zones[0].ViewModel.GenerateSerializationState()
                };
            }
        }

        // ===========[ Subclasses/structs ]====================================
        // What RunDragSearch's DockDragSession asks of WPF. A drop target and an engaged widget show through the zone's and
        //  widget's own properties. The dragged window is hidden during the drag and a ghost of it follows the pointer, so
        //  the ghost is what dims over a drop target.
        private sealed class DragSearchHost : IDockDragSessionHost<DockZone, DockDropInsertionDriverWidget, Window>
        {
            // The ghost's opacity over a drop target, so the zone shows through it: the opacity WinUI3 dims a dragged
            //  window to
            private const double kDimmedGhostOpacity = 0.65;

            private readonly List<Window> m_windowOrder;

            // windowOrder: the drag's other windows, frontmost first, which a raise moves the raised window to the front of
            public DragSearchHost (List<Window> windowOrder)
            {
                m_windowOrder = windowOrder;
            }

            // The ghost following the pointer, once it exists
            public Window Ghost { get; set; }

            public void ShowDropTarget (DockZone zone, bool isTarget) => zone.IsDirectDropTarget = isTarget;

            public void EngageWidget (DockDropInsertionDriverWidget widget, bool isEngaged) => widget.IsEngaged = isEngaged;

            public void DimDragSource (bool isDimmed)
            {
                if (this.Ghost != null)
                {
                    this.Ghost.Opacity = isDimmed ? kDimmedGhostOpacity : 1.0;
                }
            }

            // A WPF window's own Opacity only works on a window made with AllowsTransparency, which a tearoff or the root
            //  window needn't be, so a flash fades the window's content instead, and needs nothing set up first
            public void BeginHoverFlash (Window window) { }

            public void SetHoverFlashOpacity (Window window, double opacity)
            {
                if (window.Content is UIElement content)
                {
                    content.Opacity = opacity;
                }
            }

            public void EndHoverFlash (Window window) => this.SetHoverFlashOpacity(window, 1.0);

            public void RaiseHoverWindow (Window window)
            {
                // Topmost on then off brings a window to the top of the ordinary windows without activating it, which would
                //  take keyboard focus (and so Escape) from the ghost. The ghost is topmost for the whole drag, so it stays
                //  above the raised window.
                bool wasTopmost = window.Topmost;
                window.Topmost = true;
                window.Topmost = wasTopmost;

                // Now in front, so not a window to raise again straight away
                m_windowOrder.Remove(window);
                m_windowOrder.Insert(0, window);
            }
        }
    }
}
