namespace AJut.Application.Docking
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Windows;
    using AJut.Application.Controls;
    using AJut.Tree;
    using AJut.TypeManagement;

    public class DockingManager
    {
        private readonly Dictionary<Type, DisplayBuilder> m_factory = new Dictionary<Type, DisplayBuilder>();
        private readonly ObservableCollection<DockZone> m_rootDockZones = new ObservableCollection<DockZone>();

        public string UniqueId { get; }
        public string DefaultLayoutStorageFilePath { get; set; }
        public WindowManager Windows { get; }

        public DockingManager (string uniqueId, string defaultLayoutStorageFilePath = null)
        {
            this.UniqueId = uniqueId;
            this.DefaultLayoutStorageFilePath = defaultLayoutStorageFilePath ?? DockingSerialization.CreateApplicationPath(this.UniqueId);
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

        public T BuildNewDisplayElement<T>() where T : IDockableDisplayElement
        {
            return (T)BuildNewDisplayElement(typeof(T));
        }

        public Func<Window> CreateNewWindowHandler { get; set; } = () => new Window();

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

        public void SetupDisplayElement(IDockableDisplayElement element)
        {

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
