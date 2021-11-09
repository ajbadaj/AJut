namespace AJut.UX.Docking
{
    using System;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Windows;
    using AJut.Tree;
    using AJut.TypeManagement;
    using AJut.UX.Controls;

    [Flags]
    public enum eDockOrientation
    {
        /// <summary>
        /// An empty zone, contains nothing
        /// </summary>
        Empty = 0b0000,

        /// <summary>
        /// Split Orientation: Horizontal - container of an anterior zone on the left and a posterior zone on the right
        /// </summary>
        Horizontal = 0b0001,

        /// <summary>
        /// Split Orientation: Vertical - container of an anterior zone on top and a posterior zone below
        /// </summary>
        Vertical = 0b0010,

        /// <summary>
        /// Leaf orientation: Single element to display
        /// </summary>
        Single = 0b0100,

        /// <summary>
        /// Leaf orientaiton: more than one element to display
        /// </summary>
        Tabbed = 0b1000,

        /// <summary>
        /// Either horizontal or vertical, this orientation covers a zone who is only a container for two either vertically or horizontally split sub zones
        /// </summary>
        AnySplitOrientation = Horizontal | Vertical,

        /// <summary>
        /// The leaf orientations contain elements to display, alone if only a single one, or in tabs if more than 1
        /// </summary>
        AnyLeafDisplay = Single | Tabbed | Empty,
    }

    /// <summary>
    /// The view model used to populate a <see cref="DockZone"/>
    /// </summary>
    public sealed class DockZoneViewModel : NotifyPropertyChanged
    {
        private readonly ObservableCollection<DockingContentAdapterModel> m_dockedContent = new ObservableCollection<DockingContentAdapterModel>();
        private readonly ObservableCollection<DockZoneViewModel> m_children = new ObservableCollection<DockZoneViewModel>();

        // =======================[ Construction/Configuration ]=====================================
        static DockZoneViewModel ()
        {
            TreeTraversal<DockZoneViewModel>.SetupDefaults(dzvm => dzvm.Children, dzvm => dzvm.Parent);
        }
        private DockZoneViewModel ()
        {
            this.DockedContent = new ReadOnlyObservableCollection<DockingContentAdapterModel>(m_dockedContent);
            this.Children = new ReadOnlyObservableCollection<DockZoneViewModel>(m_children);
        }

        public DockZoneViewModel (DockingManager manager) : this()
        {
            this.Manager = manager;
        }
        internal DockZoneViewModel (DockingSerialization.ZoneData zoneData) : this()
        {
            this.BuildFromState(zoneData);
        }

        private static int g_trackingMoniker = 0;
        private string m_trackingMoniker = $"Dock Zone VM: #{g_trackingMoniker++}";
        
        /// <summary>
        /// The tracking moniker is an optional name given used to better identify and debug issues. It is not guranteed unique, and can
        /// be set to any string the user of this api would like. If present, it will override the ToString implementation as well.
        /// </summary>
        public string TrackingMoniker
        {
            get => m_trackingMoniker;
            set => this.SetAndRaiseIfChanged(ref m_trackingMoniker, value);
        }

        public override string ToString () => this.TrackingMoniker != null ? this.TrackingMoniker : base.ToString();

        // =======================[ Properties ]=====================================

        private DockingManager m_manager;
        public DockingManager Manager
        {
            get => m_manager;
            set => this.SetAndRaiseIfChanged(ref m_manager, value);
        }

        private DockZoneViewModel m_parent;
        public DockZoneViewModel Parent
        {
            get => m_parent;
            set => this.SetAndRaiseIfChanged(ref m_parent, value, nameof(Parent), nameof(HasParent));
        }
        public bool HasParent => m_parent != null;

        public ReadOnlyObservableCollection<DockingContentAdapterModel> DockedContent { get; }
        public ReadOnlyObservableCollection<DockZoneViewModel> Children { get; }

        private eDockOrientation m_orientation = eDockOrientation.Empty;
        public eDockOrientation Orientation
        {
            get => m_orientation;
            private set => this.SetAndRaiseIfChanged(ref m_orientation, value);
        }

        private int m_selectedIndex;
        public int SelectedIndex
        {
            get => m_selectedIndex;
            set => this.SetAndRaiseIfChanged(ref m_selectedIndex, value);
        }

        private GridLength m_sizeOnParent = new GridLength(1.0, GridUnitType.Star);
        public GridLength SizeOnParent
        {
            get => m_sizeOnParent;
            set => this.SetAndRaiseIfChanged(ref m_sizeOnParent, value);
        }

        private DockZone m_ui;
        public DockZone UI
        {
            get => m_ui;
            set
            {
                DockZone oldUI = m_ui;
                if (oldUI != null && oldUI.ViewModel == this)
                {
                    oldUI.ViewModel = null;
                }

                this.SetAndRaiseIfChanged(ref m_ui, value);
            }
        }

        // =======================[ Public API Methods ]=====================================

        public void CopyIntoAndClear (DockZoneViewModel dockZone)
        {
            // Step 1) Copy all the things we're about to steal so there are no parenting issues.
            var locallyDockedElementsCopy = m_dockedContent.ToList();
            var childZonesCopy = m_children.ToList();
            var orientation = this.Orientation;
            var selectedIndex = this.SelectedIndex;

            // Step 2) Clear in preparation, otherwise there may be some issues with visual/logical parents
            //          still being set, throwing exceptions
            this.Clear();
            dockZone.Clear();

            // Step 3) Set all the things in dockZone with the things we set aside earlier
            locallyDockedElementsCopy.ForEach(c => dockZone.AddDockedContent(c));
            childZonesCopy.ForEach(c => dockZone.AddChild(c));
            dockZone.Orientation = orientation;
            dockZone.SelectedIndex = selectedIndex;
        }

        public DockZoneViewModel DuplicateAndClear ()
        {
            var dupe = new DockZoneViewModel();
            dupe.m_dockedContent.AddEach(m_dockedContent);
            dupe.m_children.AddEach(m_children);
            dupe.Orientation = this.Orientation;
            dupe.SizeOnParent = this.SizeOnParent;
            foreach (var dockedContent in dupe.DockedContent)
            {
                dockedContent.SetNewLocation(dupe);
            }

            this.Clear();
            return dupe;
        }

        public void Clear ()
        {
            foreach (var dockedContent in m_dockedContent.Where(c => c.Location == this))
            {
                dockedContent.SetNewLocation(null);
            }

            foreach (var child in m_children)
            {
                child.Parent = null;
            }

            m_dockedContent.Clear();
            m_children.Clear();
            this.Orientation = eDockOrientation.Empty;
        }

        public void Configure (eDockOrientation orientation)
        {
            if (orientation == eDockOrientation.Empty)
            {
                this.Clear();
            }
            else if (orientation.IsFlagInGroup(eDockOrientation.AnyLeafDisplay))
            {
                m_children.Clear();
            }
            else if (orientation.IsFlagInGroup(eDockOrientation.AnySplitOrientation))
            {
                m_dockedContent.Clear();
            }

            this.Orientation = orientation;
        }

        public bool AddChild (DockZoneViewModel child)
        {
            return this.InsertChild(m_children.Count, child);
        }

        public bool InsertChild (int index, DockZoneViewModel child)
        {
            if (!this.Orientation.IsFlagInGroup(eDockOrientation.AnySplitOrientation))
            {
                Logger.LogError("Attempted to add child zone to a non-split display (leaf display)");
                return false;
            }

            child.Parent?.RunRemoveChildMechanics(child);
            child.Parent = this;
            child.Manager = this.Manager;
            m_children.Insert(index, child);
            return true;
        }

        public bool RemoveChild (DockZoneViewModel child)
        {
            bool result = this.RunRemoveChildMechanics(child);
            if (result && m_children.Count == 1)
            {
                var lastRemainingChild = m_children[0];
                if (this.Parent != null)
                {
                    this.Parent.InsertChild(this.Parent.Children.IndexOf(this), lastRemainingChild);
                    this.Parent.RemoveChild(this);
                }
                else
                {
                    lastRemainingChild.CopyIntoAndClear(this);
                }
            }

            return result;
        }

        public void DestroyUIReference ()
        {
            TreeTraversal<DockZoneViewModel>.All(this).ForEach(_DoDestroyReference);

            void _DoDestroyReference (DockZoneViewModel zone)
            {
                if (zone.UI != null)
                {
                    zone.UI.ViewModel = null;
                    zone.UI = null;
                }
            }
        }

        public bool RemoveDockedContent (DockingContentAdapterModel panelAdapter)
        {
            if (m_dockedContent.Contains(panelAdapter))
            {
                return this.DoRemoveContent(panelAdapter);
            }

            return false;
        }

        public bool CloseAndRemoveDockedContent (DockingContentAdapterModel panelAdapter)
        {
            if (m_dockedContent.Contains(panelAdapter) && panelAdapter.Close())
            {
                return this.DoRemoveContent(panelAdapter);
            }

            return false;
        }

        public bool AddDockedContent (IDockableDisplayElement panel)
        {
            return this.AddDockedContent(panel.DockingAdapter);
        }

        public bool AddDockedContent (DockingContentAdapterModel panelAdapter)
        {
            if (!this.Orientation.IsFlagInGroup(eDockOrientation.AnyLeafDisplay))
            {
                Logger.LogError("Attempted to add docked content to non-leaf display");
                return false;
            }

            m_dockedContent.Add(panelAdapter);
            panelAdapter.SetNewLocation(this);
            this.Orientation = m_dockedContent.Count == 1 ? eDockOrientation.Single : eDockOrientation.Tabbed;
            return true;
        }

        public bool DropAddSiblingIntoDock (DockZoneViewModel newSibling, eDockInsertionDirection insertionDirection)
        {
            eDockOrientation orientation;
            int indexOffset = 0;
            switch (insertionDirection)
            {
                case eDockInsertionDirection.Left:
                    orientation = eDockOrientation.Horizontal;
                    break;

                case eDockInsertionDirection.Right:
                    indexOffset = 1;
                    orientation = eDockOrientation.Horizontal;
                    break;

                case eDockInsertionDirection.Top:
                    orientation = eDockOrientation.Vertical;
                    break;

                case eDockInsertionDirection.Bottom:
                    indexOffset = 1;
                    orientation = eDockOrientation.Vertical;
                    break;

                case eDockInsertionDirection.AddToTabbedDisplay:
                    orientation = eDockOrientation.Tabbed;
                    break;

                default:
                    return false;
            }

            // ==== Scenario 1: Insert as tab =====
            if (orientation == eDockOrientation.Tabbed)
            {
                if (this.Orientation.IsFlagInGroup(eDockOrientation.AnyLeafDisplay))
                {
                    // Find all dock display elements and add them as children to this
                    bool addedAnything = false;
                    foreach (DockZoneViewModel descendant in TreeTraversal<DockZoneViewModel>.All(newSibling))
                    {
                        var elements = descendant.m_dockedContent.ToList();
                        addedAnything = addedAnything || elements.Count > 0;
                        descendant.Clear();
                        elements.ForEach(p => this.AddDockedContent(p));
                    }

                    if (addedAnything)
                    {
                        this.SelectedIndex = m_dockedContent.Count - 1;
                    }
                }
                else
                {
                    Logger.LogError("Docking issue: Attempted to drop a dock zone on a non-leaf display (tabbed, single, none), zones must be split first for that to work (otherwise the system would be guessing as to which way to split it)");
                    return false;
                }
            }

            // ==== Scenario 2: Insert into parent's child zones =====
            else if (this.Parent != null && orientation == this.Parent.Orientation)
            {
                int insertIndex = this.Parent.Children.IndexOf(this);
                if (insertIndex == -1)
                {
                    Logger.LogError("Docking issue: child zone could not be found on parent");
                    return false;
                }

                insertIndex += indexOffset;
                this.Parent.InsertChild(insertIndex, newSibling);
            }
            // ==== Scenario 3: Clone and insert as siblings =====
            else
            {
                var dupe = this.DuplicateAndClear();
                this.Orientation = orientation;
                this.InsertChild(0, dupe);
                this.InsertChild(indexOffset, newSibling);
            }

            return true;
        }

        public bool SwapChildOrder (int moveFromIndex, int moveToIndex)
        {
            return m_children.Relocate(moveFromIndex, moveToIndex);
        }

        public void GenerateAndAdd<T> (object state = null) where T : IDockableDisplayElement
        {
            var panel = this.Manager.BuildNewDisplayElement<T>();
            this.AddDockedContent(panel);
            panel.DockingAdapter.FinalizeSetup(state);
        }

        // =======================[ Hidden API Utilities ]=====================================

        internal DockingSerialization.ZoneData GenerateSerializationState ()
        {
            var data = new DockingSerialization.ZoneData
            {
                GroupId = DockZone.GetGroupId(this.UI),
                Orientation = this.Orientation,
            };

            if (this.Orientation == eDockOrientation.Tabbed)
            {
                data.DisplayState = this.DockedContent
                    .Select(
                        adapter => new DockingSerialization.DisplayData
                        {
                            TypeId = TypeIdRegistrar.GetTypeIdFor(adapter.GetType()),
                            State = adapter.Display.GenerateState()
                        }
                    ).ToArray();
            }
            else
            {
                m_children.Select(z => z.GenerateSerializationState()).ForEach(data.ChildZones.Add);
            }

            return data;
        }

        internal void BuildFromState (DockingSerialization.ZoneData data)
        {
            //  Reset
            this.Clear();

            if (data.Orientation == eDockOrientation.Tabbed)
            {
                this.DockedContent.AddEach(data.DisplayState.Select(s => DockingSerialization.BuildDisplayElement(this.Manager, s)).Select(d => d.DockingAdapter));
            }
            else
            {
                data.ChildZones.Select(zd => new DockZoneViewModel(zd)).ForEach(c => this.AddChild(c));
            }

            this.Orientation = data.Orientation;
        }

        private bool DoRemoveContent (DockingContentAdapterModel panelAdapter)
        {
            if (panelAdapter.Location == this)
            {
                panelAdapter.SetNewLocation(null);
            }

            bool result = m_dockedContent.Remove(panelAdapter);
            switch (m_dockedContent.Count)
            {
                case 0:
                    this.Orientation = eDockOrientation.Empty;
                    break;

                case 1:
                    this.Orientation = eDockOrientation.Single;
                    break;
            }

            return result;
        }

        internal bool RunRemoveChildMechanics (DockZoneViewModel child)
        {
            if (m_children.Remove(child))
            {
                child.DestroyUIReference();
                return true;
            }

            return false;
        }

        /// <summary>
        /// Called when a zone that has now become empty needs to be removed as a child zone on the parent, and recursively 
        /// upward if that was the only child zone the parent is collapsed and so on
        /// </summary>
        internal bool UnparentAndDistributeSibling ()
        {
            var parent = this.Parent;

            // It's a root dock zone :/
            if (parent == null)
            {
                return false;
            }

            parent.RemoveChild(this);
            this.Parent = null;
            return true;
        }
    }
}
