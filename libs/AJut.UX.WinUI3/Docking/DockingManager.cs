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

    public class DockingManager : NotifyPropertyChanged, IDockingManager
    {
        // ===========[ P/Invoke — cursor + key state ]========================
        [DllImport("user32.dll")]
        private static extern bool GetCursorPos (out POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState (int vKey);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
        private static extern nint GetWindowLongPtr (nint hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
        private static extern nint SetWindowLongPtr (nint hWnd, int nIndex, nint dwNewLong);

        [DllImport("user32.dll")]
        private static extern bool SetLayeredWindowAttributes (nint hwnd, uint crKey, byte bAlpha, uint dwFlags);

        [DllImport("user32.dll")]
        private static extern bool ScreenToClient (nint hWnd, ref POINT lpPoint);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int X; public int Y; }

        // ===========[ Constants ]============================================
        private const int kGwlExStyle = -20;
        private const int kWsExLayered = 0x80000;
        private const uint kLwaAlpha = 0x2;
        private const int kVkLButton = 0x01;
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

        // ===========[ Construction ]=========================================
        public DockingManager (
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

        // ===========[ IDockingManager ]======================================

        IDockableDisplayElement IDockingManager.BuildNewDisplayElement (Type elementType)
            => this.BuildNewDisplayElement(elementType);

        // ===========[ Factory Registration ]=================================

        public void RegisterDisplayFactory<T> (Func<T> customFactory = null, bool singleInstanceOnly = false)
            where T : IDockableDisplayElement
        {
            m_factory[typeof(T)] = new DisplayBuilder
            {
                IsSingleInstanceOnly = singleInstanceOnly,
                Builder = customFactory != null
                    ? () => customFactory() as IDockableDisplayElement
                    : () => BuildDefaultDisplayFor(typeof(T))
            };
        }

        // ===========[ Zone Registration ]====================================

        public void RegisterRootDockZones (params DockZone[] dockZones)
            => this.RegisterRootDockZones(this.RootWindow, dockZones);

        public void RegisterRootDockZones (Window window, params DockZone[] dockZones)
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

        public void DeRegisterRootDockZones (params DockZone[] dockZones)
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

        public T BuildNewDisplayElement<T> () where T : IDockableDisplayElement
            => (T)this.BuildNewDisplayElement(typeof(T));

        public IDockableDisplayElement BuildNewDisplayElement (Type elementType)
        {
            var displayElement = m_factory.TryGetValue(elementType, out var b)
                ? b.Builder()
                : BuildDefaultDisplayFor(elementType);
            return this.SetupAndReturnNew(displayElement);
        }

        // ===========[ Layout Control ]=======================================

        public bool CloseAll (bool force = false)
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
            foreach (var w in tearoffs)
            {
                m_tearoffRootZones.Remove(w);
                m_dockZoneMapping.Remove(w);
                m_windowManager.StopTracking(w);
                w.Close();
            }

            m_windowsToCloseSilently.Clear();
            return true;
        }

        public void CleanZoneLayoutHierarchies ()
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

        public DockZoneViewModel FindFirstAvailableDockZone ()
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

            Logger.LogError("DockingManager: tried to find first available dock zone but none exists — manager may be set up incorrectly");
            return null;
        }

        // ===========[ Tearoff Window Ops ]===================================

        public Result<Window> DoTearoff (IDockableDisplayElement element, Point newWindowOrigin)
        {
            DockZoneSize dockSize = element.DockingAdapter.Location?.UI?.RenderSize ?? DockZoneSize.Empty;
            return this.DoTearoff(element, newWindowOrigin, new Size(dockSize.Width, dockSize.Height));
        }

        public Result<Window> DoTearoff (IDockableDisplayElement element, Point newWindowOrigin, Size newWindowSize)
        {
            try
            {
                if (!element.DockingAdapter.Location.RemoveDockedContent(element.DockingAdapter))
                {
                    var result = Result<Window>.Error("DockingManager: Tear off failed — could not remove content from zone");
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

        public Result<Window> DoGroupTearoff (DockZoneViewModel sourceZone, Point newWindowOrigin)
        {
            var size = sourceZone.UI?.RenderSize ?? DockZoneSize.Empty;
            return this.DoGroupTearoff(sourceZone, newWindowOrigin, new Size(size.Width, size.Height));
        }

        public Result<Window> DoGroupTearoff (DockZoneViewModel sourceZone, Point newWindowOrigin, Size newTearoffWindowSize)
        {
            try
            {
                if (sourceZone.Parent != null)
                {
                    sourceZone.UnparentAndDistributeSibling();
                }
                else
                {
                    // Already root of a tearoff window — return its existing window
                    var existingWindow = m_tearoffRootZones
                        .Where(kv => kv.Value == sourceZone)
                        .Select(kv => kv.Key)
                        .FirstOrDefault();

                    if (existingWindow != null)
                    {
                        return Result<Window>.Success(existingWindow);
                    }

                    // Root zone on the main window — duplicate so the main zone keeps its slot
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
        internal async Task InitiateDrag (DockZone zone, Point? pressScreenPos = null, Point? cursorWindowOffset = null)
        {
            if (!GetCursorPos(out POINT pt))
            {
                return;
            }

            var renderSize = ((IDockZoneUI)zone).RenderSize;
            int windowWidth  = (int)Math.Max(renderSize.Width,  200);
            int windowHeight = (int)Math.Max(renderSize.Height,  50);

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
        internal async Task InitiateDragForContent (IDockableDisplayElement element, Point? pressScreenPos = null, Point? cursorWindowOffset = null)
        {
            if (element?.DockingAdapter == null)
            {
                return;
            }

            if (!GetCursorPos(out POINT pt))
            {
                return;
            }

            var locationSize = element.DockingAdapter.Location?.UI?.RenderSize ?? new DockZoneSize(400, 300);
            int windowWidth  = (int)Math.Max(locationSize.Width,  200);
            int windowHeight = (int)Math.Max(locationSize.Height,  50);

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
        public async Task RunDragSearch (Window dragSourceWindow, DockZone sourceDockZone, Point? cursorWindowOffset = null)
        {
            DockZone lastDropTarget = null;
            DockDropInsertionDriverWidget lastInsertionWidget = null;

            // Apply semi-transparency to the tearoff while dragging (Win32 layered window)
            nint hwnd = WinRT.Interop.WindowNative.GetWindowHandle(dragSourceWindow);
            nint originalExStyle = GetWindowLongPtr(hwnd, kGwlExStyle);
            SetWindowLongPtr(hwnd, kGwlExStyle, originalExStyle | kWsExLayered);
            SetLayeredWindowAttributes(hwnd, 0, (byte)(255 * kTearoffDragOpacity), kLwaAlpha);

            var sourceAppWindow = dragSourceWindow.AppWindow;

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
                    if ((GetAsyncKeyState(kVkLButton) & 0x8000) == 0)
                    {
                        break;
                    }

                    if (!GetCursorPos(out POINT pt))
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

                    foreach (var (window, zones) in m_dockZoneMapping)
                    {
                        if (window == dragSourceWindow)
                        {
                            continue;
                        }

                        // Convert screen cursor position to window client coordinates via Win32
                        // (handles title-bar chrome offset automatically), then divide by the
                        // XAML DPI scale to get device-independent pixel coordinates that
                        // FindElementsInHostCoordinates expects.
                        nint winHwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
                        POINT screenPt = new POINT { X = pt.X, Y = pt.Y };
                        if (!ScreenToClient(winHwnd, ref screenPt) || screenPt.X < 0 || screenPt.Y < 0)
                        {
                            continue;
                        }

                        double dpiScale = (window.Content as Microsoft.UI.Xaml.FrameworkElement)
                            ?.XamlRoot?.RasterizationScale ?? 1.0;

                        foreach (var rootZone in zones)
                        {
                            var localPt = new Point(screenPt.X / dpiScale, screenPt.Y / dpiScale);
                            var hits = Microsoft.UI.Xaml.Media.VisualTreeHelper
                                .FindElementsInHostCoordinates(localPt, rootZone);

                            // Check insertion widget first — takes priority over zone hit
                            var hitWidget = hits.OfType<DockDropInsertionDriverWidget>().FirstOrDefault();
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
                }

                // Execute drop if a widget was engaged when button was released
                if (lastInsertionWidget?.InsertionZone?.ViewModel != null)
                {
                    lastInsertionWidget.InsertionZone.ViewModel.DropAddSiblingIntoDock(
                        sourceDockZone.ViewModel,
                        lastInsertionWidget.Direction
                    );

                    // Always close the tearoff after a successful drop. For tab-add drops the
                    // source zone is emptied; for directional splits the source ViewModel is
                    // transplanted into the root hierarchy so DockedContent stays non-empty —
                    // but the tearoff window is no longer needed in either case.
                    this.CloseTearoffWindow(dragSourceWindow);
                }
                // No widget → tearoff stays at its current position as a detached window
            }
            finally
            {
                this.IsZoneDragDropUnderway = false;

                // Restore tearoff opacity. If CloseSourceTearoffIfEmpty already closed the
                // window these Win32 calls are harmless no-ops on the stale HWND.
                SetLayeredWindowAttributes(hwnd, 0, 255, kLwaAlpha);
                SetWindowLongPtr(hwnd, kGwlExStyle, originalExStyle);

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

        public bool SetupDefaultAndFallbackTo (string fallbackLayoutFilePath)
        {
            if (this.ReloadDockLayoutFromPersistentStorage())
            {
                m_isReadyToTrackAutoSave = true;
                return true;
            }
            else if (fallbackLayoutFilePath != null && this.LoadDockLayoutFromFile(fallbackLayoutFilePath))
            {
                this.SaveDockLayoutToPersistentStorage();
                m_isReadyToTrackAutoSave = true;
                return true;
            }

            return false;
        }

        public bool ReloadDockLayoutFromPersistentStorage ()
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

        public bool LoadDockLayoutFromFile (string filePath)
            => DockingSerialization.ResetFromState(filePath, this);

        public bool SaveDockLayoutToPersistentStorage ()
            => this.SaveDockLayoutToFile(this.DockLayoutPersistentStorageFile);

        public bool SaveDockLayoutToFile (string filePath = null)
            => DockingSerialization.SerializeStateTo(filePath, this);

        public string GenerateDefaultStateStorageAutoSaveTempPath ()
            => this.DockLayoutPersistentStorageFile + "~";

        // ===========[ Internal — used by DockingSerialization ]==============

        internal DockZoneViewModel GetFallbackRootZone ()
            => m_rootDockZones.FirstOrDefault()?.ViewModel;

        internal DockZoneViewModel GetRootZone (string groupId)
            => m_rootDockZones.FirstOrDefault(z => DockZone.GetGroupId(z) == groupId)?.ViewModel;

        internal void ClearForLoadingFromState ()
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

        internal DockingSerialization.CoreStorageData BuildSerializationInfoForRoot ()
        {
            var storage = new DockingSerialization.CoreStorageData();

            if (m_dockZoneMapping.TryGetValue(this.RootWindow, out var rootZones))
            {
                foreach (var rootZone in rootZones)
                {
                    string groupId = DockZone.GetGroupId(rootZone) ?? string.Empty;
                    if (!storage.ZoneInfoByRoot.TryAdd(groupId, rootZone.ViewModel.GenerateSerializationState()))
                    {
                        Logger.LogError($"Docking Serialization: Duplicate root zone group id '{groupId}' — serialization skipped for that zone");
                    }
                }
            }

            return storage;
        }

        internal IEnumerable<DockingSerialization.WindowStorageData> BuildSerializationInfoForAncillaryWindows ()
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

        internal Result<Window> CreateAndStockTearoffWindow (
            DockZoneViewModel rootZone,
            Point origin,
            Size size)
        {
            Window window = null;
            try
            {
                window = this.CreateNewTearoffWindowHandler();

                // Tool-window presenter: keep the resize border so the user can resize
                // the tearoff, but remove the OS title bar (and with it the system
                // caption buttons). Our custom header row provides all chrome.
                var overlappedPresenter = Microsoft.UI.Windowing.OverlappedPresenter.Create();
                overlappedPresenter.SetBorderAndTitleBar(hasBorder: true, hasTitleBar: false);
                window.AppWindow.SetPresenter(overlappedPresenter);

                var newDockZone = new DockZone { ViewModel = rootZone };

                // ---- Custom chrome -----------------------------------------------
                // Replaces the system title bar with a minimal header:
                //   [         drag area         ] [✕ close]
                // PointerPressed on the drag area (the star-width region) calls
                // InitiateDrag: the tearoff window follows the cursor and drop-target
                // overlays appear on other zones. The close Button captures pointer
                // first so clicking it never triggers the drag handler.
                var fgBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0xF0, 0xF0, 0xF0));

                var closeButton = new Button
                {
                    Content           = "✕",
                    Padding           = new Thickness(8, 4, 8, 4),
                    VerticalAlignment = VerticalAlignment.Stretch,
                    BorderThickness   = new Thickness(0),
                    Background        = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0)),
                    Foreground        = fgBrush,
                };
                closeButton.Click += (s, e) => window.Close();

                var titleBarGrid = new Grid
                {
                    Height     = 30,
                    Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0x1A, 0x1A, 0x1A)),
                };
                titleBarGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                titleBarGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                Grid.SetColumn(closeButton, 1);
                titleBarGrid.Children.Add(closeButton);

                // Root layout: title bar row + dock zone row
                var rootGrid = new Grid();
                rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                Grid.SetRow(titleBarGrid, 0);
                Grid.SetRow(newDockZone,  1);
                rootGrid.Children.Add(titleBarGrid);
                rootGrid.Children.Add(newDockZone);

                window.Content = rootGrid;

                // Pressing on the title bar (not the close button) starts a dock-drag.
                // We wait for DragThresholdPixels of movement before initiating so a
                // simple click on the title bar doesn't accidentally tear off the window.
                // The press-point is passed through so the window stays under the cursor.
                bool titleBarDragPending = false;
                Windows.Foundation.Point titleBarLocalPressPt  = default;
                Windows.Foundation.Point titleBarScreenPressPt = default;

                titleBarGrid.PointerPressed += (s, e) =>
                {
                    e.Handled = true;
                    titleBarDragPending = true;
                    titleBarLocalPressPt = e.GetCurrentPoint(titleBarGrid).Position;
                    if (GetCursorPos(out POINT sp))
                    {
                        titleBarScreenPressPt = new Windows.Foundation.Point(sp.X, sp.Y);
                    }

                    titleBarGrid.CapturePointer(e.Pointer);
                };

                titleBarGrid.PointerMoved += (s, e) =>
                {
                    if (!titleBarDragPending)
                    {
                        return;
                    }

                    var cur = e.GetCurrentPoint(titleBarGrid).Position;
                    double dx = cur.X - titleBarLocalPressPt.X;
                    double dy = cur.Y - titleBarLocalPressPt.Y;
                    double threshold = this.DragThresholdPixels;

                    if (dx * dx + dy * dy > threshold * threshold)
                    {
                        titleBarDragPending = false;
                        titleBarGrid.ReleasePointerCapture(e.Pointer);
                        e.Handled = true;

                        // cursorWindowOffset = local press point within the title bar,
                        // which IS the top row of the window (Y offset = 0 extra).
                        var winOffset = new Windows.Foundation.Point(
                            titleBarLocalPressPt.X,
                            titleBarLocalPressPt.Y);

                        _ = this.InitiateDrag(newDockZone, titleBarScreenPressPt, winOffset);
                    }
                };

                titleBarGrid.PointerReleased += (s, e) =>
                {
                    titleBarDragPending = false;
                    titleBarGrid.ReleasePointerCapture(e.Pointer);
                };

                titleBarGrid.PointerCaptureLost += (s, e) =>
                {
                    titleBarDragPending = false;
                };
                // -----------------------------------------------------------------

                var appWindow = window.AppWindow;
                appWindow.Resize(new Windows.Graphics.SizeInt32((int)size.Width, (int)size.Height));
                appWindow.Move(new Windows.Graphics.PointInt32((int)origin.X, (int)origin.Y));
                appWindow.Closing += (s, e) => this.OnTearoffWindowClosing(window, e);

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

        // ===========[ Internal Helpers ]=====================================

        // Called by DockZone.OnHeaderPointerPressed to resolve the host Window.
        // Traverses m_dockZoneMapping; falls back to RootWindow if not found.
        internal Window GetWindowForZone (DockZone zone)
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
        internal DockZone GetRootDockZoneForWindow (Window window)
            => m_dockZoneMapping.TryGetValue(window, out var zones) ? zones.FirstOrDefault() : null;

        // Exposes Win32 GetCursorPos to DockZone without duplicating the P/Invoke.
        internal bool TryGetCursorScreenPos (out Point pt)
        {
            if (GetCursorPos(out POINT raw))
            {
                pt = new Point(raw.X, raw.Y);
                return true;
            }

            pt = default;
            return false;
        }

        // ===========[ Private Helpers ]======================================

        // Unconditionally close and deregister a tearoff window (e.g. after a successful drop).
        private void CloseTearoffWindow (Window sourceWindow)
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
        private void CloseSourceTearoffIfEmpty (Window sourceWindow, DockZone sourceDockZone)
        {
            bool hasContent = TreeTraversal<DockZone>.All(sourceDockZone)
                .Any(z => z.ViewModel?.DockedContent.Count > 0);

            if (!hasContent)
            {
                this.CloseTearoffWindow(sourceWindow);
            }
        }

        private void OnTearoffWindowClosing (Window window, Microsoft.UI.Windowing.AppWindowClosingEventArgs e)
        {
            if (m_windowsToCloseSilently.Contains(window) || m_currentlyClosingWindows.Contains(window))
            {
                return;
            }

            m_currentlyClosingWindows.Add(window);
            try
            {
                if (!m_tearoffRootZones.TryGetValue(window, out DockZoneViewModel root))
                {
                    return;
                }

                if (root.IsActivelyAttemptingClose)
                {
                    return;
                }

                var closeResult = root.RequestCloseAllAndClear();
                if (closeResult.HasErrors)
                {
                    e.Cancel = true;
                    return;
                }

                m_tearoffRootZones.Remove(window);
                m_dockZoneMapping.Remove(window);
                m_windowManager.StopTracking(window);
                this.RootWindow?.Activate();
            }
            finally
            {
                m_currentlyClosingWindows.Remove(window);
            }
        }

        // WindowManager Remove events fire both for actual window closes AND for
        // in-place reordering (Remove + immediate re-Insert when activation changes).
        // Removing from m_dockZoneMapping here would erase live tearoff entries on
        // every activation reorder. Cleanup is handled exclusively by
        // OnTearoffWindowClosing (AppWindow.Closing), CloseAll, and OnRootWindowClosed.
        private void OnWindowManagerCollectionChanged (object sender, NotifyCollectionChangedEventArgs e)
        {
            // Intentional no-op — see comment above.
        }

        // Force-close all tearoff windows when the root window closes so no
        // orphaned tool windows linger after the app exits. We bypass the normal
        // CanClose veto path by adding windows to m_windowsToCloseSilently first,
        // which causes OnTearoffWindowClosing to exit early without cancelling.
        private void OnRootWindowClosed (object sender, WindowEventArgs e)
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
        private void OnRootWindowActivated (object sender, WindowActivatedEventArgs e)
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

        private void TriggerLayoutAutoSave ()
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

        private IDockableDisplayElement SetupAndReturnNew (IDockableDisplayElement displayElement)
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

        private static IDockableDisplayElement BuildDefaultDisplayFor (Type elementType)
            => AJutActivator.CreateInstanceOf(elementType) as IDockableDisplayElement;

        // ===========[ Sub-classes ]===========================================

        private class DisplayBuilder
        {
            public bool IsSingleInstanceOnly { get; init; }
            public Func<IDockableDisplayElement> Builder { get; init; }
        }
    }
}
