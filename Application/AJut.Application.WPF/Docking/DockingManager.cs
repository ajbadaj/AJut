namespace AJut.Application.Docking
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Input;
    using System.Windows.Media;
    using AJut.Application.Controls;
    using AJut.Storage;
    using AJut.Tree;
    using AJut.TypeManagement;

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
                if (zone.IsSetup)
                {
                    zone.Clear();
                }

                m_rootDockZones.Add(zone);
                zone.Manager = this;
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

        public DockZone GenerateDockZone ()
        {
            return new DockZone
            {
                Manager = this,
            };
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
            var all = m_rootDockZones.SelectMany(z => TreeTraversal<DockZone>.All(z)).SelectMany(z => z.LocallyDockedElements).ToList();
            foreach (var adapter in all)
            {
                if (!adapter.CheckCanClose())
                {
                    var closeSupression = new RoutedEventArgs(DockZone.NotifyCloseSupressionEvent);
                    adapter.Location.RaiseEvent(closeSupression);
                    anyDissenters = true;
                }
            }

            if (anyDissenters)
            {
                return false;
            }


            this.Windows.CloseAllWindows();
            return true;
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
            DockZone currentDropTarget = null;

            try
            {
                this.IsZoneInDragDropMode = true;
                await dragSourceWindow.AsyncDragMove(onMove: _WindowLocationChanged);

                if (currentDropTarget != null)
                {
                    sourceDockZone.DropContentInto(currentDropTarget, eDockOrientation.Horizontal, false);
                    if (DockWindow.GetIsDockingTearoffWindow(dragSourceWindow))
                    {
                        int numZones = dragSourceWindow.GetVisualChildren().OfType<DockZone>().Count();
                        if (numZones == 0 || numZones == 1)
                        {
                            dragSourceWindow.Close();
                        }
                    }
                }
            }
            finally
            {
                this.IsZoneInDragDropMode = false;
            }

            void _WindowLocationChanged ()
            {
                currentDropTarget = null;

                foreach (var window in this.Windows.Where(w => w != dragSourceWindow))
                {
                    var result = VisualTreeHelper.HitTest(window, Mouse.PrimaryDevice.GetPosition(window));
                    if (result?.VisualHit != null)
                    {
                        var hitZone = result.VisualHit.GetFirstParentOf<DockZone>();
                        if (hitZone != null)
                        {
                            currentDropTarget = hitZone;
                            return;
                        }
                    }
                }
            }
        }

        public Result<Window> DoTearoff (IDockableDisplayElement element, Point newWindowOrigin)
        {
            DockZone newZone = null;

            try
            {
                Size previousZoneSize = new Size(element.DockingAdapter.Location.ActualWidth, element.DockingAdapter.Location.ActualHeight);

                // Cleanup: Remove element from zone
                if (!element.DockingAdapter.Location.TryHandleRemovePanel(element.DockingAdapter))
                {
                    var result = Result<Window>.Error("DockingManager: Tear off failed at panel closing");
                    Logger.LogError(result.GetErrorReport());
                    return result;
                }

                // Create the new dock zone & window, doing all appropriate tracking adds
                newZone = new DockZone();
                var windowResult = this.CreateAndStockTearoffWindow(newZone, newWindowOrigin, previousZoneSize);
                if (windowResult)
                {
                    element.DockingAdapter.SetNewLocation(newZone);
                    newZone.Add(element);
                }

                return windowResult;
            }
            catch (Exception exc)
            {
                // There is probably more to do - but for now, at least make sure stray windows aren't left open
                if (newZone != null)
                {
                    this.DeRegisterRootDockZones(newZone);
                    this.RegisterRootDockZones(newZone);
                }

                var result = Result<Window>.Error("DockingManager: Window tearoff failed for unknown reason");
                result.AddError(exc.ToString());
                Logger.LogError(result.GetErrorReport(), exc);
                return result;
            }
        }

        public Result<Window> DoGroupTearoff (DockZone sourceZone, Point newWindowOrigin)
        {
            return this.DoGroupTearoff(sourceZone, newWindowOrigin, sourceZone.RenderSize);
        }

        public Result<Window> DoGroupTearoff (DockZone sourceZone, Point newWindowOrigin, Size previousZoneSize)
        {
            try
            {
                // Step 1: Cleanup
                //  Remove zone properly from hierarchy
                if (sourceZone.HasParentZone)
                {
                    sourceZone.HandlePreTearoff();
                    this.DeRegisterRootDockZones(sourceZone);
                }
                else
                {
                    // We have a scenario where tearing off a root zone is actually bad and create an unexpected
                    //  flow - so instead move all current stuff down into a new zone, and essentially tear that
                    //  off instead
                    Result<DockZone> zoneResult = sourceZone.InsertAndReparentAllChildrenOnToNewZone();
                    if (zoneResult.HasErrors)
                    {
                        Logger.LogError("DockingManager: DockZone reparent for root failed");
                        return new Result<Window>(zoneResult);
                    }

                    sourceZone = zoneResult.Value;
                    sourceZone.HandlePreTearoff();
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

        private Result<Window> CreateAndStockTearoffWindow (DockZone rootZone, Point newWindowOrigin, Size previousZoneSize)
        {
            Window window = null;
            try
            {
                window = this.CreateNewTearoffWindowHandler();
                DockWindow.SetIsDockingTearoffWindow(window, true);
                this.Windows.Track(window);

                window.Content = rootZone;
                window.WindowStartupLocation = WindowStartupLocation.Manual;
                window.Left = newWindowOrigin.X;
                window.Top = newWindowOrigin.Y;
                window.Width = previousZoneSize.Width;
                window.Height = previousZoneSize.Height;
                this.ShowTearoffWindowHandler(window);
                this.RegisterRootDockZones(rootZone);
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
