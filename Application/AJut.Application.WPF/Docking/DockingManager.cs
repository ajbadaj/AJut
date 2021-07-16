namespace AJut.Application.Docking
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Windows;
    using AJut.Application.Controls;
    using AJut.Storage;
    using AJut.Tree;
    using AJut.TypeManagement;

    public class DockingManager
    {
        private readonly Dictionary<Type, DisplayBuilder> m_factory = new Dictionary<Type, DisplayBuilder>();
        private readonly ObservableCollection<DockZone> m_rootDockZones = new ObservableCollection<DockZone>();

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
            this.Windows = new WindowManager(rootWindow);
            this.UniqueId = uniqueId;
            this.DefaultLayoutStorageFilePath = defaultLayoutStorageFilePath ?? DockingSerialization.CreateApplicationPath(this.UniqueId);
            this.AutoSaveDockLayout = autoSave;
        }

        public void RegisterRootDockZones (params DockZone[] dockZones)
        {
            foreach (var zone in dockZones)
            {
                m_rootDockZones.Add(zone);
                zone.Manager = this;
            }
        }

        public void Register<T> (Action<T> customFactory = null, bool singleInstanceOnly = false)
        {
            var builder = new DisplayBuilder
            {
                IsSingleInstanceOnly = singleInstanceOnly,
                Builder = customFactory != null ? () => customFactory as IDockableDisplayElement : () => BuildDefaultDisplayFor(typeof(T))
            };

            m_factory.Add(typeof(T), builder);
        }

        public T BuildNewDisplayElement<T> () where T : IDockableDisplayElement
        {
            return (T)BuildNewDisplayElement(typeof(T));
        }

        public Func<Window> CreateNewWindowHandler { get; set; } = () => new DefaultDockTearoffWindow();

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

        internal Result<Window> DoTearOff (IDockableDisplayElement element, Point newWindowOrigin)
        {
            Window window = null;
            DockZone newZone = null;

            try
            {
                Size previousZoneSize = new Size(element.DockingAdapter.Location.ActualWidth, element.DockingAdapter.Location.ActualHeight);

                // Step 1: Cleanup
                //  Remove element from zone
                if (!element.DockingAdapter.Location.TryHandleRemovePanel(element.DockingAdapter))
                {
                    var result = Result<Window>.Error("DockingManager: Tear off failed at panel closing");
                    Logger.LogError(result.GetErrorReport());
                    return result;
                }

                // Step 2: Create the new dock zone and track it in the system
                newZone = new DockZone();
                newZone.Manager = this;
                m_rootDockZones.Add(newZone);


                // Step 3: Create the new window and track it in the system
                window = this.CreateNewWindowHandler();
                DockWindow.SetIsDockingTearoffWindow(window, true);
                this.Windows.Track(window);
                window.Content = newZone;
                window.WindowStartupLocation = WindowStartupLocation.Manual;
                window.Left = newWindowOrigin.X;
                window.Top = newWindowOrigin.Y;
                window.Width = previousZoneSize.Width;
                window.Height = previousZoneSize.Height;
                window.Show();

                // Step 4: Now that the new stuff is shown, finalize element & zone setup
                element.DockingAdapter.SetNewLocation(newZone);
                newZone.Add(element);

                return Result<Window>.Success(window);
            }
            catch (Exception exc)
            {
                // There is probably more to do - but for now, at least make sure stray windows aren't left open
                if (window != null && window.IsActive)
                {
                    window.Close();
                }

                if (newZone != null)
                {
                    newZone.Manager = null;
                    m_rootDockZones.Remove(newZone);
                }

                var result = Result<Window>.Error("DockingManager: Window tearoff failed for unknown reason");
                result.AddError(exc.ToString());
                Logger.LogError(result.GetErrorReport(), exc);
                return result;
            }
        }

        internal Result<Window> DoGroupTearOff (DockZone sourceZone, Point newWindowOrigin)
        {
            return this.DoGroupTearOff(sourceZone, newWindowOrigin, sourceZone.RenderSize);
        }

        internal Result<Window> DoGroupTearOff (DockZone sourceZone, Point newWindowOrigin, Size previousZoneSize)
        {
            Window window = null;

            try
            {
                // Step 1: Cleanup
                //  Remove zone properly from hierarchy
                if (sourceZone.HasParentZone)
                {
                    sourceZone.HandlePreTearOff();

                    // If it was a root zone, just remove it because we're about to re-add it in a sec
                    m_rootDockZones.Remove(sourceZone);
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
                    sourceZone.HandlePreTearOff();
                }


                // Step 2: Create the new window and track it in the system
                window = this.CreateNewWindowHandler();
                DockWindow.SetIsDockingTearoffWindow(window, true);
                this.Windows.Track(window);

                // Step 3: Set up new window with zone
                m_rootDockZones.Add(sourceZone);
                window.Content = sourceZone;
                window.WindowStartupLocation = WindowStartupLocation.Manual;
                window.Left = newWindowOrigin.X;
                window.Top = newWindowOrigin.Y;
                window.Width = previousZoneSize.Width;
                window.Height = previousZoneSize.Height;
                window.Show();

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
