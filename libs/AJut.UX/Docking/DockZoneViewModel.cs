namespace AJut.UX.Docking
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using AJut.Storage;
    using AJut.Tree;
    using AJut.TypeManagement;

    /// <summary>
    /// The view model used to populate a DockZone control. Platform-agnostic - works with
    /// both the WPF and WinUI3 DockZone controls via the <see cref="IDockZoneUI"/> interface.
    /// </summary>
    public sealed class DockZoneViewModel : NotifyPropertyChanged
    {
        // ===========[ Fields ]===================================
        private readonly ObservableCollection<DockingContentAdapterModel> m_dockedContent = new();
        private readonly ObservableCollection<DockZoneViewModel> m_children = new();
        private IDockingManager m_manager;
        private DockZoneViewModel m_parent;
        private eDockOrientation m_orientation = eDockOrientation.Empty;
        private int m_selectedIndex;
        private IDockZoneUI m_ui;
        private bool m_isActivelyAttemptingClose;
        private DockZoneSize? m_internalStorageOfPassAlongUISize;

        private static int g_debugTrackingCounter = 0;
        private string m_debugTrackingMoniker = $"Dock Zone VM: #{++g_debugTrackingCounter}";

        // ===========[ Construction ]===================================
        static DockZoneViewModel ()
        {
            TreeTraversal<DockZoneViewModel>.SetupDefaults(dzvm => dzvm.Children, dzvm => dzvm.Parent);
        }

        private DockZoneViewModel ()
        {
            this.DockedContent = new ReadOnlyObservableCollection<DockingContentAdapterModel>(m_dockedContent);
            this.Children = new ReadOnlyObservableCollection<DockZoneViewModel>(m_children);
        }

        public DockZoneViewModel (IDockingManager manager) : this()
        {
            this.Manager = manager;
        }

        public override string ToString () => m_debugTrackingMoniker ?? base.ToString();

        // ===========[ Properties ]===================================

        /// <summary>
        /// Optional debug name - not guaranteed unique; overrides ToString when set.
        /// </summary>
        public string DebugTrackingMoniker
        {
            get => m_debugTrackingMoniker;
            set => this.SetAndRaiseIfChanged(ref m_debugTrackingMoniker, value);
        }

        public IDockingManager Manager
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

        public IDockZoneUI UI
        {
            get => m_ui;
            set
            {
                IDockZoneUI oldUI = m_ui;
                if (oldUI != null && oldUI == m_ui)
                {
                    // Let the UI break its own back-reference before we clear ours
                }

                this.SetAndRaiseIfChanged(ref m_ui, value);
            }
        }

        public bool IsActivelyAttemptingClose
        {
            get => m_isActivelyAttemptingClose;
            private set => this.SetAndRaiseIfChanged(ref m_isActivelyAttemptingClose, value);
        }

        // ===========[ Pass-Along Size (for tearoff / layout handoff) ]===================================

        /// <summary>
        /// [INTERNAL] Store the UI size for passalong during tearoff or serialization-driven construction.
        /// </summary>
        public void StorePassAlongUISize (DockZoneSize size)
        {
            m_internalStorageOfPassAlongUISize = size;
        }

        /// <summary>
        /// [INTERNAL] Take and clear the stored pass-along size. Returns false if none was stored.
        /// </summary>
        public bool TakePassAlongUISize (out DockZoneSize size)
        {
            if (m_internalStorageOfPassAlongUISize != null)
            {
                size = m_internalStorageOfPassAlongUISize.Value;
                m_internalStorageOfPassAlongUISize = null;
                return true;
            }

            size = DockZoneSize.Empty;
            return false;
        }

        public bool HasPassAlongUISize => m_internalStorageOfPassAlongUISize != null;

        public void ClearPassAlongUISize ()
        {
            m_internalStorageOfPassAlongUISize = null;
        }

        // ===========[ Public Interface Methods ]===================================

        public void CopyIntoAndClear (DockZoneViewModel zoneVm)
        {
            // 1. Copy aside to avoid parenting issues during move
            var locallyDockedElementsCopy = m_dockedContent.ToList();
            var childZonesCopy = m_children.ToList();
            var orientation = this.Orientation;
            var selectedIndex = this.SelectedIndex;
            var sizeOnParent = this.UI?.RenderSize ?? DockZoneSize.Empty;

            // 2. Clear both
            this.InternalClearAllSilently();
            zoneVm.InternalClearAllSilently();

            // 3. Populate destination
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

            dupe.StorePassAlongUISize(this.UI?.RenderSize ?? DockZoneSize.Empty);

            // Copy over docked content
            dupe.m_dockedContent.AddEach(m_dockedContent);
            foreach (var dockedContent in m_dockedContent)
            {
                dockedContent.SetNewLocation(dupe);
            }

            // Move children to the dupe
            List<DockZoneViewModel> childrenCopy = m_children.ToList();
            m_children.Clear();
            foreach (DockZoneViewModel child in childrenCopy)
            {
                child.InternallyReparentAndCleanup(dupe);
            }

            this.InternalClearAllSilently();
            return dupe;
        }

        /// <summary>
        /// Requests close of all docked content, and clears entire dock zone. If any panel vetoes close, none are closed.
        /// </summary>
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
        /// Recursively (leaf-up) closes all docked content then clears. Does not ask permission - use
        /// <see cref="RequestCloseAllAndClear"/> to check first.
        /// </summary>
        public void ForceCloseAllAndClear ()
        {
            foreach (var child in m_children)
            {
                child.ForceCloseAllAndClear();
            }

            foreach (var dockedContent in m_dockedContent)
            {
                dockedContent.InternalClose(true);
                dockedContent.SetNewLocation(null);
            }

            if (this.Parent != null)
            {
                this.InternallyReparentAndCleanup(null);
            }

            this.InternalClearAllSilently();
        }

        public void Configure (eDockOrientation orientation)
        {
            if (orientation == eDockOrientation.Empty)
            {
                this.ForceCloseAllAndClear();
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

        public bool AddChild (DockZoneViewModel child) => this.InsertChild(m_children.Count, child);

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
                    zone.StorePassAlongUISize(zone.UI.RenderSize);
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

        public bool AddDockedContent (IDockableDisplayElement panel) => this.AddDockedContent(panel.DockingAdapter);

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
                if (this.Orientation == eDockOrientation.Empty)
                {
                    // Dropped on empty zone - copy the entire source in and rescale
                    List<DockZoneSize> sizes = newSibling.Children.Select(c => c.UI?.RenderSize ?? DockZoneSize.Empty).ToList();
                    newSibling.CopyIntoAndClear(this);
                    if (eDockOrientation.AnySplitOrientation.HasFlag(this.Orientation))
                    {
                        DockZoneSize rootSize = this.UI?.RenderSize ?? DockZoneSize.Empty;
                        if (this.Orientation == eDockOrientation.Horizontal)
                        {
                            double fullHorizontal = sizes.Sum(s => s.Width);
                            List<double> horizontalSizes = sizes.Select(s => rootSize.Width * (s.Width / fullHorizontal)).ToList();
                            this.UI?.SetTargetSizeAsync(horizontalSizes);
                        }
                        else
                        {
                            double fullVertical = sizes.Sum(s => s.Height);
                            List<double> verticalSizes = sizes.Select(s => rootSize.Height * (s.Height / fullVertical)).ToList();
                            this.UI?.SetTargetSizeAsync(verticalSizes);
                        }
                    }
                }
                else if (this.Orientation.IsFlagInGroup(eDockOrientation.AnyLeafDisplay))
                {
                    // Merge all docked content from the source into this leaf zone's tabs
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
                    Logger.LogError("Docking: Attempted to tab-drop onto a non-leaf zone (must split first)");
                    return false;
                }
            }

            // ==== Scenario 2: Insert into parent's child zones at the correct orientation =====
            else if (this.Parent != null && orientation == this.Parent.Orientation)
            {
                int insertIndex = this.Parent.Children.IndexOf(this);
                if (insertIndex == -1)
                {
                    Logger.LogError("Docking: child zone could not be found on parent");
                    return false;
                }

                // Capture existing siblings' render sizes so the UI can redistribute proportionally
                _StorePassAlongSizesForExistingSiblings(this.Parent, orientation, newSibling);

                insertIndex += indexOffset;
                this.Parent.InsertChild(insertIndex, newSibling);
            }

            // ==== Scenario 3: Clone this zone and insert both as siblings =====
            else
            {
                // Ensure newSibling has a pass-along size so the proportional redistribution
                // gives it a reasonable share rather than an equal split with the existing content
                if (!newSibling.HasPassAlongUISize)
                {
                    DockZoneSize currentSize = this.UI?.RenderSize ?? DockZoneSize.Empty;
                    newSibling.StorePassAlongUISize(currentSize);
                }

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

        /// <summary>
        /// Build a new display element of type <typeparamref name="T"/> via the manager's factory and add it to this zone.
        /// </summary>
        public void GenerateAndAdd<T> (object state = null) where T : IDockableDisplayElement
        {
            if (this.Manager.BuildNewDisplayElement(typeof(T)) is T panel)
            {
                this.AddDockedContent(panel);
                panel.DockingAdapter.FinalizeSetup(state);
            }
        }

        // ===========[ Serialization ]===================================

        public DockZoneSerializationData GenerateSerializationState ()
        {
            var data = new DockZoneSerializationData(this.Orientation);
            if (this.Orientation.IsFlagInGroup(eDockOrientation.AnyLeafDisplay))
            {
                data.DisplayState = this.DockedContent
                    .Select(adapter => new DockDisplaySerializationData
                    {
                        TypeId = TypeIdRegistrar.GetTypeIdFor(adapter.Display.GetType()) ?? adapter.Display.GetType().FullName,
                        State = adapter.Display.GenerateState()
                    })
                    .ToArray();
                data.SizeOnParent = this.UI?.RenderSize ?? DockZoneSize.Empty;
                data.SelectedIndex = this.SelectedIndex;
            }
            else
            {
                m_children.Select(z => z.GenerateSerializationState()).ForEach(data.ChildZones.Add);
                if (data.SizeOnParent.IsEmpty)
                {
                    data.SizeOnParent = this.UI?.RenderSize ?? DockZoneSize.Empty;
                }
            }

            return data;
        }

        /// <summary>
        /// Rebuild this zone's tree from serialized state. The <paramref name="displayFactory"/> is called
        /// for each stored display element - it creates the display and returns its adapter, or null to skip.
        /// </summary>
        public void BuildFromState (
            DockZoneSerializationData data,
            Func<DockDisplaySerializationData, DockingContentAdapterModel> displayFactory)
        {
            this.ForceCloseAllAndClear();

            if (data.ChildZones.IsNotNullOrEmpty())
            {
                foreach (DockZoneSerializationData childData in data.ChildZones)
                {
                    var child = new DockZoneViewModel(this.Manager);
                    // Store the child's serialized size so the UI can restore proportions when it's created
                    child.StorePassAlongUISize(childData.SizeOnParent);
                    child.BuildFromState(childData, displayFactory);
                    this.AddChild(child);
                }
            }
            else if (data.DisplayState.IsNotNullOrEmpty())
            {
                foreach (DockDisplaySerializationData displayData in data.DisplayState)
                {
                    DockingContentAdapterModel adapter = displayFactory(displayData);
                    if (adapter != null)
                    {
                        this.AddDockedContent(adapter);
                    }
                }

                this.SelectedIndex = data.SelectedIndex;
            }

            this.Orientation = data.Orientation;
        }

        public bool RunChildZoneRemoval (DockZoneViewModel child)
        {
            if (m_children.Remove(child))
            {
                child.DestroyUIReference();
                return true;
            }

            return false;
        }

        public bool UnparentAndDistributeSibling ()
        {
            var parent = this.Parent;
            if (parent == null)
            {
                return false;
            }

            parent.RemoveChild(this);
            this.Parent = null;
            return true;
        }

        // ===========[ Private Helpers ]===================================

        private static void _StorePassAlongSizesForExistingSiblings (DockZoneViewModel parent, eDockOrientation orientation, DockZoneViewModel newSibling)
        {
            // 1. Snapshot every existing child's current render size
            foreach (DockZoneViewModel sibling in parent.Children)
            {
                sibling.StorePassAlongUISize(sibling.UI?.RenderSize ?? DockZoneSize.Empty);
            }

            // 2. If the new sibling has no size hint (e.g. programmatic add), give it
            //    an equal share so the proportional formula treats it fairly
            if (!newSibling.HasPassAlongUISize)
            {
                bool isHorizontal = orientation == eDockOrientation.Horizontal;
                double totalExisting = parent.Children.Sum(c =>
                    isHorizontal
                        ? (c.UI?.RenderSize.Width ?? 0)
                        : (c.UI?.RenderSize.Height ?? 0)
                );

                double average = parent.Children.Count > 0
                    ? totalExisting / parent.Children.Count
                    : 0;

                newSibling.StorePassAlongUISize(isHorizontal
                    ? new DockZoneSize(average, parent.UI?.RenderSize.Height ?? 0)
                    : new DockZoneSize(parent.UI?.RenderSize.Width ?? 0, average)
                );
            }
        }

        private void InternallyReparentAndCleanup (DockZoneViewModel newParent)
        {
            this.DestroyUIReference();
            newParent?.m_children.Add(this);
            this.Parent = newParent;
        }

        private void InternalClearAllSilently ()
        {
            m_dockedContent.Clear();
            m_children.Clear();
            this.Orientation = eDockOrientation.Empty;
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
    }
}
