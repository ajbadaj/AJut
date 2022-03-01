namespace AJut.UX.Docking
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.ComponentModel;
    using System.Linq;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Input;
    using System.Windows.Media;
    using System.Windows.Media.Imaging;
    using AJut.Storage;
    using AJut.Tree;
    using AJut.TypeManagement;
    using AJut.UX.AttachedProperties;
    using AJut.UX.Controls;

    public enum eDockingAutoSaveMethod
    {
        /// <summary>
        /// Never auto save
        /// </summary>
        None,

        /// <summary>
        /// Auto save to the <see cref="DockingManager.DockLayoutPersistentStorageFile"/> whenever anything happens
        /// </summary>
        AutoSaveOnAllChanges,

        /// <summary>
        /// Auto save to a temp file next to the <see cref="DockingManager.DockLayoutPersistentStorageFile"/> file whenever anything happens, but wait
        /// for explicit calls to save to the <see cref="DockingManager.DockLayoutPersistentStorageFile"/> directly.
        /// </summary>
        AutoSaveToTemp,
    }


    /* TODO: Auto save
     * Notes...
     * Enable auto save (bool, notify property changed)
     * Auto save on add/remove/resize panels
     * The one tricky part, this will be affected by window size, so saving that might be important as well, how to do that?
     */

    /// <summary>
    /// The centralized manager for a single docking experience
    /// </summary>
    public class DockingManager : NotifyPropertyChanged
    {
        private readonly Dictionary<Type, DisplayBuilder> m_factory = new Dictionary<Type, DisplayBuilder>();
        private readonly ObservableCollection<DockZone> m_rootDockZones = new ObservableCollection<DockZone>();
        private readonly MultiMap<Window, DockZone> m_dockZoneMapping = new MultiMap<Window, DockZone>();
        private bool m_isZoneDragDropUnderway;
        private bool m_isReadyToTrackAutoSave = false;

        private readonly List<Window> m_currentlyClosingWindows = new List<Window>();
        private readonly List<Window> m_windowsToCloseSilently = new List<Window>();

        /// <summary>
        /// Construct a new <see cref="DockingManager"/> instance.
        /// </summary>
        /// <param name="rootWindow">The main window that the manager will track</param>
        /// <param name="uniqueId">The unique id (human readability is optional, it will form the default layout storage file path if none is specified, so it should also be file safe)</param>
        /// <param name="persistentStorageFilePath">The (optionally specified) path to the default state save for the docking experience in which this manager represents</param>
        /// <param name="autoSaveMethod">How should the manager auto save to the default state storage path as layout changes occur (default = <see cref="eDockingAutoSaveMethod.None"/>)</param>
        public DockingManager (Window rootWindow, string uniqueId, string persistentStorageFilePath = null, eDockingAutoSaveMethod autoSaveMethod = eDockingAutoSaveMethod.None)
        {
            this.CreateNewTearoffWindowHandler = () => new DefaultDockTearoffWindow(this);
            this.ShowTearoffWindowHandler = w => w.Show();
            this.Windows = new WindowManager(rootWindow);
            this.UniqueId = uniqueId;
            this.DockLayoutPersistentStorageFile = persistentStorageFilePath ?? DockingSerialization.CreateApplicationPath(this.UniqueId);
            this.AutoSaveMethod = autoSaveMethod;
        }

        // ======================[ Properties ]=======================

        /// <summary>
        /// The unique id (human readability is optional, it will form the default layout storage file path if none is specified, so it should also be file safe)
        /// </summary>
        public string UniqueId { get; }

        /// <summary>
        /// The (optionally specified) path to the default state save for the docking experience in which this manager represents
        /// </summary>
        public string DockLayoutPersistentStorageFile { get; set; }

        /// <summary>
        /// The method by which this manager auto saves (if any)
        /// </summary>
        public eDockingAutoSaveMethod AutoSaveMethod { get; set; }

        /// <summary>
        /// The window tracking for all tearoff and associated docking windows
        /// </summary>
        public WindowManager Windows { get; }

        /// <summary>
        /// Indicates if a zone is actively being drag/dropped within this docking experience
        /// </summary>
        public bool IsZoneDragDropUnderway
        {
            get => m_isZoneDragDropUnderway;
            private set => this.SetAndRaiseIfChanged(ref m_isZoneDragDropUnderway, value);
        }

        /// <summary>
        /// An (optional) method for specifying creation of a tearoff window (default is null which will create a <see cref="DefaultDockTearoffWindow"/>)
        /// </summary>
        public Func<Window> CreateNewTearoffWindowHandler { private get; set; }

        /// <summary>
        /// An (optional) method for specifying how a tearoff window is shown (default is null which will result in <see cref="Window.Show"/>)
        /// </summary>
        public Action<Window> ShowTearoffWindowHandler { get; set; }

        /// <summary>
        /// Indicates if the manager is loading from a layout file (so as to stop other layout based actions from happening, like triggering auto-save)
        /// </summary>
        public bool IsLoadingFromLayout { get; internal set; } = false;

        // ======================[ Interface Methods ]=======================

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

                m_rootDockZones.Add(zone);
                zone.Manager = this;
                if (zone.ViewModel == null)
                {
                    zone.ViewModel = new DockZoneViewModel(this);
                }

                m_dockZoneMapping.Add(window, zone);
            }
        }

        /// <summary>
        /// Deregister a root <see cref="DockZone"/>.
        /// </summary>
        public void DeRegisterRootDockZones (params DockZone[] dockZones)
        {
            foreach (var zone in dockZones)
            {
                m_rootDockZones.Remove(zone);
                zone.Manager = null;
                m_dockZoneMapping.RemoveAllValues(zone);
            }
        }

        /// <summary>
        /// Sets up the manager, providing a fallback layout should the default file be non-existant or invalid
        /// </summary>
        /// <param name="fallbackLayoutFilePath">The file path (local file, embedded asset, or path of file from the interwebs)</param>
        /// <returns><c>true</c> if something is loaded (including fallback) or <c>false</c> otherwise</returns>
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

        /// <summary>
        /// Load the default state (at path: <see cref="DockLayoutPersistentStorageFile"/>), closes all (without prompting) and
        /// builds displays with the given state and the given layouts of the file.
        /// </summary>
        public bool ReloadDockLayoutFromPersistentStorage ()
        {
            try
            {
                return DockingSerialization.ResetFromState(this.DockLayoutPersistentStorageFile, this);
            }
            catch (Exception e)
            {
                Logger.LogError($"Docking: Error loading from default dock layout at '{DockLayoutPersistentStorageFile ?? "<null>"}'", e);
                return false;
            }
        }

        /// <summary>
        /// Loads the state from the given path, closes all (without prompting) and
        /// builds displays with the given state and the given layouts of the file.
        /// </summary>
        public bool LoadDockLayoutFromFile (string filePath)
        {
            return DockingSerialization.ResetFromState(filePath, this);
        }

        /// <summary>
        /// Saves to a file the current docking layout + display state for each display
        /// </summary>
        public bool SaveDockLayoutToPersistentStorage ()
        {
            return this.SaveDockLayoutToFile(this.DockLayoutPersistentStorageFile);
        }

        /// <summary>
        /// Saves to a file the current docking layout + display state for each display
        /// </summary>
        public bool SaveDockLayoutToFile (string filePath = null)
        {
            return DockingSerialization.SerializeStateTo(filePath, this);
        }

        /// <summary>
        /// Generates the auto save temp path (should only used if <see cref="AutoSaveMethod"/> == <see cref="eDockingAutoSaveMethod.AutoSaveToTemp"/>)
        /// </summary>
        public string GenerateDefaultStateStorageAutoSaveTempPath () => this.DockLayoutPersistentStorageFile + "~";

        /// <summary>
        /// Register a factory method called to build displays (implenetations of <see cref="IDockableDisplayElement"/>)
        /// </summary>
        /// <typeparam name="T">The type of display (an implenetation of <see cref="IDockableDisplayElement"/>)</typeparam>
        /// <param name="customFactory">The factory method</param>
        /// <param name="singleInstanceOnly">Indicates if only one instance of the display should allowed to be created (default false)</param>
        public void RegisterDisplayFactory<T> (Func<T> customFactory = null, bool singleInstanceOnly = false)
            where T : IDockableDisplayElement
        {
            var builder = new DisplayBuilder
            {
                IsSingleInstanceOnly = singleInstanceOnly,
                Builder = customFactory != null ? () => customFactory() as IDockableDisplayElement : () => BuildDefaultDisplayFor(typeof(T))
            };

            m_factory.Add(typeof(T), builder);
        }

        /// <summary>
        /// Use the factory and internal setup methods to build a new <see cref="IDockableDisplayElement"/> of type <see cref="T"/>
        /// </summary>
        /// <typeparam name="T">The type of display (an implenetation of <see cref="IDockableDisplayElement"/>)</typeparam>
        public T BuildNewDisplayElement<T> () where T : IDockableDisplayElement
        {
            return (T)this.BuildNewDisplayElement(typeof(T));
        }

        /// <summary>
        /// Use the factory and internal setup methods to build a new <see cref="IDockableDisplayElement"/> of type <see cref="T"/>
        /// </summary>
        /// <typeparam name="T">The type of display (an implenetation of <see cref="IDockableDisplayElement"/>)</typeparam>
        public IDockableDisplayElement BuildNewDisplayElement (Type elementType)
        {
            var displayElement = m_factory.TryGetValue(elementType, out var b)
                                    ? b.Builder()
                                    : BuildDefaultDisplayFor(elementType);
            return this.SetupAndReturnNew(displayElement);
        }

        /// <summary>
        /// Close all zones out, going through the effort first of asking them if that's ok prior to closing them
        /// </summary>
        /// <returns>True if all zones successfully closed, false otherwise</returns>
        public bool CloseAll ()
        {
            bool anyDissenters = false;
            var all = m_rootDockZones.SelectMany(z => TreeTraversal<DockZone>.All(z)).SelectMany(z => _DockedContentOrEmptyFor(z?.ViewModel)).ToList();
            foreach (var adapter in all)
            {
                if (!adapter.CheckCanClose())
                {
                    var closeSupression = new RoutedEventArgs(DockZone.NotifyCloseSupressionEvent);
                    adapter.Location.UI.RaiseEvent(closeSupression);
                    anyDissenters = true;
                }
            }

            if (anyDissenters)
            {
                return false;
            }

            this.Windows.CloseAllChildWindows();
            return true;

            IEnumerable<DockingContentAdapterModel> _DockedContentOrEmptyFor (DockZoneViewModel vm)
            {
                if ((vm?.DockedContent?.Count ?? 0) == 0)
                {
                    return Enumerable.Empty<DockingContentAdapterModel>();
                }

                return vm.DockedContent;
            }
        }

        /// <summary>
        /// Drags the given window around alpha'd out, and as the cursor moves highlights drop opportunities, executing
        /// the drop if the user releases.
        /// </summary>
        /// <param name="dragSourceWindow">The window which will be dragged</param>
        /// <param name="sourceDockZone">The dock zone on that window which will be dragged</param>
        public async Task RunDragSearch (Window dragSourceWindow, DockZone sourceDockZone)
        {
            const double kMouseOffset = 20.0;
            DockZone currentDropTarget = null;
            DockZone lastDropTarget = null;
            DockDropInsertionDriverWidget lastInsertionDriver = null;
            try
            {
                this.IsZoneDragDropUnderway = true;

                Window draggerWindow = new Window();
                draggerWindow.WindowStyle = WindowStyle.None;
                draggerWindow.AllowsTransparency = true;
                draggerWindow.Opacity = 0.45;

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
                    using (var stream = dragSourceWindow.RenderToPngAsIs())
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
                    await draggerWindow.AsyncDragMove(onMove: _WindowLocationChanged);
                }
                finally
                {
                    draggerWindow.Close();
                    dragSourceWindow.Left = draggerWindow.Left - kMouseOffset;
                    dragSourceWindow.Top = draggerWindow.Top - kMouseOffset;
                    dragSourceWindow.Visibility = Visibility.Visible;
                }

                if (currentDropTarget != null && lastInsertionDriver != null)
                {
                    // Ensure no bugs will inadvertently cause a break
                    if (lastInsertionDriver.InsertionZone.ViewModel == null)
                    {
                        lastInsertionDriver = null;
                    }
                    else
                    {
                        // Do the drop
                        lastInsertionDriver.InsertionZone.ViewModel.DropAddSiblingIntoDock(sourceDockZone.ViewModel, lastInsertionDriver.Direction);

                        // If it's a docking tearoff window and this was the last thing, close it
                        if (DockWindowConfig.GetIsDockingTearoffWindow(dragSourceWindow))
                        {
                            int numZones = dragSourceWindow.GetVisualChildren().OfType<DockZone>().Count();
                            if (numZones == 0 || numZones == 1)
                            {
                                m_windowsToCloseSilently.Add(dragSourceWindow);
                                try
                                {
                                    dragSourceWindow.Close();
                                    m_dockZoneMapping.RemoveAllFor(dragSourceWindow);
                                }
                                finally
                                {
                                    m_windowsToCloseSilently.Remove(dragSourceWindow);
                                }

                            }
                        }
                    }
                }
            }
            finally
            {
                this.IsZoneDragDropUnderway = false;
                if (lastInsertionDriver != null)
                {
                    lastInsertionDriver.IsEngaged = false;
                    lastInsertionDriver = null;
                }

                if (lastDropTarget != null)
                {
                    lastDropTarget.IsDirectDropTarget = false;
                    lastDropTarget = null;
                }
            }

            void _WindowLocationChanged ()
            {
                currentDropTarget = null;

                foreach (var window in this.Windows.Where(w => w != dragSourceWindow))
                {
                    var result = VisualTreeHelper.HitTest(window, Mouse.PrimaryDevice.GetPosition(window));
                    if (result?.VisualHit != null)
                    {
                        var hitWidget = result.VisualHit as DockDropInsertionDriverWidget ?? result.VisualHit.GetFirstParentOf<DockDropInsertionDriverWidget>();
                        if (hitWidget?.IsEnabled == true)
                        {
                            hitWidget.IsEngaged = true;
                            if (lastInsertionDriver != hitWidget && lastInsertionDriver != null)
                            {
                                lastInsertionDriver.IsEngaged = false;
                            }

                            lastInsertionDriver = hitWidget;
                        }
                        else if (lastInsertionDriver != null)
                        {
                            lastInsertionDriver.IsEngaged = false;
                            lastInsertionDriver = null;
                        }

                        var hitZone = result.VisualHit as DockZone ?? result.VisualHit.GetFirstParentOf<DockZone>();
                        if (hitZone != null)
                        {
                            currentDropTarget = hitZone;
                            currentDropTarget.IsDirectDropTarget = true;
                            if (currentDropTarget != lastDropTarget && lastDropTarget != null)
                            {
                                lastDropTarget.IsDirectDropTarget = false;
                            }

                            lastDropTarget = currentDropTarget;
                            return;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Performs a tearoff for only the given display element (not the rest of the elements in the zone ui) by pulling
        /// it out of the UI it's embedded in, and creating + placing it in a new UI for it in a tearoff window that matches
        /// in size to the current zone ui.
        /// </summary>
        public Result<Window> DoTearoff (IDockableDisplayElement element, Point newWindowOrigin)
        {
            Size previousZoneSize = new Size(element.DockingAdapter.Location.UI.ActualWidth, element.DockingAdapter.Location.UI.ActualHeight);
            return this.DoTearoff(element, newWindowOrigin, previousZoneSize);
        }

        /// <summary>
        /// Performs a tearoff for only the given display element (not the rest of the elements in the zone ui) by pulling
        /// it out of the UI it's embedded in, and creating + placing it in a new UI for it in a tearoff window that is set
        /// to whatever size you specify.
        /// </summary>
        public Result<Window> DoTearoff (IDockableDisplayElement element, Point newWindowOrigin, Size newWindowSize)
        {
            try
            {
                // Cleanup: Remove element from zone
                if (!element.DockingAdapter.Location.RemoveDockedContent(element.DockingAdapter))
                {
                    var result = Result<Window>.Error("DockingManager: Tear off failed at panel closing");
                    Logger.LogError(result.GetErrorReport());
                    return result;
                }

                // Create the new dock zone & window, doing all appropriate tracking adds
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
                var result = Result<Window>.Error("DockingManager: Window tearoff failed for unknown reason");
                result.AddError(exc.ToString());
                Logger.LogError(result.GetErrorReport(), exc);
                return result;
            }
        }

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
            return this.DoGroupTearoff(sourceZone, newWindowOrigin, sourceZone.UI.RenderSize);
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
        {
            try
            {
                // Has a parent, standard tear off
                if (sourceZone.Parent != null)
                {
                    sourceZone.UnparentAndDistributeSibling();
                }
                // Is a root zone
                else
                {
                    // First check to see if it's the only zone in the window, if that's the case then we're good to go
                    var window = Window.GetWindow(sourceZone.UI);
                    if (DockWindowConfig.GetIsDockingTearoffWindow(window))
                    {
                        return Result<Window>.Success(window);
                    }
                    else
                    {
                        // Otherwise we have a scenario where we're tearing off a root zone from the main window. This
                        //  is not allowed as the root zones are the anchor points that should be expected to always exist
                        //  (for serialization and just normal expectation purposes). So instead move all current stuff
                        //  down into a new zone, and essentially tear that off instead
                        sourceZone = sourceZone.DuplicateAndClear();
                    }
                }

                return this.CreateAndStockTearoffWindow(sourceZone, newWindowOrigin, newTearoffWindowSize);
            }
            catch (Exception exc)
            {
                // There is probably more to do - but for now, at least make sure stray windows aren't left open
                Logger.LogError("Window tearoff failed!", exc);
                return Result<Window>.Error("DockingManager: Window tearoff failed for unknown reason");
            }
        }

        /// <summary>
        /// The general flow of docking expects certain layouts (like you won't have an empty zone as a sibling to other child zones),
        /// this is so ingrained, it is assumed to be there all the while providing major benefits of code speed up and simplification.
        /// While the system only builds layout hierarchies that are valid - manually built layouts, or layouts loaded from invalid layout
        /// files might cause layout hierarchies to be incorrect. To combat that, this cleanup function will fix any problems of that nature
        /// and should be considered to be called when doing any manual building/adding of <see cref="DockZoneViewModel"/>s.
        /// </summary>
        public void CleanZoneLayoutHierarchies ()
        {
            var toVisit = new Queue<DockZoneViewModel>();
            m_rootDockZones.Select(z => z.ViewModel).ForEach(toVisit.Enqueue);
            while (toVisit.Count != 0)
            {
                var parentEval = toVisit.Dequeue();
                if (EnumerableXT.IsNullOrEmpty(parentEval?.Children))
                {
                    continue;
                }

                for (int index = 0; index < parentEval.Children.Count; ++index)
                {
                    var child = parentEval.Children[index];
                    if (child.Orientation == parentEval.Orientation)
                    {
                        parentEval.RunChildZoneRemoval(child);
                        foreach (var grandchild in child.Children.AllNonNullElements().ToList())
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

        // ==============================[ Utility Methods ]======================================

        internal IDockableDisplayElement BuildNewDisplayElement (string typeId)
        {
            return TypeIdRegistrar.TryGetType(typeId, out Type type)
                    ? this.BuildNewDisplayElement(type)
                    : this.SetupAndReturnNew(AJutActivator.CreateInstanceOf(typeId) as IDockableDisplayElement);
        }

        private IDockableDisplayElement SetupAndReturnNew (IDockableDisplayElement displayElement)
        {
            if (displayElement == null)
            {
                return null;
            }

            displayElement.Setup(new DockingContentAdapterModel(this));
            displayElement.DockingAdapter.Display = displayElement;
            return displayElement;
        }

        internal DockZoneViewModel GetFallbackRootZone () => m_rootDockZones.FirstOrDefault()?.ViewModel;

        internal DockZoneViewModel GetRootZone (string groupId)
        {
            return m_rootDockZones.FirstOrDefault(z => DockZone.GetGroupId(z) == groupId)?.ViewModel;
        }

        internal void ClearForLoadingFromState ()
        {
            var allTearoffs = this.Windows.Where(w => w != this.Windows.Root).ToList();
            var allTearoffRoots = m_dockZoneMapping.Where(kv => kv.Key != this.Windows.Root).SelectMany(kv => kv.Value);

            m_windowsToCloseSilently.AddRange(allTearoffs);
            foreach (var tearoff in allTearoffs)
            {
                m_windowsToCloseSilently.Add(tearoff);
                tearoff.Close();
                m_dockZoneMapping.RemoveAllFor(tearoff);
                m_rootDockZones.RemoveAll(allTearoffRoots.Contains);
            }

            m_windowsToCloseSilently.Clear();

            // Now all that's left are the root window root zones
            foreach (var root in m_rootDockZones)
            {
                root.ViewModel?.ForceCloseAllAndClear();
            }
        }

        internal DockingSerialization.CoreStorageData BuildSerializationInfoForRoot ()
        {
            // Core window info:
            //  - Split by root zones w/ zone ids
            //  - normal serialization window
            var storage = new DockingSerialization.CoreStorageData();
            foreach (var rootZone in m_dockZoneMapping[this.Windows.Root])
            {
                string groupId = DockZone.GetGroupId(rootZone) ?? String.Empty;
                if (!storage.ZoneInfoByRoot.TryAdd(groupId, rootZone.ViewModel.GenerateSerializationState()))
                {
                    Logger.LogError($"Docking Serialization: Failed to build serialization data properly, as two ore more root zone groups share id of '{groupId}' - zone serialization info was SKIPPED");
                }
            }

            return storage;
        }

        internal IEnumerable<DockingSerialization.WindowStorageData> BuildSerializationInfoForAncillaryWindows ()
        {
            // For each docking tearoff window...
            //  - window location and display info (state, size, etc)
            //  - single root zone tree info
            foreach (var kv in m_dockZoneMapping)
            {
                if (kv.Key == this.Windows.Root)
                {
                    continue;
                }

                yield return new DockingSerialization.WindowStorageData
                {
                    WindowSize = kv.Key.RenderSize,
                    WindowState = kv.Key.WindowState,
                    WindowLocation = new Point(kv.Key.Left, kv.Key.Top),
                    WindowIsFullscreened = WindowXTA.GetIsFullscreen(kv.Key),
                    State = kv.Value.First().ViewModel.GenerateSerializationState()
                };
            }
        }
        internal Result<Window> CreateAndStockTearoffWindow (DockZoneViewModel rootZone, Point newWindowOrigin, Size previousZoneSize)
        {
            Window window = null;
            try
            {
                window = this.CreateNewTearoffWindowHandler();
                DockWindowConfig.SetDockingTearoffWindowRootZone(window, rootZone);
                this.Windows.Track(window);

                DockZone newRoot = new DockZone() { ViewModel = rootZone };
                window.Content = newRoot;
                window.WindowStartupLocation = WindowStartupLocation.Manual;
                window.Left = newWindowOrigin.X;
                window.Top = newWindowOrigin.Y;
                window.Width = previousZoneSize.Width;
                window.Height = previousZoneSize.Height;
                window.Closing += OnDockTearoffWindowClosing;
                this.ShowTearoffWindowHandler(window);
                this.RegisterRootDockZones(window, newRoot);

                rootZone.ClearPassAlongUISize();
                return Result<Window>.Success(window);
            }
            catch (Exception exc)
            {
                // There is probably more to do - but for now, at least make sure stray windows aren't left open
                if (window != null && window.IsActive)
                {
                    window.Close();
                }

                Logger.LogError("Window tearoff failed!", exc);
                return Result<Window>.Error("DockingManager: Window tearoff failed for unknown reason");
            }
        }

        private void OnDockTearoffWindowClosing (object sender, CancelEventArgs e)
        {
            var window = (Window)sender;
            if (m_windowsToCloseSilently.Contains(window))
            {
                return;
            }

            m_currentlyClosingWindows.Add(window);
            try
            {
                DockZoneViewModel root = DockWindowConfig.GetDockingTearoffWindowRootZone(window);
                if (root.IsActivelyAttemptingClose)
                {
                    return;
                }

                if (!root.RequestCloseAllAndClear())
                {
                    e.Cancel = true;
                    return;
                }

                this.Windows.Root.Focus();
            }
            finally
            {
                m_currentlyClosingWindows.Remove(window);
            }
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

        internal void FindAndCloseTearoffWindowByDockRoot (DockZoneViewModel dockZoneViewModel, bool closeSilently)
        {
            var window = this.Windows.FirstOrDefault(w => DockWindowConfig.GetDockingTearoffWindowRootZone(w) == dockZoneViewModel);
            if (window != null && !m_currentlyClosingWindows.Contains(window))
            {
                if (closeSilently)
                {
                    m_windowsToCloseSilently.Add(window);
                }

                try
                {
                    window.Close();
                }
                finally
                {
                    m_currentlyClosingWindows.Remove(window);
                }
                this.Windows.Root.Focus();
            }
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

        private void DockZone_OnSizeChanged (object sender, SizeChangedEventArgs e)
        {
            this.TriggerLayoutAutoSave();
        }

        private static IDockableDisplayElement BuildDefaultDisplayFor (Type elementType)
        {
            return AJutActivator.CreateInstanceOf(elementType) as IDockableDisplayElement;
        }

        // ==============================[ Sub Classes ]======================================

        private class DisplayBuilder
        {
            public bool IsSingleInstanceOnly { get; init; }
            public Func<IDockableDisplayElement> Builder { get; init; }
        }
    }
}
