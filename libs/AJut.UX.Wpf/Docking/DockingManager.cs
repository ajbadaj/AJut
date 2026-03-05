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

    /* TODO: Auto save
     * Notes...
     * Enable auto save (bool, notify property changed)
     * Auto save on add/remove/resize panels
     * The one tricky part, this will be affected by window size, so saving that might be important as well, how to do that?
     */

    /// <summary>
    /// The centralized manager for a single docking experience
    /// </summary>
    public class DockingManager : NotifyPropertyChanged, IDockingManager
    {
        private readonly Dictionary<Type, DisplayBuilder> m_factory = new Dictionary<Type, DisplayBuilder>();
        private readonly ObservableCollection<DockZone> m_rootDockZones = new ObservableCollection<DockZone>();
        private readonly MultiMap<Window, DockZone> m_dockZoneMapping = new MultiMap<Window, DockZone>();
        private bool m_isZoneDragDropUnderway;
        private bool m_isReadyToTrackAutoSave = false;

        private readonly List<Window> m_currentlyClosingWindows = new List<Window>();
        private readonly List<Window> m_windowsToCloseSilently = new List<Window>();
        private readonly List<UIElement> m_commandSources = new List<UIElement>();

        /// <summary>
        /// Construct a new <see cref="DockingManager"/> instance.
        /// </summary>
        /// <param name="rootWindow">The main window that the manager will track</param>
        /// <param name="uniqueId">The unique id (human readability is optional, it will form the default layout storage file path if none is specified, so it should also be file safe)</param>
        /// <param name="persistentStorageFilePath">The (optionally specified) path to the default state save for the docking experience in which this manager represents</param>
        /// <param name="autoSaveMethod">How should the manager auto save to the default state storage path as layout changes occur (default = <see cref="eDockingAutoSaveMethod.None"/>)</param>
        public DockingManager (Window rootWindow, string uniqueId, string persistentStorageFilePath = null, eDockingAutoSaveMethod autoSaveMethod = eDockingAutoSaveMethod.None)
        {
            this.UISyncVM = new DockPanelAddRemoveUISync();
            this.UISyncVM.SetManager(this);
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

        public DockPanelAddRemoveUISync UISyncVM { get; }

        /// <summary>Minimum pixel dimension for dock zone panels during size recalculation. Default: 50.</summary>
        public double MinPanelDimension { get; set; } = 50.0;

        // ======================[ Menu Management ]=========================

        /// <summary>
        /// Populate a <see cref="MenuItem"/> with toggle entries for each registered
        /// panel type. Checked items indicate visible panels; clicking a toggle item
        /// shows/hides the panel.
        /// </summary>
        public void ManageMenu (System.Windows.Controls.MenuItem menuItem)
        {
            this.RebuildMenuItems(menuItem);
            this.UISyncVM.PanelStateChanged += (s, e) => this.UpdateMenuCheckedStates(menuItem);
            ((System.Collections.Specialized.INotifyCollectionChanged)this.UISyncVM.PanelTypeEntries)
                .CollectionChanged += (s, e) => this.RebuildMenuItems(menuItem);
        }

        private void RebuildMenuItems (System.Windows.Controls.MenuItem menuItem)
        {
            menuItem.Items.Clear();
            foreach (var desc in this.UISyncVM.GenerateMenuDescriptors())
            {
                var item = new System.Windows.Controls.MenuItem
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

        private void UpdateMenuCheckedStates (System.Windows.Controls.MenuItem menuItem)
        {
            foreach (var item in menuItem.Items)
            {
                if (item is System.Windows.Controls.MenuItem mi && mi.Tag is Type panelType)
                {
                    DockPanelTypeEntry entry = this.UISyncVM.FindEntry(panelType);
                    if (entry != null)
                    {
                        mi.IsChecked = entry.HasActiveInstance;
                    }
                }
            }
        }

        private void OnManagedMenuItemClicked (object sender, System.Windows.RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.MenuItem mi && mi.Tag is Type panelType)
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

        // ======================[ Interface Methods ]=======================

        /// <summary>
        /// Register root <see cref="DockZone"/>(s) that are fixed dock locations. Each DockingManager requires at least one root level zone.
        /// </summary>
        public void RegisterRootDockZones (params DockZone[] dockZones)
        {
            this.RegisterRootDockZones(this.Windows.Root, dockZones);
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
            this.RegisterDisplayFactory<T>(
                new DockPanelRegistrationRules { SingleInstanceOnly = singleInstanceOnly },
                customFactory
            );
        }

        public void RegisterDisplayFactory<T> (DockPanelRegistrationRules rules, Func<T> customFactory = null)
            where T : IDockableDisplayElement
        {
            m_factory[typeof(T)] = new DisplayBuilder
            {
                Rules = rules,
                Builder = customFactory != null ? () => customFactory() as IDockableDisplayElement : () => BuildDefaultDisplayFor(typeof(T))
            };

            this.UISyncVM.AddEntry(typeof(T), rules);
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

        public IEnumerable<DockZoneViewModel> GetAllRoots()
        {
            return m_rootDockZones.Select(z => z.ViewModel);
        }

        /// <summary>
        /// Close all zones out, going through the effort first of asking them if that's ok prior to closing them
        /// </summary>
        /// <returns>True if all zones successfully closed, false otherwise</returns>
        public bool CloseAll (bool force = false)
        {
            if (force)
            {
                m_windowsToCloseSilently.AddRange(this.Windows);
            }
            else
            {
                List<DockingContentAdapterModel> all = m_rootDockZones.SelectMany(z => TreeTraversal<DockZone>.All(z)).SelectMany(z => _DockedContentOrEmptyFor(z?.ViewModel)).ToList();

                bool anyDissenters = false;
                foreach (var adapter in all)
                {
                    if (!adapter.CheckCanClose())
                    {
                        var closeSupression = new RoutedEventArgs(DockZone.NotifyCloseSupressionEvent);
                        ((DockZone)adapter.Location.UI).RaiseEvent(closeSupression);
                        anyDissenters = true;
                    }
                }

                if (anyDissenters)
                {
                    return false;
                }
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
                draggerWindow.Style = null;

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

                foreach (DockZone rootZones in this.Windows.Where(w => w != dragSourceWindow).SelectMany(w => m_dockZoneMapping.TryGetValues(w, out List<DockZone> zones) ? zones : Enumerable.Empty<DockZone>()))
                {
                    var result = VisualTreeHelper.HitTest(rootZones, Mouse.PrimaryDevice.GetPosition(rootZones));
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
            DockZoneSize dockSize = element.DockingAdapter.Location.UI?.RenderSize ?? DockZoneSize.Empty;
            Size previousZoneSize = new Size(dockSize.Width, dockSize.Height);
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
                // Check if tearing off will orphan the source tearoff window
                DockZoneViewModel sourceLocation = element.DockingAdapter.Location;

                // Remove element from zone
                if (!sourceLocation.RemoveDockedContent(element.DockingAdapter))
                {
                    var result = Result<Window>.Error("DockingManager: Tear off failed at panel closing");
                    Logger.LogError(result.GetErrorReport());
                    return result;
                }

                // Create the new dock zone & window
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
                    var window = Window.GetWindow((DockZone)sourceZone.UI);
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

        public DockZoneViewModel FindFirstAvailableDockZone ()
        {
            foreach (DockZoneViewModel zone in m_rootDockZones.SelectMany(z => TreeTraversal<DockZone>.All(z)).Select(z => z.ViewModel))
            {
                if (zone.Orientation.IsFlagInGroup(eDockOrientation.AnyLeafDisplay))
                {
                    return zone;
                }
            }

            Logger.LogError($"DockingManager may be setup wrong, tried to find first available dock zone and couldn't find a leaf zone");
            return null;
        }

        public DockZoneViewModel FindFirstAvailableDockZoneForGroup (string groupId)
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

        public void AddPanel (Type panelType) => this.UISyncVM.AddPanel(panelType);
        public void TogglePanel (Type panelType) => this.UISyncVM.ShowPanel(panelType);
        public void RemoveOrHidePanel (DockingContentAdapterModel adapter) => this.UISyncVM.CloseOrHidePanel(adapter);

        // ===========[ IDockingManager Platform Hooks ]========================

        public DockPanelRegistrationRules? GetPanelRules (Type panelType)
        {
            return m_factory.TryGetValue(panelType, out DisplayBuilder builder) ? builder.Rules : null;
        }

        public DockZoneViewModel FindTargetZoneForGroup (string groupId)
        {
            return this.FindFirstAvailableDockZoneForGroup(groupId);
        }

        public HiddenPanelPlatformState CaptureHideState (DockingContentAdapterModel adapter)
        {
            DockZone dockZoneUI = adapter.Location?.UI as DockZone;
            Window hostWindow = dockZoneUI != null ? Window.GetWindow(dockZoneUI) : null;
            bool wasInTearoff = hostWindow != null && DockWindowConfig.GetIsDockingTearoffWindow(hostWindow);

            if (!wasInTearoff)
            {
                return null;
            }

            return new HiddenPanelPlatformState
            {
                WasInTearoff = true,
                TearoffX = hostWindow.Left,
                TearoffY = hostWindow.Top,
                TearoffWidth = hostWindow.Width,
                TearoffHeight = hostWindow.Height,
                TearoffWindowRef = hostWindow,
            };
        }

        public bool TryRestoreFromHideState (object hideState, DockingContentAdapterModel adapter)
        {
            if (hideState is not HiddenPanelPlatformState state || !state.WasInTearoff)
            {
                return false;
            }

            // Check if the same tearoff window is still alive and tracked
            if (state.TearoffWindowRef is Window window
                && m_dockZoneMapping.TryGetValues(window, out List<DockZone> zones)
                && zones.Count > 0)
            {
                DockZoneViewModel tearoffRoot = DockWindowConfig.GetDockingTearoffWindowRootZone(window);
                if (tearoffRoot != null)
                {
                    tearoffRoot.AddDockedContent(adapter);
                    return true;
                }
            }

            return false;
        }

        public void AfterPanelHidden (object hideState)
        {
            if (hideState is not HiddenPanelPlatformState state || !state.WasInTearoff)
            {
                return;
            }

            if (state.TearoffWindowRef is Window hostWindow)
            {
                // Check if the tearoff is now empty and should be closed
                var rootZone = DockWindowConfig.GetDockingTearoffWindowRootZone(hostWindow);
                if (rootZone != null && !rootZone.DockedContent.Any()
                    && (rootZone.Orientation == eDockOrientation.Empty || rootZone.Orientation.IsFlagInGroup(eDockOrientation.AnyLeafDisplay)))
                {
                    m_windowsToCloseSilently.Add(hostWindow);
                    try
                    {
                        m_dockZoneMapping.RemoveAllFor(hostWindow);
                    }
                    catch (Exception ex)
                    {
                        // Windows is very finnicky about trying to close or change properties while closing is already
                        //  happening. We're trying to safeguard around this problem, but it also means close isn't happening
                        //  because close is already happening, which makes this less of a worry
                        Logger.LogError(ex);
                    }
                    finally
                    {
                        m_windowsToCloseSilently.Remove(hostWindow);
                    }
                }
            }
        }

        public bool CreateTearoffForPanel (DockingContentAdapterModel adapter, double x, double y, double width, double height)
        {
            if (x < 0)
            {
                x = this.Windows.Root.Left;
            }
            if (y < 0)
            {
                y = this.Windows.Root.Top;
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

        public bool IsTearoffRootThatWouldOrphan (DockZoneViewModel zone)
        {
            if (zone == null)
            {
                return false;
            }

            foreach (var kv in m_dockZoneMapping)
            {
                Window window = kv.Key;
                if (!DockWindowConfig.GetIsDockingTearoffWindow(window))
                {
                    continue;
                }

                DockZoneViewModel rootZone = DockWindowConfig.GetDockingTearoffWindowRootZone(window);
                if (rootZone == zone)
                {
                    bool hasOnlyOneContent = zone.DockedContent.Count <= 1
                        && zone.Children.Count == 0;
                    return hasOnlyOneContent;
                }
            }

            return false;
        }

        public void CloseTearoffForRootZone (DockZoneViewModel rootZone)
        {
            foreach (var kv in m_dockZoneMapping)
            {
                Window window = kv.Key;
                if (m_windowsToCloseSilently.Contains(window))
                {
                    continue;
                }

                DockZoneViewModel rz = DockWindowConfig.GetDockingTearoffWindowRootZone(window);
                if (rz == rootZone)
                {
                    m_windowsToCloseSilently.Add(window);
                    try
                    {
                        window.Close();
                        m_dockZoneMapping.RemoveAllFor(window);
                    }
                    finally
                    {
                        m_windowsToCloseSilently.Remove(window);
                    }

                    return;
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

            var adapter = new DockingContentAdapterModel(this);
            adapter.TitleContent = StringXT.ConvertToFriendlyEn(displayElement.GetType().Name);
            displayElement.Setup(adapter);
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
                window.CommandBindings.AddEach(this.EnumerateAllCommandBindings());
                DockWindowConfig.SetDockingTearoffWindowRootZone(window, rootZone);
                this.Windows.Track(window);

                DockZone newRoot = new DockZone() { ViewModel = rootZone };
                window.Content = newRoot;
                window.WindowStartupLocation = WindowStartupLocation.Manual;
                window.Left = newWindowOrigin.X;
                window.Top = newWindowOrigin.Y;
                window.Width = previousZoneSize.Width;
                window.Height = previousZoneSize.Height;
                window.Closing += this.OnDockTearoffWindowClosing;
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
                    try
                    {
                        this.UISyncVM.CloseOrHidePanel(adapter);
                    }
                    catch(Exception ex)
                    {
                        Logger.LogError(ex);
                        throw;
                    }
                }

                // Close remaining (non-hideable) panels normally
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
                m_windowsToCloseSilently.Remove(window);
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

        public void AddCommandSource (UIElement source)
        {
            if (source != null)
            {
                m_commandSources.Add(source);
            }
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

        // ==============================[ Sub Classes ]======================================

        private class DisplayBuilder
        {
            public DockPanelRegistrationRules Rules { get; init; }
            public bool IsSingleInstanceOnly => this.Rules.SingleInstanceOnly;
            public Func<IDockableDisplayElement> Builder { get; init; }
        }

    }
}
