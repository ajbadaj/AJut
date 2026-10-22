namespace AJut.UX.Docking
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Diagnostics;
    using AJut.Storage;
    using AJut.UX.Controls;
    using AppKit;
    using CoreGraphics;
    using Foundation;

    // ===========[ DockingManager ]=============================================
    // The AppKit docking manager. Everything that never touches a window (the display factory,
    // the root zone registry, tearoff bookkeeping and every tearoff close path, hidden panel
    // state, the layout file and its load and save) is AJut.UX's DockingManagerBase, shared with
    // WinUI3. This supplies what is AppKit's: tearoff NSWindows, where a window is, closing and
    // raising one, and the DockZoneView tree.
    //
    // Two kinds of bounds pass through the base:
    //  - Placement bounds say where a whole window is. GetWindowBounds gives them, layout files
    //    keep them, and a hidden panel's tearoff state holds them. They are in the layout file's
    //    units, which are WinUI3's: physical pixels, with a top-left origin measured down from the
    //    top of the primary screen. So a layout file is one format on both stacks.
    //  - Creation bounds are what CreateAndStockTearoffWindow takes: a top-left position and the
    //    zone's content size, in points, the units a zone's render size is in.
    // ToTearoffCreationBounds turns placement into creation: down by the primary screen's backing
    // scale, and from the whole frame's size to the content's. The scale is the primary screen's
    // both ways, so a layout saved and loaded on the same Mac lands where it was, and a tearoff
    // never grows by the scale across a save and load or a hide and show.
    //
    // AppKit's own window frames are bottom-left, measured up from the bottom of the primary
    // screen, so ToTopLeftPoints and ToFrame flip between the two.
    //
    // Tearoff windows are ThemedWindows this manager makes and owns, with the zone below AJut's
    // title strip. A ThemedWindow is its own delegate, so the manager hears a tearoff's close
    // button through its Closing event, which is where a panel's close veto runs, and takes a drag
    // on its title strip through TitleStripDragging, so dragging a tearoff can dock it. The root
    // window is the app's, and its delegate is left alone: the manager hears it close through
    // NSWindow's WillClose notification instead.
    //
    // Dragging (DragPanel, DragFromHeader and RunDragSearch) runs as an AppKit event-tracking loop, the documented way
    // to follow a drag: from the mouse down or drag that starts it, the loop takes the mouse and key
    // events itself until the button comes up, so nothing polls. The rules (thresholds, placement,
    // the drop overlay, Escape and the way back) are AJut.UX's, shared with WinUI3.
    //
    // Raising the tearoffs when the app comes back to the front needs nothing here, unlike on
    // WinUI3: activating a Mac app brings all of its windows forward.
    public class DockingManager : DockingManagerBase<NSWindow, DockZoneView>
    {
        // The height of a tearoff window's title strip, above its zone, which creation adds to the zone's height
        private const double kTearoffTitleBarHeight = 32.0;

        // A dragged tearoff fades to this over a drop target, so the zone under it shows through
        private const double kTearoffDragOpacity = 0.65;

        // How often the drag loop checks under the pointer while nothing moves, and the rate the shared drag
        // session's tick counts assume (WinUI3's 16ms poll)
        private const double kDragTickSeconds = 0.016;

        // The Escape key's virtual key code (kVK_Escape)
        private const ushort kEscapeKeyCode = 53;

        private readonly List<NSWindow> m_tearoffWindows = new List<NSWindow>();
        private NSObject m_rootWindowWillCloseObserver;

        // ===========[ Setup/Construction/Teardown ]===========================
        /// <summary>
        /// Sets up docking for <paramref name="rootWindow"/>. With no <paramref name="persistentStorageFilePath"/>, the
        /// layout is kept under the app data root, in Layouts, named for <paramref name="uniqueId"/>: run
        /// <see cref="ApplicationUtilities.RunOnetimeSetup"/> first, since that is what sets the root.
        /// </summary>
        public DockingManager (NSWindow rootWindow, string uniqueId, string persistentStorageFilePath = null, eDockingAutoSaveMethod autoSaveMethod = eDockingAutoSaveMethod.None)
            : base(rootWindow, uniqueId, persistentStorageFilePath, autoSaveMethod)
        {
            m_rootWindowWillCloseObserver = NSWindow.Notifications.ObserveWillClose(rootWindow, this.OnRootWindowWillClose);
        }

        protected override void OnDisposing ()
        {
            m_rootWindowWillCloseObserver?.Dispose();
            m_rootWindowWillCloseObserver = null;
            this.CreateNewTearoffWindowHandler = null;
        }

        protected override void OnRootZoneDisposing (DockZoneView zone)
        {
            zone.Teardown();
        }

        // ===========[ Events ]================================================
        // Force closes every tearoff, skipping the panels' close veto, so none outlives the window
        // the app was built around
        private void OnRootWindowWillClose (object sender, NSNotificationEventArgs e)
        {
            this.CloseTearoffsForRootWindowClosed();
        }

        // A tearoff's close button (or Command-W) asks here first, and the base runs the whole close,
        // a panel's veto included. A yes lets AppKit close the window.
        private void OnTearoffWindowClosing (object sender, CancelEventArgs e)
        {
            if (!this.DoTearoffWindowClose((NSWindow)sender))
            {
                e.Cancel = true;
            }
        }

        // A press on a tearoff's title strip: once it moves past the threshold, the whole tearoff is
        // dragged the way a dock drag is, so it can be dropped into a zone. A click that never moves
        // that far does nothing, and a double-click never gets here (the title strip zooms).
        private void OnTearoffTitleStripDragging (object sender, ThemedWindow.TitleStripDragEventArgs e)
        {
            e.Handled = true;
            if (this.WaitForDragThreshold(e.MouseDown))
            {
                this.DragWholeTearoff((NSWindow)sender);
            }
        }

        // ===========[ Properties ]============================================
        /// <summary>
        /// Makes the window a tearoff is shown in. Null, the default, makes a plain ThemedWindow. The manager sets the
        /// window's contents, title, title strip height and frame, so leave those unset.
        /// </summary>
        public Func<ThemedWindow> CreateNewTearoffWindowHandler { get; set; }

        /// <summary>The title every tearoff window shows; empty, the default, shows none</summary>
        public string FixedTearoffWindowTitle { get; set; } = String.Empty;

        /// <summary>
        /// How far, in points, a press on a panel header, a tab or a tearoff's title strip moves before it starts a drag.
        /// The same default as WinUI3's DragThresholdPixels.
        /// </summary>
        public double DragThreshold { get; set; } = DockDragRules.kDefaultDragThreshold;

        // ===========[ Public Interface Methods ]==============================
        /// <summary>Registers root zones that live in the root window</summary>
        public void RegisterRootDockZones (params DockZoneView[] dockZones)
            => this.RegisterRootDockZones(this.RootWindow, dockZones);

        /// <summary>
        /// Registers root zones hosted by <paramref name="window"/>. Each zone's view model must have been made for this
        /// manager; a zone with a <see cref="DockZoneView.GroupId"/> is what a layout and a panel's default group find
        /// it by.
        /// </summary>
        public void RegisterRootDockZones (NSWindow window, params DockZoneView[] dockZones)
        {
            foreach (DockZoneView zone in dockZones)
            {
                if (zone.ViewModel.Manager != this)
                {
                    // A zone's whole tree points at its manager, so it can only be registered with the
                    // one it was made for
                    Logger.LogError($"DockingManager: root zone '{zone.GroupId ?? "<no group>"}' was made for a different manager, and was not registered");
                    continue;
                }

                this.RecordRootZone(window, zone);
            }
        }

        /// <summary>
        /// Makes a root zone for the root window, with a view model for this manager, and registers it. Put the returned
        /// view wherever the root window's docking area goes.
        /// </summary>
        public DockZoneView CreateRootDockZone (string groupId = null)
        {
            var zone = new DockZoneView(new DockZoneViewModel(this)) { GroupId = groupId };
            this.RegisterRootDockZones(zone);
            return zone;
        }

        /// <summary>Tears a panel off into its own window, its top-left corner at <paramref name="windowTopLeft"/>, as big as the zone it leaves</summary>
        public Result<NSWindow> DoTearoff (IDockableDisplayElement element, CGPoint windowTopLeft)
        {
            DockZoneSize size = element.DockingAdapter.Location?.UI?.RenderSize ?? DockZoneSize.Empty;
            return this.DoTearoff(element, windowTopLeft, new CGSize((nfloat)size.Width, (nfloat)size.Height));
        }

        /// <summary>Tears a panel off into its own window, its top-left corner at <paramref name="windowTopLeft"/>, with content of <paramref name="contentSize"/></summary>
        public Result<NSWindow> DoTearoff (IDockableDisplayElement element, CGPoint windowTopLeft, CGSize contentSize)
            => this.DoTearoffCore(element, new DockWindowBounds((double)windowTopLeft.X, (double)windowTopLeft.Y, (double)contentSize.Width, (double)contentSize.Height));

        /// <summary>Moves a whole zone into its own window, its top-left corner at <paramref name="windowTopLeft"/>, as big as the zone is now</summary>
        public Result<NSWindow> DoGroupTearoff (DockZoneViewModel sourceZone, CGPoint windowTopLeft)
        {
            DockZoneSize size = sourceZone.UI?.RenderSize ?? DockZoneSize.Empty;
            return this.DoGroupTearoff(sourceZone, windowTopLeft, new CGSize((nfloat)size.Width, (nfloat)size.Height));
        }

        /// <summary>Moves a whole zone into its own window, its top-left corner at <paramref name="windowTopLeft"/>, with content of <paramref name="contentSize"/></summary>
        public Result<NSWindow> DoGroupTearoff (DockZoneViewModel sourceZone, CGPoint windowTopLeft, CGSize contentSize)
            => this.DoGroupTearoffCore(sourceZone, new DockWindowBounds((double)windowTopLeft.X, (double)windowTopLeft.Y, (double)contentSize.Width, (double)contentSize.Height));

        /// <summary>
        /// Tears <paramref name="panel"/> off into a tearoff placed under the pointer, the way the panel menu's Tear Off
        /// Panel does. Nothing follows the pointer afterward. Does nothing for a panel that can't tear off.
        /// </summary>
        internal void TearOffPanel (DockingContentAdapterModel panel)
        {
            if (!panel.CanTearoff || (panel.Display == null))
            {
                return;
            }

            DockTearoffPlacement placement = PlacePanelTearoff(panel);
            this.DoTearoff(panel.Display, ToCGPoint(placement.Origin), new CGSize((nfloat)placement.Width, (nfloat)placement.Height));
        }

        /// <summary>
        /// A tab dragged out of its strip: tears <paramref name="panel"/> off into a tearoff under the pointer and drags
        /// it until the button comes up. Returns once the drag is over.
        /// </summary>
        internal void DragPanel (DockingContentAdapterModel panel)
        {
            if (!panel.CanTearoff || (panel.Display == null))
            {
                return;
            }

            DockTearoffPlacement placement = PlacePanelTearoff(panel);
            DockDragOrigin origin = DockDragOrigin.ForPanel(panel);
            Result<NSWindow> tearoff = this.DoTearoff(panel.Display, ToCGPoint(placement.Origin), new CGSize((nfloat)placement.Width, (nfloat)placement.Height));
            if (tearoff)
            {
                this.RunDragSearch(tearoff.Value, placement.ScreenCursorOffset, origin);
            }
        }

        /// <summary>
        /// A header dragged past the threshold. In the root window the zone tears off; in a tearoff, the tearoff's own root
        /// zone leaves dragging to the title strip, any other zone tears off when the root is a split, and otherwise the
        /// whole tearoff is dragged. Returns once the drag is over.
        /// </summary>
        /// <param name="zoneView">The zone whose header was pressed</param>
        /// <param name="pressInZone">Where the press was, in the zone view's own points (top-left origin)</param>
        /// <param name="pressOnScreen">Where the press was, in AppKit screen coordinates</param>
        internal void DragFromHeader (DockZoneView zoneView, CGPoint pressInZone, CGPoint pressOnScreen)
        {
            NSWindow hostWindow = this.GetWindowForZone(zoneView);
            bool isInTearoff = (hostWindow != null) && (hostWindow != this.RootWindow);
            DockZoneView tearoffRootZone = isInTearoff ? this.GetRootZoneForWindow(hostWindow) : null;
            switch (DockDragRules.ChooseHeaderDragSubject(zoneView.ViewModel, isInTearoff, tearoffRootZone?.ViewModel))
            {
                case eHeaderDragSubject.ThisZone:
                {
                    // The header is the top of the zone, which sits under the new tearoff's title strip
                    var cursorOffset = new DockPoint((double)pressInZone.X, kTearoffTitleBarHeight + (double)pressInZone.Y);
                    DockTearoffPlacement placement = DockDragRules.PlaceTearoff(((IDockZoneUI)zoneView).RenderSize, ToTopLeft(pressOnScreen), cursorOffset, 1.0);
                    DockDragOrigin origin = DockDragOrigin.ForZone(zoneView.ViewModel);
                    Result<NSWindow> tearoff = this.DoGroupTearoff(zoneView.ViewModel, ToCGPoint(placement.Origin), new CGSize((nfloat)placement.Width, (nfloat)placement.Height));
                    if (tearoff)
                    {
                        this.RunDragSearch(tearoff.Value, placement.ScreenCursorOffset, origin);
                    }

                    break;
                }

                case eHeaderDragSubject.TearoffRoot:
                    this.DragWholeTearoff(hostWindow);
                    break;
            }
        }

        // ===========[ DockingManagerBase Implementations ]====================
        protected override DockZoneViewModel GetZoneViewModel (DockZoneView zone) => zone.ViewModel;

        protected override string GetZoneGroupId (DockZoneView zone) => zone.GroupId;

        protected override IEnumerable<DockZoneView> EnumerateZoneTree (DockZoneView rootZone)
        {
            yield return rootZone;
            foreach (DockZoneView child in rootZone.ChildZoneViews)
            {
                foreach (DockZoneView descendant in this.EnumerateZoneTree(child))
                {
                    yield return descendant;
                }
            }
        }

        // Placement bounds: the whole frame, top-left origin, in pixels; see the class comment
        protected override DockWindowBounds GetWindowBounds (NSWindow window) => ToPixels(ToTopLeftPoints(window.Frame));

        // Placement to creation: pixels down to points, and the frame's size down to its zone's. A ThemedWindow's
        // contents fill its whole frame (the system title bar is transparent over them), so only the title strip comes off.
        protected override DockWindowBounds ToTearoffCreationBounds (DockWindowBounds placementBounds)
        {
            DockWindowBounds points = FromPixels(placementBounds);
            return new DockWindowBounds(points.X, points.Y, points.Width, Math.Max(0.0, points.Height - kTearoffTitleBarHeight));
        }

        protected override bool IsWindowTracked (NSWindow window) => window == this.RootWindow || m_tearoffWindows.Contains(window);

        protected override void StopTrackingWindow (NSWindow window) => m_tearoffWindows.Remove(window);

        // Close, unlike PerformClose, does not ask the window's delegate, so a close the base has
        // already run does not run again
        protected override void CloseWindow (NSWindow window) => window.Close();

        protected override void ActivateWindow (NSWindow window) => window.MakeKeyAndOrderFront(null);

        protected override Result<NSWindow> CreateAndStockTearoffWindow (DockZoneViewModel rootZone, DockWindowBounds bounds)
        {
            ThemedWindow window = null;
            try
            {
                window = this.CreateNewTearoffWindowHandler?.Invoke() ?? CreateDefaultTearoffWindow();

                // A window made from C# is owned by its managed object; letting AppKit release it on
                // close as well would free it twice
                window.ReleaseWhenClosed(false);
                window.Title = this.FixedTearoffWindowTitle ?? String.Empty;
                window.TitleBarHeight = kTearoffTitleBarHeight;

                var zoneView = new DockZoneView(rootZone);
                window.WindowContents = zoneView;

                // Creation bounds: a top-left position and the zone's size, in points, so the frame adds the title strip
                window.SetFrame(ToFrame(new DockWindowBounds(bounds.X, bounds.Y, bounds.Width, bounds.Height + kTearoffTitleBarHeight)), true);

                window.Closing += this.OnTearoffWindowClosing;
                window.TitleStripDragging += this.OnTearoffTitleStripDragging;

                m_tearoffWindows.Add(window);
                this.RecordTearoffRootZone(window, rootZone);
                this.RegisterRootDockZones(window, zoneView);
                window.MakeKeyAndOrderFront(null);

                rootZone.ClearPassAlongUISize();
                return Result<NSWindow>.Success(window);
            }
            catch (Exception exc)
            {
                if (window != null)
                {
                    m_tearoffWindows.Remove(window);
                    this.DetachTearoffSubscriptions(window);
                    window.Close();
                }

                Logger.LogError("Window tearoff creation failed!", exc);
                return Result<NSWindow>.Error("DockingManager: Window tearoff failed");
            }
        }

        // The window's Closing and TitleStripDragging are the manager's only hooks on a tearoff, and
        // the zone view is the only thing listening to the zone tree; all of them go before the
        // window closes, so a closing window keeps nothing alive
        protected override void DetachTearoffSubscriptions (NSWindow window)
        {
            if (window is not ThemedWindow themedWindow)
            {
                return;
            }

            themedWindow.Closing -= this.OnTearoffWindowClosing;
            themedWindow.TitleStripDragging -= this.OnTearoffTitleStripDragging;
            (themedWindow.WindowContents as DockZoneView)?.Teardown();
        }

        // For the layout file: the window's placement bounds, and whether it is zoomed or fullscreen. A window this manager
        // no longer tracks is already closing, and is left out of the file.
        protected override bool TryCaptureWindowLayoutState (NSWindow window, out DockWindowLayoutState state)
        {
            if (!m_tearoffWindows.Contains(window))
            {
                state = default;
                return false;
            }

            bool isFullscreen = (window.StyleMask & NSWindowStyle.FullScreenWindow) == NSWindowStyle.FullScreenWindow;
            state = new DockWindowLayoutState(this.GetWindowBounds(window), window.IsZoomed, isFullscreen);
            return true;
        }

        // Zoom and full screen both toggle, so each only goes on when the window is not already in it
        protected override void ApplyWindowLayoutState (NSWindow window, DockWindowLayoutState state)
        {
            if (state.IsFullscreen)
            {
                if ((window.StyleMask & NSWindowStyle.FullScreenWindow) != NSWindowStyle.FullScreenWindow)
                {
                    window.ToggleFullScreen(null);
                }
            }
            else if (state.IsMaximized && !window.IsZoomed)
            {
                window.Zoom(null);
            }
        }

        // ===========[ Helper Methods ]========================================
        private static ThemedWindow CreateDefaultTearoffWindow ()
        {
            const double kPlaceholderSize = 400.0;
            return new ThemedWindow(new CGRect(0, 0, (nfloat)kPlaceholderSize, (nfloat)kPlaceholderSize));
        }

        // Where the pointer is, as a top-left point measured down from the top of the primary screen
        private static DockPoint PointerTopLeft () => ToTopLeft(NSEvent.CurrentMouseLocation);

        // An AppKit screen point (measured up from the bottom of the primary screen) as a top-left one
        private static DockPoint ToTopLeft (CGPoint screenPoint) => new DockPoint((double)screenPoint.X, PrimaryScreenHeight() - (double)screenPoint.Y);

        private static CGPoint ToCGPoint (DockPoint point) => new CGPoint((nfloat)point.X, (nfloat)point.Y);

        // A lone panel's tearoff, under the pointer, as big as the zone it leaves; the pointer sits on the title strip
        private static DockTearoffPlacement PlacePanelTearoff (DockingContentAdapterModel panel)
            => DockDragRules.PlaceTearoff(panel.Location?.UI?.RenderSize ?? DockDragRules.kFallbackPanelSize, PointerTopLeft(), null, 1.0);

        // Drags a tearoff that already exists, keeping the pointer where it is in the window. Nothing was torn
        // off, so Escape only stops the drag where it is.
        private void DragWholeTearoff (NSWindow window)
        {
            if (window == null)
            {
                return;
            }

            CGPoint pointer = NSEvent.CurrentMouseLocation;
            CGRect frame = window.Frame;
            var cursorOffset = new DockPoint((double)(pointer.X - frame.X), (double)(frame.Y + frame.Height - pointer.Y));
            this.RunDragSearch(window, cursorOffset, null);
        }

        // Takes the mouse events after mouseDown until the press moves past the drag threshold (true) or the
        // button comes up first (false), the way a control decides between a click and a drag
        private bool WaitForDragThreshold (NSEvent mouseDown)
        {
            CGPoint start = mouseDown.LocationInWindow;
            while (true)
            {
                NSEvent next = NSApplication.SharedApplication.NextEvent(NSEventMask.LeftMouseDragged | NSEventMask.LeftMouseUp, NSDate.DistantFuture, NSRunLoopMode.EventTracking, true);
                if ((next == null) || (next.Type == NSEventType.LeftMouseUp))
                {
                    return false;
                }

                CGPoint now = next.LocationInWindow;
                if (DockDragRules.HasMovedPastThreshold((double)(now.X - start.X), (double)(now.Y - start.Y), this.DragThreshold))
                {
                    return true;
                }
            }
        }

        // The drop search. AppKit follows a drag with an event-tracking loop: NextEvent takes the mouse and key
        // events itself, in the tracking run loop mode, until the drag ends, so the window under the pointer never
        // sees them. It waits at most one tick for an event, so a pointer held still still ticks the shared session
        // (a window it rests on flashes, then comes forward). The dragged window follows every event; what is under
        // the pointer is looked at once a tick.
        //  - The button coming up drops onto the engaged widget, or leaves the tearoff where it is.
        //  - Escape cancels: what the drag tore off goes back where it came from (origin), or with no origin the
        //    tearoff only stops.
        // See https://developer.apple.com/documentation/appkit/nsapplication/nextevent(matching:until:inmode:dequeue:)
        // and Apple's Cocoa Event Handling Guide, "Handling Mouse Dragging Operations" (the mouse-tracking loop).
        private void RunDragSearch (NSWindow source, DockPoint screenCursorOffset, DockDragOrigin origin)
        {
            DockZoneViewModel sourceRoot = this.GetRootZoneForWindow(source)?.ViewModel;
            if (sourceRoot == null)
            {
                return;
            }

            const NSEventMask kDragEvents = NSEventMask.LeftMouseDragged | NSEventMask.LeftMouseUp | NSEventMask.KeyDown;
            var host = new DragSearchHost(source);
            var session = new DockDragSession<DockZoneView, DockDropOverlayView.InsertionWidget, NSWindow>(host);
            var tickClock = Stopwatch.StartNew();
            NSWindow dropWindow = null;

            this.IsZoneDragDropUnderway = true;
            try
            {
                while (true)
                {
                    NSEvent next = NSApplication.SharedApplication.NextEvent(kDragEvents, NSDate.FromTimeIntervalSinceNow(kDragTickSeconds), NSRunLoopMode.EventTracking, true);
                    if ((next != null)
                        && (next.Type == NSEventType.KeyDown)
                        && (next.KeyCode == kEscapeKeyCode))
                    {
                        session.Cancel();
                        break;
                    }

                    // The button state is read as well as the up event, which covers a release that came before the
                    // loop started taking events
                    if (((next != null) && (next.Type == NSEventType.LeftMouseUp))
                        || ((NSEvent.CurrentPressedMouseButtons & 1) == 0))
                    {
                        break;
                    }

                    CGPoint pointer = NSEvent.CurrentMouseLocation;
                    source.SetFrameTopLeftPoint(new CGPoint(pointer.X - (nfloat)screenCursorOffset.X, pointer.Y + (nfloat)screenCursorOffset.Y));

                    if (tickClock.Elapsed.TotalSeconds >= kDragTickSeconds)
                    {
                        tickClock.Restart();
                        this.FindUnderPointer(pointer, source, out DockZoneView zone, out DockDropOverlayView.InsertionWidget widget, out dropWindow, out NSWindow hoverWindow);
                        session.Update(zone, widget, hoverWindow);
                    }
                }

                DockZoneView dropZone = session.DropTarget;
                DockDropOverlayView.InsertionWidget engagedWidget = session.EngagedWidget;
                if ((engagedWidget != null) && (dropZone?.ViewModel != null))
                {
                    this.CommitDrop(dropZone.ViewModel, sourceRoot, engagedWidget.Direction, source, dropWindow);
                }
                else if (session.IsCanceled && (origin != null))
                {
                    // Canceled: what the drag tore off is dropped back where it came from, which closes the tearoff
                    // the drag made, then gets its tab position, selection and split sizes back
                    this.ReturnCanceledDrag(origin, sourceRoot, source);
                }
            }
            finally
            {
                this.IsZoneDragDropUnderway = false;
                session.End();
                host.RestoreDragSource();
            }
        }

        // What is under the pointer, for one tick of a drag. The app's windows are walked front to back, so the
        // window the pointer is over is the first one whose frame holds it; a window in front that isn't a docking
        // window hides everything behind it. In a docking window, the zone is the innermost zone view under the
        // pointer, and the widget is the one under it on that zone's overlay, if the overlay is showing. The
        // pointer resting on a docking window that is not already the frontmost one (the dragged window aside) makes
        // that window a candidate for raising.
        private void FindUnderPointer (CGPoint pointer, NSWindow source, out DockZoneView zone, out DockDropOverlayView.InsertionWidget widget, out NSWindow pointerWindow, out NSWindow hoverWindow)
        {
            zone = null;
            widget = null;
            pointerWindow = null;
            hoverWindow = null;

            NSWindow frontmostDockingWindow = null;
            bool isPointerWindowDocking = false;
            foreach (NSWindow window in WindowsFrontToBack())
            {
                if ((window == source) || !window.IsVisible)
                {
                    continue;
                }

                bool isDocking = this.TryGetZonesForWindow(window, out _);
                if (isDocking && (frontmostDockingWindow == null))
                {
                    frontmostDockingWindow = window;
                }

                if ((pointerWindow == null) && window.Frame.Contains(pointer))
                {
                    pointerWindow = window;
                    isPointerWindowDocking = isDocking;
                }
            }

            if ((pointerWindow == null) || !isPointerWindowDocking)
            {
                pointerWindow = null;
                return;
            }

            if (pointerWindow != frontmostDockingWindow)
            {
                hoverWindow = pointerWindow;
            }

            // NSView.HitTest takes a point in the view's superview's coordinates
            CGPoint windowPoint = pointerWindow.ConvertPointFromScreen(pointer);
            NSView contentView = pointerWindow.ContentView;
            NSView hit = contentView?.HitTest(contentView.Superview?.ConvertPointFromView(windowPoint, null) ?? windowPoint);
            for (NSView view = hit; view != null; view = view.Superview)
            {
                if ((view is DockZoneView zoneView) && (zoneView.ViewModel.Manager == this))
                {
                    zone = zoneView;
                    break;
                }
            }

            widget = zone?.FindDropWidget(windowPoint);
        }

        // The app's windows in screen order, frontmost first. NSApplication's enumerateWindowsWithOptions: is the one
        // API that promises that order (its windows list does not), and it takes a block, so the order is gathered here.
        private static List<NSWindow> WindowsFrontToBack ()
        {
            var windows = new List<NSWindow>();
            NSApplication.SharedApplication.EnumerateWindows(NSWindowListOptions.OrderedFrontToBack, _Collect);
            return windows;

            void _Collect (NSWindow window, ref bool stop)
            {
                windows.Add(window);
            }
        }

        // The primary screen's points to pixels scale, which placement bounds are scaled by both ways
        private static double PrimaryScreenScale ()
        {
            NSScreen[] screens = NSScreen.Screens;
            return screens.Length > 0 ? (double)screens[0].BackingScaleFactor : 1.0;
        }

        private static DockWindowBounds ToPixels (DockWindowBounds points)
        {
            double scale = PrimaryScreenScale();
            return new DockWindowBounds(points.X * scale, points.Y * scale, points.Width * scale, points.Height * scale);
        }

        private static DockWindowBounds FromPixels (DockWindowBounds pixels)
        {
            double scale = PrimaryScreenScale();
            return new DockWindowBounds(pixels.X / scale, pixels.Y / scale, pixels.Width / scale, pixels.Height / scale);
        }

        // AppKit frames are measured up from the bottom of the primary screen (the one with the menu
        // bar, always Screens[0]); bounds are measured down from its top
        private static double PrimaryScreenHeight ()
        {
            NSScreen[] screens = NSScreen.Screens;
            return screens.Length > 0 ? (double)screens[0].Frame.Height : 0.0;
        }

        private static DockWindowBounds ToTopLeftPoints (CGRect frame)
        {
            double top = PrimaryScreenHeight() - ((double)frame.Y + (double)frame.Height);
            return new DockWindowBounds((double)frame.X, top, (double)frame.Width, (double)frame.Height);
        }

        private static CGRect ToFrame (DockWindowBounds bounds)
        {
            double bottom = PrimaryScreenHeight() - (bounds.Y + bounds.Height);
            return new CGRect((nfloat)bounds.X, (nfloat)bottom, (nfloat)bounds.Width, (nfloat)bounds.Height);
        }

        // ===========[ Subclasses/structs ]====================================
        // The AppKit side of one drag search, for the shared DockDragSession. Opacity is the window's
        // alpha, which needs nothing switched on first, so a flash only sets it; raising orders the
        // window to the front and the dragged one back above it.
        private sealed class DragSearchHost : IDockDragSessionHost<DockZoneView, DockDropOverlayView.InsertionWidget, NSWindow>
        {
            private readonly NSWindow m_source;

            public DragSearchHost (NSWindow source)
            {
                m_source = source;
            }

            public void ShowDropTarget (DockZoneView zone, bool isTarget) => zone.ShowDropOverlay(isTarget);

            public void EngageWidget (DockDropOverlayView.InsertionWidget widget, bool isEngaged) => widget.IsEngaged = isEngaged;

            public void DimDragSource (bool isDimmed) => m_source.AlphaValue = isDimmed ? (nfloat)kTearoffDragOpacity : (nfloat)1.0;

            // A window's alpha needs no setup, so a flash only starts when its opacity is first set
            public void BeginHoverFlash (NSWindow window) { }

            public void SetHoverFlashOpacity (NSWindow window, double opacity) => window.AlphaValue = (nfloat)opacity;

            public void EndHoverFlash (NSWindow window) => window.AlphaValue = (nfloat)1.0;

            public void RaiseHoverWindow (NSWindow window)
            {
                window.OrderFront(null);
                m_source.OrderFront(null);
            }

            /// <summary>Puts the dragged window back to full opacity</summary>
            public void RestoreDragSource () => m_source.AlphaValue = (nfloat)1.0;
        }
    }
}
