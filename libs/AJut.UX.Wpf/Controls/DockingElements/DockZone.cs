namespace AJut.UX.Controls
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Input;
    using System.Windows.Media;
    using AJut.Storage;
    using AJut.Tree;
    using AJut.TypeManagement;
    using AJut.UX.Docking;
    using APUtils = AJut.UX.APUtils<DockZone>;
    using DPUtils = AJut.UX.DPUtils<DockZone>;
    using REUtils = AJut.UX.REUtils<DockZone>;

    [Flags]
    public enum eDockOrientation
    {
        /// <summary>
        /// An empty zone, contains nothing
        /// </summary>
        Empty       = 0b0000,

        /// <summary>
        /// Split Orientation: Horizontal - container of an anterior zone on the left and a posterior zone on the right
        /// </summary>
        Horizontal  = 0b0001,

        /// <summary>
        /// Split Orientation: Vertical - container of an anterior zone on top and a posterior zone below
        /// </summary>
        Vertical    = 0b0010,

        /// <summary>
        /// Leaf orientation: Single element to display
        /// </summary>
        Single      = 0b0100,

        /// <summary>
        /// Leaf orientaiton: more than one element to display
        /// </summary>
        Tabbed      = 0b1000,

        /// <summary>
        /// Either horizontal or vertical, this orientation covers a zone who is only a container for two either vertically or horizontally split sub zones
        /// </summary>
        AnySplitOrientation = Horizontal | Vertical,

        /// <summary>
        /// The leaf orientations contain elements to display, alone if only a single one, or in tabs if more than 1
        /// </summary>
        AnyLeafDisplay  = Single | Tabbed | Empty,
    }

    public enum eDockDirection
    {
        Any,
        Anterior, 
        Posterior
    }

    public sealed class DockZone : Control, IDisposable
    {
        private readonly ObservableCollection<DockingContentAdapterModel> m_locallyDockedElements = new ObservableCollection<DockingContentAdapterModel>();
        private readonly ObservableCollection<DockZone> m_childZones = new ObservableCollection<DockZone>();

        static DockZone ()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(DockZone), new FrameworkPropertyMetadata(typeof(DockZone)));
            TreeTraversal<DockZone>.SetupDefaults(_GetChildren, z => z.ParentZone);

            IEnumerable<DockZone> _GetChildren (DockZone z)
            {
                if (z.DockOrientation == eDockOrientation.Tabbed)
                {
                    return Enumerable.Empty<DockZone>();
                }
                else
                {
                    return z.ChildZones;
                }
            }
        }

        public DockZone ()
        {
            this.LocallyDockedElements = new ReadOnlyObservableCollection<DockingContentAdapterModel>(m_locallyDockedElements);
            this.ChildZones = new ReadOnlyObservableCollection<DockZone>(m_childZones);
            this.CommandBindings.Add(new CommandBinding(ClosePanelCommand, OnClosePanel, OnCanClosePanel));
            DragDropElement.AddDragDropItemsSwapHandler(this, HandleDragDropItemsSwapForHeaders);
        }

        internal DockZone (DockingSerialization.ZoneData zoneData) : this()
        {
            this.BuildFromState(zoneData);
        }

        public DockZone DuplicateAndClear ()
        {
            var dupe = new DockZone();
            this.CopyIntoAndClear(dupe);
            return dupe;
        }

        public void CopyIntoAndClear (DockZone dockZone)
        {
            // Step 1) Copy all the things we're about to steal so there are no parenting issues.
            var locallyDockedElementsCopy = m_locallyDockedElements.ToList();
            var childZonesCopy = m_childZones.ToList();
            var orientation = this.DockOrientation;
            var selectedIndex = this.SelectedIndex;

            // Step 2) Clear in preparation, otherwise there may be some issues with visual/logical parents
            //          still being set, throwing exceptions
            this.Clear();
            dockZone.Clear();

            // Step 3) Set all the things in dockZone with the things we set aside earlier
            locallyDockedElementsCopy.ForEach(dockZone.AddPanel);
            childZonesCopy.ForEach(dockZone.AddChildZone);
            dockZone.DockOrientation = orientation;
            dockZone.SelectedIndex = selectedIndex;
        }


        public void Clear ()
        {
            foreach (DockZone child in m_childZones)
            {
                child.ParentZone = null;

                // It's dangerous ground holding onto UI elements, like we are with
                //  DockZone - it's mostly fine but it means some weirdness where child
                //  elements will have UI they are part of which will be remade, and then
                //  break the "only be a logical/visual child of one" rule, throwing an
                //  exception. By doing this, we ensure we avoid that problem.
                foreach (var desc in TreeTraversal<DockZone>.All(child))
                {
                    desc.TryRemoveFromLogicalAndVisualParents();
                }
            }
            m_childZones.Clear();

            foreach (DockingContentAdapterModel panelAdapter in m_locallyDockedElements)
            {
                panelAdapter.SetNewLocation(null);
            }
            m_locallyDockedElements.Clear();

            this.DockOrientation = eDockOrientation.Empty;
            this.SelectedIndex = -1;
        }


        static int kDEBUG_Counter = 0;
        private void HandleDragDropItemsSwapForHeaders (object sender, DragDropItemsSwapEventArgs e)
        {
            Logger.LogInfo($"Hit log {kDEBUG_Counter++} times");
            if (m_locallyDockedElements.Relocate(e.MoveFromIndex, e.MoveToIndex))
            {
                e.Handled = true;
            }
        }

        public void Dispose ()
        {
            this.DeRegisterAndClear();
        }

        // ============================[ Events / Commands ]====================================

        // Identifies the group of docking the zone is part of, allows things to share docking
        public static DependencyProperty GroupIdProperty = APUtils.Register(GetGroupId, SetGroupId);
        public static string GetGroupId (DependencyObject obj) => (string)obj.GetValue(GroupIdProperty);
        public static void SetGroupId (DependencyObject obj, string value) => obj.SetValue(GroupIdProperty, value);

        public static RoutedEvent NotifyCloseSupressionEvent = REUtils.Register<RoutedEventHandler>(nameof(NotifyCloseSupressionEvent));
        public static RoutedUICommand ClosePanelCommand = new RoutedUICommand("Close Panel", nameof(ClosePanelCommand), typeof(DockZone), new InputGestureCollection(new[] { new KeyGesture(Key.F4, ModifierKeys.Control) }));

        // ============================[ Properties ]====================================

        private DockingManager m_manager;
        public DockingManager Manager
        {
            get => m_manager;
            internal set
            {
                if (m_manager != value)
                {
                    var old = m_manager;
                    m_manager = value;

                    old?.StopTrackingSizingChanges(this);
                    m_manager?.TrackSizingChanges(this);
                    this.IsSetup = m_manager != null;
                }
            }
        }


        public static readonly DependencyProperty PanelBackgroundProperty = DPUtils.RegisterFP(_ => _.PanelBackground, null, null, CoerceUtils.CallbackForBrush);
        public Brush PanelBackground
        {
            get => (Brush)this.GetValue(PanelBackgroundProperty);
            set => this.SetValue(PanelBackgroundProperty, value);
        }

        public static readonly DependencyProperty PanelForegroundProperty = DPUtils.RegisterFP(_ => _.PanelForeground, null, null, CoerceUtils.CallbackForBrush);
        public Brush PanelForeground
        {
            get => (Brush)this.GetValue(PanelForegroundProperty);
            set => this.SetValue(PanelForegroundProperty, value);
        }

        public static readonly DependencyProperty PanelBorderThicknessProperty = DPUtils.Register(_ => _.PanelBorderThickness);
        public Thickness PanelBorderThickness
        {
            get => (Thickness)this.GetValue(PanelBorderThicknessProperty);
            set => this.SetValue(PanelBorderThicknessProperty, value);
        }

        public static readonly DependencyProperty PanelBorderBrushProperty = DPUtils.RegisterFP(_ => _.PanelBorderBrush, null, null, CoerceUtils.CallbackForBrush);
        public Brush PanelBorderBrush
        {
            get => (Brush)this.GetValue(PanelBorderBrushProperty);
            set => this.SetValue(PanelBorderBrushProperty, value);
        }

        public static readonly DependencyProperty PanelCornerRadiusProperty = DPUtils.Register(_ => _.PanelCornerRadius);
        public CornerRadius PanelCornerRadius
        {
            get => (CornerRadius)this.GetValue(PanelCornerRadiusProperty);
            set => this.SetValue(PanelCornerRadiusProperty, value);
        }

        public static readonly DependencyProperty SeparationSizeProperty = DPUtils.Register(_ => _.SeparationSize, (d, e) => d.HalfSeparationSize = d.SeparationSize / 2);
        public double SeparationSize
        {
            get => (double)this.GetValue(SeparationSizeProperty);
            set => this.SetValue(SeparationSizeProperty, value);
        }

        private static readonly DependencyPropertyKey HalfSeparationSizePropertyKey = DPUtils.RegisterReadOnly(_ => _.HalfSeparationSize);
        public static readonly DependencyProperty HalfSeparationSizeProperty = HalfSeparationSizePropertyKey.DependencyProperty;
        public double HalfSeparationSize
        {
            get => (double)this.GetValue(HalfSeparationSizeProperty);
            private set => this.SetValue(HalfSeparationSizePropertyKey, value);
        }

        public static readonly DependencyProperty SeparatorBrushProperty = DPUtils.RegisterFP(_ => _.SeparatorBrush, null, null, CoerceUtils.CallbackForBrush);
        public Brush SeparatorBrush
        {
            get => (Brush)this.GetValue(SeparatorBrushProperty);
            set => this.SetValue(SeparatorBrushProperty, value);
        }

        private static readonly DependencyPropertyKey IsSetupPropertyKey = DPUtils.RegisterReadOnly(_ => _.IsSetup);
        public static readonly DependencyProperty IsSetupProperty = IsSetupPropertyKey.DependencyProperty;
        public bool IsSetup
        {
            get => (bool)this.GetValue(IsSetupProperty);
            private set => this.SetValue(IsSetupPropertyKey, value);
        }

        public static readonly DependencyProperty ParentZoneProperty = DPUtils.Register(_ => _.ParentZone, (d, e) => d.HasParentZone = e.HasNewValue);
        public DockZone ParentZone
        {
            get => (DockZone)this.GetValue(ParentZoneProperty);
            set => this.SetValue(ParentZoneProperty, value);
        }

        private static readonly DependencyPropertyKey HasParentZonePropertyKey = DPUtils.RegisterReadOnly(_ => _.HasParentZone);
        public static readonly DependencyProperty HasParentZoneProperty = HasParentZonePropertyKey.DependencyProperty;
        public bool HasParentZone
        {
            get => (bool)this.GetValue(HasParentZoneProperty);
            private set => this.SetValue(HasParentZonePropertyKey, value);
        }

        public static readonly DependencyProperty DockOrientationProperty = DPUtils.Register(_ => _.DockOrientation, eDockOrientation.Empty, (d, e) => d.OnDockOrientationChanged(e));
        public eDockOrientation DockOrientation
        {
            get => (eDockOrientation)this.GetValue(DockOrientationProperty);
            set => this.SetValue(DockOrientationProperty, value);
        }

        private static readonly DependencyPropertyKey HasSplitZoneOrientationPropertyKey = DPUtils.RegisterReadOnly(_ => _.HasSplitZoneOrientation);
        public static readonly DependencyProperty HasSplitZoneOrientationProperty = HasSplitZoneOrientationPropertyKey.DependencyProperty;
        public bool HasSplitZoneOrientation
        {
            get => (bool)this.GetValue(HasSplitZoneOrientationProperty);
            private set => this.SetValue(HasSplitZoneOrientationPropertyKey, value);
        }

        public static readonly DependencyProperty SelectedIndexProperty = DPUtils.Register(_ => _.SelectedIndex, 0);
        public int SelectedIndex
        {
            get => (int)this.GetValue(SelectedIndexProperty);
            set => this.SetValue(SelectedIndexProperty, value);
        }

        private static readonly DependencyPropertyKey IsDirectDropTargetPropertyKey = DPUtils.RegisterReadOnly(_ => _.IsDirectDropTarget);
        public static readonly DependencyProperty IsDirectDropTargetProperty = IsDirectDropTargetPropertyKey.DependencyProperty;
        public bool IsDirectDropTarget
        {
            get => (bool)this.GetValue(IsDirectDropTargetProperty);
            internal set => this.SetValue(IsDirectDropTargetPropertyKey, value);
        }

        public ReadOnlyObservableCollection<DockingContentAdapterModel> LocallyDockedElements { get; }
        public ReadOnlyObservableCollection<DockZone> ChildZones { get; }

        public static readonly DependencyProperty IsDropScootHoverLeftProperty = DPUtils.Register(_ => _.IsDropScootHoverLeft);
        public bool IsDropScootHoverLeft
        {
            get => (bool)this.GetValue(IsDropScootHoverLeftProperty);
            set => this.SetValue(IsDropScootHoverLeftProperty, value);
        }

        public static readonly DependencyProperty IsDropScootHoverTopProperty = DPUtils.Register(_ => _.IsDropScootHoverTop);
        public bool IsDropScootHoverTop
        {
            get => (bool)this.GetValue(IsDropScootHoverTopProperty);
            set => this.SetValue(IsDropScootHoverTopProperty, value);
        }

        public static readonly DependencyProperty IsDropScootHoverRightProperty = DPUtils.Register(_ => _.IsDropScootHoverRight);
        public bool IsDropScootHoverRight
        {
            get => (bool)this.GetValue(IsDropScootHoverRightProperty);
            set => this.SetValue(IsDropScootHoverRightProperty, value);
        }

        public static readonly DependencyProperty IsDropScootHoverBottomProperty = DPUtils.Register(_ => _.IsDropScootHoverBottom);
        public bool IsDropScootHoverBottom
        {
            get => (bool)this.GetValue(IsDropScootHoverBottomProperty);
            set => this.SetValue(IsDropScootHoverBottomProperty, value);
        }

        // ============================[ Public Interface ]====================================

        public void AddPanel (IDockableDisplayElement panel)
        {
            this.AddPanel(panel.DockingAdapter);
        }

        public void AddPanel (DockingContentAdapterModel panelAdapter)
        {
            m_locallyDockedElements.Add(panelAdapter);
            panelAdapter.SetNewLocation(this);
            this.DockOrientation = m_locallyDockedElements.Count == 1 ? eDockOrientation.Single : eDockOrientation.Tabbed;
        }

        public bool DropAddSiblingIntoDock (DockZone newSibling, eDockInsertionDirection insertionDirection)
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

            // ==== Scenario 2: Insert as tab =====
            if (orientation == eDockOrientation.Tabbed)
            {
                if (this.DockOrientation.HasPartialFlag(eDockOrientation.AnyLeafDisplay))
                {
                    // Find all dock display elements and add them as children to this
                    bool addedAnything = false;
                    foreach (DockZone descendant in TreeTraversal<DockZone>.All(newSibling))
                    {
                        var elements = descendant.m_locallyDockedElements.ToList();
                        addedAnything = addedAnything || elements.Count > 0;
                        descendant.Clear();
                        elements.ForEach(this.AddPanel);
                    }

                    if (addedAnything)
                    {
                        this.SelectedIndex = m_locallyDockedElements.Count - 1;
                    }
                }
                else
                {
                    Logger.LogError("Docking issue: Attempted to drop a dock zone on a non-leaf display (tabbed, single, none), zones must be split first for that to work (otherwise the system would be guessing as to which way to split it)");
                    return false;
                }
            }

            // ==== Scenario 2: Insert into parent's child zones =====
            else if (orientation == this.DockOrientation)
            {
                int insertIndex = this.ParentZone.ChildZones.IndexOf(this);
                if (insertIndex == -1)
                {
                    Logger.LogError("Docking issue: child zone could not be found on parent");
                    return false;
                }

                insertIndex += indexOffset;
                this.ParentZone.InsertChildZone(insertIndex, newSibling);
            }
            // ==== Scenario 3: Clone and insert =====
            else
            {
                var dupe = this.DuplicateAndClear();
                this.DockOrientation = orientation;
                this.InsertChildZone(0, dupe);
                this.InsertChildZone(indexOffset, newSibling);
            }

            return true;
        }

        public void RemoveChildZone (DockZone child)
        {
            if (child != null && child.ParentZone == this)
            {
                m_childZones.Remove(child);
                child.Manager = null;
                child.ParentZone = null;
            }

            if (m_childZones.Count == 0)
            {
                this.DockOrientation = eDockOrientation.Empty;
            }
            else if (m_childZones.Count == 1)
            {
                // If there were two zones, and removing the one we did just put it to one zone
                //  then for efficiancy, we're going to collapse this. We should never have a child
                //  with a single zone
                DockZone siblingToKeep = m_childZones[0].DuplicateAndClear();
                this.Clear();
                siblingToKeep.CopyIntoAndClear(this);
            }
        }

#if false

        internal Result<DockZone> InsertAndReparentAllChildrenOnToNewZone ()
        {
            DockZone duplicate = this.DuplicateAndClear(clearAfter: true);
            this.

            //DockZone empty = new DockZone();
            //empty.Manager = this.Manager;

            //DockZone newZone = new DockZone();
            //newZone.Manager = this.Manager;

            //newZone.AnteriorSize = this.AnteriorSize;
            //newZone.AnteriorZone = this.PosteriorZone;
            //newZone.PosteriorZone = this.PosteriorZone;
            //m_locallyDockedElements.ForEach(newZone.Add);
            //newZone.SelectedIndex = this.SelectedIndex;

            //m_locallyDockedElements.Clear();
            //this.SetSplitChildZones(eDockOrientation.Vertical, empty, newZone);
            //return Result<DockZone>.Success(newZone);
        }


        public void InsertSplitChild (eDockOrientation orientation, DockZone newChild, int index = -1)
        {
            // Can't add a split without it being one of the split orientation elements
            if (!orientation.HasFlag(eDockOrientation.AnySplitOrientation))
            {
                return;
            }

            // ==== Scenario 1: Drop leaf =====
            // If it's a leaf and now we're splitting it
            if (this.DockOrientation.HasFlag(eDockOrientation.AnyLeafDisplay))
            {
                
            }
            // ==== Scenario 2: Reorient =====
            else if (this.DockOrientation != orientation)
            {

            }
            // ==== Scenario 3: Insert into child zones =====
            else
            {
            }
        }

        
        /*
        public void SetSplitChildZones (eDockOrientation orientation, DockZone anterior, DockZone posterior)
        {
            this.AnteriorSize = double.NaN;
            this.DockOrientation = orientation;
            this.AnteriorZone = anterior;
            this.PosteriorZone = posterior;

            this.DockOrientation = orientation;

            // TODO: What does clearing this mean? Should close happen first? Should we rely on the user
            //  having already done the right thing before this? Should there be an option to cleanup defaulted to false or something?
            m_locallyDockedElements.Clear();
        }
        */



        // ↓ Like this name... needs some kind of parent deliniation or version.
        internal void DropContentInto (eDockOrientation requestedOrientation, DockZone dropTarget, eDockDirection side = eDockDirection.Any)
        {
            switch (dropTarget.DockOrientation)
            {
                case eDockOrientation.Empty:
                case eDockOrientation.Single:
                case eDockOrientation.Tabbed:
                    if (this.HasParentZone)
                    {
                        this.CollapseAndDistributeSibling();
                    }
                    var allLocals = TreeTraversal<DockZone>.All(this.GenerateNewAndEmptyInto()).SelectMany(z => z.m_locallyDockedElements);
                    allLocals.ForEach(dropTarget.Add);
                    break;

                case eDockOrientation.Horizontal:
                case eDockOrientation.Vertical:
                    var targetDuplicate = dropTarget.GenerateNewAndEmptyInto();
                    dropTarget.SetSplitChildZones(newOrientation,
                        side == eDockDirection.Anterior ? this.GenerateNewAndEmptyInto() : targetDuplicate,
                        side == eDockDirection.Anterior ? targetDuplicate : this.GenerateNewAndEmptyInto()
                    );
                    break;
            }
        }
        
        internal DockZone GenerateNewAndEmptyInto ()
        {
            DockZone duplicate = new DockZone();
            duplicate.Manager = this.Manager;

            //parent.AnteriorZone = null;
            //parent.PosteriorZone = null;
            //parent.m_locallyDockedElements.Clear();
            duplicate.AnteriorSize = this.AnteriorSize;
            duplicate.AnteriorZone = this.PosteriorZone;
            duplicate.PosteriorZone = this.PosteriorZone;

            if (duplicate.AnteriorZone != null)
            {
                duplicate.AnteriorZone.ParentZone = duplicate;
            }

            if (duplicate.PosteriorZone != null)
            {
                duplicate.PosteriorZone.ParentZone = duplicate;
            }

            m_locallyDockedElements.ForEach(duplicate.AddPanel);
            duplicate.DockOrientation = this.DockOrientation;

            this.AnteriorZone = null;
            this.PosteriorZone = null;
            this.m_locallyDockedElements.Clear();
            this.DockOrientation = eDockOrientation.Empty;

            return duplicate;
        }

#endif
        public Result TryHandleRemovePanel (IDockableDisplayElement panel)
        {
            return this.TryHandleRemovePanel(panel.DockingAdapter);
        }

        public Result TryHandleRemovePanel (DockingContentAdapterModel panelAdapter)
        {
            if (!m_locallyDockedElements.Remove(panelAdapter))
            {
                return Result.Error($"DockZone: Panel {panelAdapter?.DebugName ?? "null"} not found");
            }

            panelAdapter.SetNewLocation(null);
            switch (m_locallyDockedElements.Count)
            {
                case 0: this.CollapseAndDistributeSibling(); break;
                case 1: this.DockOrientation = eDockOrientation.Single; break;
            }

            return new Result();
        }

        public void AddChildZone (DockZone child)
        {
            child.Manager = this.Manager;
            child.ParentZone = this;
            m_childZones.Add(child);
        }

        public void AddChildZones (params DockZone[] children)
        {
            children.ForEach(this.AddChildZone);
        }

        // ============================[ Private Utilities ]====================================

        /// <summary>
        /// Inserts child zone and also sets it up with manager and parenting
        /// </summary>
        private void InsertChildZone (int index, DockZone child)
        {
            child.Manager = this.Manager;
            child.ParentZone = this;
            m_childZones.Insert(index, child);
        }

        /// <summary>
        /// Clears the dockzone, assumes it has already been removed from other zones
        /// </summary>
        internal void DeRegisterAndClear ()
        {
            this.Manager?.DeRegisterRootDockZones(this);
            this.Clear();
        }

        private void OnCanClosePanel (object sender, CanExecuteRoutedEventArgs e)
        {
            if (e.Parameter is DockingContentAdapterModel panelAdapter)
            {
                if (m_locallyDockedElements.Contains(panelAdapter))
                {
                    e.CanExecute = true;
                }
            }
        }

        private void OnClosePanel (object sender, ExecutedRoutedEventArgs e)
        {
            this.TryHandleRemovePanel((DockingContentAdapterModel)e.Parameter);
        }

        private void OnDockOrientationChanged (DependencyPropertyChangedEventArgs<eDockOrientation> e)
        {
            this.HasSplitZoneOrientation = eDockOrientation.AnySplitOrientation.HasFlag(e.NewValue);
        }

        public void GenerateAndAdd<T> (object state = null) where T : IDockableDisplayElement
        {
            var panel = this.Manager.BuildNewDisplayElement<T>();
            this.AddPanel(panel);
            panel.DockingAdapter.FinalizeSetup(state);
        }

        internal void HandlePreTearoff ()
        {
            this.CollapseAndDistributeSibling();
        }

        /// <summary>
        /// Called when a zone that has now become empty needs to be removed as a child zone on the parent, and recursively 
        /// upward if that was the only child zone the parent is collapsed and so on
        /// </summary>
        internal bool CollapseAndDistributeSibling ()
        {
            var parent = this.ParentZone;

            // It's a root dock zone :/
            if (parent == null)
            {
                return false;
            }

            parent.RemoveChildZone(this);
            return true;
        }

        internal DockingSerialization.ZoneData GenerateSerializationState ()
        {
            var data = new DockingSerialization.ZoneData
            {
                GroupId = DockZone.GetGroupId(this),
                DockOrientation = this.DockOrientation,
            };

            if (this.DockOrientation == eDockOrientation.Tabbed)
            {
                data.DisplayState = this.LocallyDockedElements
                    .Select(
                        adapter => new DockingSerialization.DisplayData {
                            TypeId = TypeIdRegistrar.GetTypeIdFor(adapter.GetType()),
                            State = adapter.Display.GenerateState()
                        }
                    ).ToArray();
            }
            else
            {
                m_childZones.Select(z => z.GenerateSerializationState()).ForEach(data.ChildZones.Add);
            }

            return data;
        }


        internal void BuildFromState (DockingSerialization.ZoneData data)
        {
            //  Reset
            this.Clear();

            if (data.DockOrientation == eDockOrientation.Tabbed)
            {
                m_locallyDockedElements.AddEach(data.DisplayState.Select(s => DockingSerialization.BuildDisplayElement(this.Manager, s)).Select(d => d.DockingAdapter));
            }
            else
            {
                data.ChildZones.Select(zd => new DockZone(zd)).ForEach(this.AddChildZone);
            }

            this.DockOrientation = data.DockOrientation;
        }
    }
}
