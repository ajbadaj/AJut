namespace AJut.UX.Docking
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Windows;
    using AJut.Storage;
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
        private readonly ObservableCollection<DockingContentAdapterModel> m_dockedContent = new();
        private readonly ObservableCollection<DockZoneViewModel> m_children = new();
        private DockingManager m_manager;
        private DockZoneViewModel m_parent;
        private eDockOrientation m_orientation = eDockOrientation.Empty;
        private int m_selectedIndex;
        private DockZone m_ui;
        private bool m_isActivelyAttemptingClose;
        private Size? m_internalStorageOfPassAlongUISize;

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

        private static int g_debugTrackingCounter = 0;
        private string m_debugTrackingMoniker = $"Dock Zone VM: #{++g_debugTrackingCounter}";

        /// <summary>
        /// The tracking moniker is an optional name given used to better identify and debug issues. It is not guranteed unique, and can
        /// be set to any string the user of this api would like. If present, it will override the ToString implementation as well.
        /// </summary>
        public string DebugTrackingMoniker
        {
            get => m_debugTrackingMoniker;
            set => this.SetAndRaiseIfChanged(ref m_debugTrackingMoniker, value);
        }

        public override string ToString () => this.DebugTrackingMoniker ?? base.ToString();

        /// <summary>
        /// [INTERNAL] Store the UI Size for passalong. Note, this is an uncommon mechanism and should only be used as construction passthrough
        /// either at serialization time, or as part of the tear-off or similar mechanisms where a new UI is created.
        /// </summary>
        internal void StorePassAlongUISize (Size size)
        {
            m_internalStorageOfPassAlongUISize = size;
        }

        /// <summary>
        /// [INTERNAL] Take the UI Size for passing along. This action is destructive. Note, this is an uncommon mechanism and should only be used 
        /// as construction passthrough either at serialization time, or as part of the tear-off or similar mechanisms where a new UI is created.
        /// </summary>
        internal bool TakePassAlongUISize (out Size size)
        {
            if (m_internalStorageOfPassAlongUISize != null)
            {
                size = m_internalStorageOfPassAlongUISize.Value;
                m_internalStorageOfPassAlongUISize = null;
                return true;
            }

            size = Size.Empty;
            return false;
        }

        /// <summary>
        /// [INTERNAL] Clear the UI Size for passing along. This action is destructive. Note, this is an uncommon mechanism
        /// and should only be used as part of the tear-off or similar mechanisms where a new UI is created.
        /// </summary>
        internal void ClearPassAlongUISize ()
        {
            m_internalStorageOfPassAlongUISize = null;
        }

        // =======================[ Properties ]=====================================

        public DockingManager Manager
        {
            get => m_manager;
            set => this.SetAndRaiseIfChanged(ref m_manager, value);
        }

        public DockZoneViewModel Parent
        {
            get => m_parent;
            set => this.SetAndRaiseIfChanged(ref m_parent, value, nameof(Parent), nameof(HasParent));
        }

        public bool HasParent => m_parent != null;

        public ReadOnlyObservableCollection<DockingContentAdapterModel> DockedContent { get; }
        public ReadOnlyObservableCollection<DockZoneViewModel> Children { get; }

        public eDockOrientation Orientation
        {
            get => m_orientation;
            private set => this.SetAndRaiseIfChanged(ref m_orientation, value);
        }

        public int SelectedIndex
        {
            get => m_selectedIndex;
            set => this.SetAndRaiseIfChanged(ref m_selectedIndex, value);
        }

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

        public bool IsActivelyAttemptingClose
        {
            get => m_isActivelyAttemptingClose;
            private set => this.SetAndRaiseIfChanged(ref m_isActivelyAttemptingClose, value);
        }

        // =======================[ Public API Methods ]=====================================

        /// <summary>
        /// Copy this into the passed in zone vm, then clear this
        /// </summary>
        /// <param name="zoneVm"></param>
        public void CopyIntoAndClear (DockZoneViewModel zoneVm)
        {
            // Step 1) Copy all the things we're about to steal so there are no parenting issues.
            var locallyDockedElementsCopy = m_dockedContent.ToList();
            var childZonesCopy = m_children.ToList();
            var orientation = this.Orientation;
            var selectedIndex = this.SelectedIndex;
            var sizeOnParent = this.UI.RenderSize;

            // Step 2) Clear in preparation, otherwise there may be some issues with visual/logical parents
            //          still being set, throwing exceptions
            this.InternalClearAllSilently();
            zoneVm.InternalClearAllSilently();

            // Step 3) Set all the things in dockZone with the things we set aside earlier
            locallyDockedElementsCopy.ForEach(c => zoneVm.AddDockedContent(c));
            childZonesCopy.ForEach(c => zoneVm.AddChild(c));
            zoneVm.Orientation = orientation;
            zoneVm.SelectedIndex = selectedIndex;
            zoneVm.StorePassAlongUISize(sizeOnParent);
        }

        public DockZoneViewModel DuplicateAndClear ()
        {
            var dupe = new DockZoneViewModel
            {
                Orientation = this.Orientation,
            };

            // Store sizing info to pass along to grid in next UI location
            dupe.StorePassAlongUISize(this.UI.RenderSize);

            // Copy over Docked Content and clear
            dupe.m_dockedContent.AddEach(m_dockedContent);
            foreach (var dockedContent in m_dockedContent)
            {
                dockedContent.SetNewLocation(dupe);
            }


            // Clear children (child zones have direct impact in creating UI so cleanup is vital)
            List<DockZoneViewModel> childrenCopy = m_children.ToList();
            m_children.Clear();
            foreach (DockZoneViewModel child in childrenCopy)
            {
                child.InternallyReparentAndCleanup(dupe);
            }

            // With everything moved over, we can now clear directly without having to carefully
            //  check and move, and update all the content
            this.InternalClearAllSilently();
            return dupe;
        }


        /// <summary>
        /// Requests close of all docked content, and clears entire dockzone. If any dockzones can't close however, none are.
        /// </summary>
        /// <param name="bailAtFirstFailure">The result contains all failures, but if you feel you're pestering your user you can choose to bail at the first failure</param>
        /// <param name="closeWindowIfDockTearoff"></param>
        public Result<List<DockingContentAdapterModel>> RequestCloseAllAndClear (bool bailAtFirstFailure = true)
        {
            this.IsActivelyAttemptingClose = true;
            try
            {

                var cantClose = new List<DockingContentAdapterModel>();
                var allDockedContent = TreeTraversal<DockZoneViewModel>.All(this).SelectMany(z => z.DockedContent).ToList();
                foreach (DockingContentAdapterModel dockedContent in allDockedContent)
                {
                    if (!dockedContent.CheckCanClose())
                    {
                        cantClose.Add(dockedContent);
                        if (bailAtFirstFailure)
                        {
                            break;
                        }
                    }
                }

                if (cantClose.Count > 0)
                {
                    var result = new Result<List<DockingContentAdapterModel>>(cantClose);
                    result.AddError("Several docked elements could not close");
                    foreach (var content in cantClose)
                    {
                        result.AddError($"  > Can't close: {content.TitleContent ?? content.GetType().Name}");
                    }

                    return result;
                }

                this.ForceCloseAllAndClear();
                return Result<List<DockingContentAdapterModel>>.Success(null);
            }
            finally
            {
                this.IsActivelyAttemptingClose = false;
            }
        }

        /// <summary>
        /// Recursively (leaf up) clozes all docked content (sends close event), then clears and cleans up all ui and parent references.
        /// To request using the <see cref="DockingContentAdapterModel.CheckCanClose"/>, use <see cref="RequestCloseAllAndClear"/> instead/
        /// </summary>
        public void ForceCloseAllAndClear ()
        {
            // Recurse first so we start at the leaves
            foreach (var child in m_children)
            {
                child.ForceCloseAllAndClear();
            }

            // Remove all docked content
            foreach (var dockedContent in m_dockedContent)
            {
                dockedContent.InternalClose();
                dockedContent.SetNewLocation(null);
            }

            this.InternallyReparentAndCleanup(null);
            this.InternalClearAllSilently();
        }

        public void Configure (eDockOrientation orientation)
        {
            if (orientation == eDockOrientation.Empty)
            {
                this.ForceClearAllRecursively();
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

            child.Parent?.RunChildZoneRemoval(child);
            child.Parent = this;
            child.Manager = this.Manager;
            m_children.Insert(index, child);
            return true;
        }

        public bool RemoveChild (DockZoneViewModel child)
        {
            bool result = this.RunChildZoneRemoval(child);
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

            static void _DoDestroyReference (DockZoneViewModel zone)
            {
                if (zone.UI != null)
                {
                    // Save it for the next UI (if any)
                    zone.StorePassAlongUISize(zone.UI.RenderSize);

                    // Break both references
                    zone.UI.ViewModel = null;
                    zone.UI = null;
                }
            }
        }

        public bool RemoveDockedContent (DockingContentAdapterModel contentAdapter)
        {
            if (m_dockedContent.Contains(contentAdapter))
            {
                return this.DoRemoveContent(contentAdapter);
            }

            return false;
        }

        public bool RequestCloseAndRemoveDockedContent (DockingContentAdapterModel panelAdapter)
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

        public bool AddDockedContent (DockingContentAdapterModel adapter)
        {
            if (!this.Orientation.IsFlagInGroup(eDockOrientation.AnyLeafDisplay))
            {
                Logger.LogError("Attempted to add docked content to non-leaf display");
                return false;
            }

            m_dockedContent.Add(adapter);
            adapter.SetNewLocation(this);
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
                // If it's empty (which, by all efforts should be only a root level item - but that shouldn't be relevent), than
                //  there is a special insert to do, which is to copy it all in. That is because you can't do directional drops
                //  on empty, as it is assumed empty panels are only at the root level, and therefore there is nothing to split with
                //  as well as visually there is an expectation that what I'm dropping in just fills the space.
                if (this.Orientation == eDockOrientation.Empty)
                {
                    newSibling.CopyIntoAndClear(this);
                }
                else if (this.Orientation.IsFlagInGroup(eDockOrientation.AnyLeafDisplay))
                {
                    // Find all dock display elements and add them as children to this
                    bool addedAnything = false;
                    foreach (DockingContentAdapterModel content in TreeTraversal<DockZoneViewModel>.All(newSibling).SelectMany(z => z.DockedContent).ToList())
                    {
                        addedAnything = true;
                        this.AddDockedContent(content);
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

        public bool SwapDockedContentOrder (int moveFromIndex, int moveToIndex)
        {
            return m_dockedContent.Relocate(moveFromIndex, moveToIndex);
        }

        public void GenerateAndAdd<T> (object state = null) where T : IDockableDisplayElement
        {
            var panel = this.Manager.BuildNewDisplayElement<T>();
            this.AddDockedContent(panel);
            panel.DockingAdapter.FinalizeSetup(state);
        }

        // =======================[ Hidden API Utilities ]=====================================

        private void PrepForExport ()
        {
            this.StorePassAlongUISize(this.UI.RenderSize);
        }

        /// <summary>
        /// Recursively (leaf up) clears all contents and cleans up all ui and parent references
        /// </summary>
        internal void ForceClearAllRecursively ()
        {
            // Recurse first so we start at the leaves
            foreach (var child in m_children)
            {
                child.ForceClearAllRecursively();
            }

            // Remove all docked content
            foreach (DockingContentAdapterModel dockedContent in m_dockedContent)
            {
                dockedContent.SetNewLocation(null);
            }

            this.InternallyReparentAndCleanup(null);
            this.InternalClearAllSilently();
        }

        /// <summary>
        /// The non-interface way to remove a child, to be performed only in mass cleanup efforts
        /// </summary>
        private void InternallyReparentAndCleanup (DockZoneViewModel newParent)
        {
            // Out with the old
            this.DestroyUIReference();

            // Directly add it to dupe and set it up
            newParent?.m_children.Add(this);
            this.Parent = newParent;
        }

        /// <summary>
        /// Clears all docked content &amp; children, but does NOT do any notifications
        /// </summary>
        private void InternalClearAllSilently ()
        {
            m_dockedContent.Clear();
            m_children.Clear();
            this.Orientation = eDockOrientation.Empty;
        }

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
            this.ForceCloseAllAndClear();

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

        private bool DoRemoveContent (DockingContentAdapterModel dockedContent)
        {
            if (dockedContent.Location == this)
            {
                dockedContent.SetNewLocation(null);
            }

            int currentIndex = m_dockedContent.IndexOf(dockedContent);
            bool result = m_dockedContent.Remove(dockedContent);
            switch (m_dockedContent.Count)
            {
                case 0:
                    if (this.Parent == null)
                    {
                        this.Orientation = eDockOrientation.Empty;
                    }
                    else
                    {
                        this.Parent.RemoveChild(this);
                    }
                    break;

                case 1:
                    this.Orientation = eDockOrientation.Single;
                    break;
            }

            if (currentIndex == this.SelectedIndex)
            {
                this.SelectedIndex = Math.Max(0, currentIndex - 1);
            }

            return result;
        }

        /// <summary>
        /// Removes the specified child, and destorys the UI reference
        /// </summary>
        internal bool RunChildZoneRemoval (DockZoneViewModel child)
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
