namespace AJut.UX.Docking
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Diagnostics;
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
    using AJut.UX.Controls;

    public class DockingManager : NotifyPropertyChanged
    {
        private readonly Dictionary<Type, DisplayBuilder> m_factory = new Dictionary<Type, DisplayBuilder>();
        private readonly ObservableCollection<DockZone> m_rootDockZones = new ObservableCollection<DockZone>();
        private readonly MultiMap<Window, DockZone> m_dockZoneMapping = new MultiMap<Window, DockZone>();

        public string UniqueId { get; }
        public string DefaultLayoutStorageFilePath { get; set; }
        public WindowManager Windows { get; }
        public bool AutoSaveDockLayout { get; set; }

        /* 
         * Enable auto save (bool, notify property changed)
         * Auto save on add/remove/resize panels
         * The one tricky part, this will be affected by window size, so saving that might be important as well, how to do that?
         */

        public DockingManager (Window rootWindow, string uniqueId, string defaultLayoutStorageFilePath = null, bool autoSave = false)
        {
            this.CreateNewTearoffWindowHandler = () => new DefaultDockTearoffWindow(this);
            this.ShowTearoffWindowHandler = w => w.Show();
            this.Windows = new WindowManager(rootWindow);
            this.UniqueId = uniqueId;
            this.DefaultLayoutStorageFilePath = defaultLayoutStorageFilePath ?? DockingSerialization.CreateApplicationPath(this.UniqueId);
            this.AutoSaveDockLayout = autoSave;
        }

        private bool m_isZoneInDragDropMode;
        public bool IsZoneInDragDropMode
        {
            get => m_isZoneInDragDropMode;
            private set => this.SetAndRaiseIfChanged(ref m_isZoneInDragDropMode, value);
        }

        public void RegisterRootDockZones (params DockZone[] dockZones)
        {
            foreach (var zone in dockZones)
            {
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

                m_dockZoneMapping.Add(this.Windows.Root, zone);
            }
        }

        public void DeRegisterRootDockZones (params DockZone[] dockZones)
        {
            foreach (var zone in dockZones)
            {
                m_rootDockZones.Remove(zone);
                zone.Manager = null;
                m_dockZoneMapping.RemoveAllValues(zone);
            }
        }

        public void RegisterDisplayFactory<T> (Func<T> customFactory = null, bool singleInstanceOnly = false)
        {
            var builder = new DisplayBuilder
            {
                IsSingleInstanceOnly = singleInstanceOnly,
                Builder = customFactory != null ? () => customFactory() as IDockableDisplayElement : () => BuildDefaultDisplayFor(typeof(T))
            };

            m_factory.Add(typeof(T), builder);
        }

        public T BuildNewDisplayElement<T> () where T : IDockableDisplayElement
        {
            return (T)BuildNewDisplayElement(typeof(T));
        }

        public Func<Window> CreateNewTearoffWindowHandler { get; set; }
        public Action<Window> ShowTearoffWindowHandler { get; set; }

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

            IEnumerable<DockingContentAdapterModel> _DockedContentOrEmptyFor(DockZoneViewModel vm)
            {
                if ((vm?.DockedContent?.Count ?? 0) == 0)
                {
                    return Enumerable.Empty<DockingContentAdapterModel>();
                }

                return vm.DockedContent;
            }
        }


        public bool SaveState (string filePath = null)
        {
            return DockingSerialization.SerializeStateTo(
                    filePath ?? this.DefaultLayoutStorageFilePath,
                    m_rootDockZones
            );
        }

        public bool ResetFromState (string filePath)
        {
            return DockingSerialization.ResetFromState(
                    filePath ?? this.DefaultLayoutStorageFilePath,
                    m_rootDockZones
            );
        }

        public IDockableDisplayElement BuildNewDisplayElement (Type elementType)
        {
            var displayElement = m_factory.TryGetValue(elementType, out var b)
                                    ? b.Builder()
                                    : BuildDefaultDisplayFor(elementType);

            if (displayElement == null)
            {
                return displayElement;
            }

            displayElement.Setup(new DockingContentAdapterModel(this));
            displayElement.DockingAdapter.Display = displayElement;
            return displayElement;
        }

        public void SetupDisplayElement (IDockableDisplayElement element)
        {

        }

        public void TriggerLayoutAutoSave ()
        {
            if (this.AutoSaveDockLayout)
            {
                this.SaveState();
            }
        }

        public async Task RunDragSearch (Window dragSourceWindow, DockZone sourceDockZone)
        {
            const double kMouseOffset = 20.0;
            DockZone currentDropTarget = null;
            DockZone lastDropTarget = null;
            DockDropInsertionDriverWidget lastInsertionDriver = null;
            try
            {
                this.IsZoneInDragDropMode = true;

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
                }, System.Windows.Threading.DispatcherPriority.Loaded);

                draggerWindow.Content = img;
                draggerWindow.Show();
                try
                {
                    dragSourceWindow.Visibility = Visibility.Collapsed;
                    await draggerWindow.AsyncDragMove(onMove: _WindowLocationChanged);
                    if (Mouse.PrimaryDevice.GetPrimaryButtonState() == MouseButtonState.Pressed)
                    {
                        Debugger.Break();
                    }
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
                                dragSourceWindow.Close();
                            }
                        }
                    }
                }
            }
            finally
            {
                this.IsZoneInDragDropMode = false;
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

        public Result<Window> DoTearoff (IDockableDisplayElement element, Point newWindowOrigin)
        {
            try
            {
                Size previousZoneSize = new Size(element.DockingAdapter.Location.UI.ActualWidth, element.DockingAdapter.Location.UI.ActualHeight);

                // Cleanup: Remove element from zone
                if (!element.DockingAdapter.Location.RemoveDockedContent(element.DockingAdapter))
                {
                    var result = Result<Window>.Error("DockingManager: Tear off failed at panel closing");
                    Logger.LogError(result.GetErrorReport());
                    return result;
                }

                // Create the new dock zone & window, doing all appropriate tracking adds
                var newZone = new DockZoneViewModel(this);
                var windowResult = this.CreateAndStockTearoffWindow(newZone, newWindowOrigin, previousZoneSize);
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

        public Result<Window> DoGroupTearoff (DockZoneViewModel sourceZone, Point newWindowOrigin)
        {
            return this.DoGroupTearoff(sourceZone, newWindowOrigin, sourceZone.UI.RenderSize);
        }

        public Result<Window> DoGroupTearoff (DockZoneViewModel sourceZone, Point newWindowOrigin, Size previousZoneSize)
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

                return this.CreateAndStockTearoffWindow(sourceZone, newWindowOrigin, previousZoneSize);
            }
            catch (Exception exc)
            {
                // There is probably more to do - but for now, at least make sure stray windows aren't left open
                Logger.LogError("Window tearoff failed!", exc);
                return Result<Window>.Error("DockingManager: Window tearoff failed for unknown reason");
            }
        }

        private Result<Window> CreateAndStockTearoffWindow (DockZoneViewModel rootZone, Point newWindowOrigin, Size previousZoneSize)
        {
            Window window = null;
            try
            {
                window = this.CreateNewTearoffWindowHandler();
                DockWindowConfig.SetIsDockingTearoffWindow(window, true);
                this.Windows.Track(window);

                DockZone newRoot = new DockZone() { ViewModel = rootZone };
                window.Content = newRoot;
                window.WindowStartupLocation = WindowStartupLocation.Manual;
                window.Left = newWindowOrigin.X;
                window.Top = newWindowOrigin.Y;
                window.Width = previousZoneSize.Width;
                window.Height = previousZoneSize.Height;
                this.ShowTearoffWindowHandler(window);
                this.RegisterRootDockZones(newRoot);
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

        public void CleanZoneLayoutHierarchies ()
        {
            var toVisit = new Queue<DockZoneViewModel>();
            m_rootDockZones.Select(z=>z.ViewModel).ForEach(toVisit.Enqueue);
            while (toVisit.Count != 0)
            {
                var parentEval = toVisit.Dequeue();
                if (parentEval.Children.Count == 0)
                {
                    continue;
                }

                for (int index = 0; index < parentEval.Children.Count; ++index)
                {
                    var child = parentEval.Children[index];
                    if (child.Orientation == parentEval.Orientation)
                    {
                        parentEval.RunRemoveChildMechanics(child);
                        foreach (var grandchild in child.Children.ToList())
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

        internal void TrackSizingChanges (DockZone dockZone)
        {
            dockZone.SizeChanged -= this.DockZone_OnSizeChanged;
            dockZone.SizeChanged += this.DockZone_OnSizeChanged;
        }

        internal void StopTrackingSizingChanges (DockZone dockZone)
        {
            dockZone.SizeChanged -= this.DockZone_OnSizeChanged;
        }

        private void DockZone_OnSizeChanged (object sender, SizeChangedEventArgs e)
        {
            this.TriggerLayoutAutoSave();
        }

        private static IDockableDisplayElement BuildDefaultDisplayFor (Type elementType)
        {
            return AJutActivator.CreateInstanceOf(elementType) as IDockableDisplayElement;
        }

        private class DisplayBuilder
        {
            public bool IsSingleInstanceOnly { get; init; }
            public Func<IDockableDisplayElement> Builder { get; init; }
        }
    }
}
