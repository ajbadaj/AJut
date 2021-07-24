namespace AJut.Application.Controls
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Input;
    using AJut.Application.Docking;
    using AJut.Storage;
    using AJut.Tree;
    using AJut.TypeManagement;
    using APUtils = AJut.Application.APUtils<DockZone>;
    using DPUtils = AJut.Application.DPUtils<DockZone>;
    using REUtils = AJut.Application.REUtils<DockZone>;

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
        AnyLeafDisplay  = Single | Tabbed,
    }

    public sealed class DockZone : Control, IDisposable
    {
        private readonly ObservableCollection<DockingContentAdapterModel> m_locallyDockedElements = new ObservableCollection<DockingContentAdapterModel>();

        static DockZone ()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(DockZone), new FrameworkPropertyMetadata(typeof(DockZone)));
            TreeTraversal<DockZone>.SetupDefaults(_GetChildren, z => z.ParentZone);

            IEnumerable<DockZone> _GetChildren (DockZone z)
            {
                if (z.DockOrientation == eDockOrientation.Tabbed)
                {
                    yield break;
                }
                else
                {
                    yield return z.AnteriorZone;
                    yield return z.PosteriorZone;
                }
            }
        }
        public DockZone () : this(Guid.NewGuid())
        {
        }

        public DockZone (Guid zoneId)
        {
            this.Id = zoneId;
            this.LocallyDockedElements = new ReadOnlyObservableCollection<DockingContentAdapterModel>(m_locallyDockedElements);
            this.CommandBindings.Add(new CommandBinding(ClosePanelCommand, OnClosePanel, OnCanClosePanel));
        }

        public void Dispose()
        {
            this.Manager = null;
            this.ParentZone = null;
            this.PosteriorZone = null;
            this.AnteriorZone = null;
            m_locallyDockedElements.Clear();
        }

        public Guid Id { get; }

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
                }
            }
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

        public static readonly DependencyProperty AnteriorZoneProperty = DPUtils.Register(_ => _.AnteriorZone, (d, e) => d.OnDirectChildZoneChanged(e));
        public DockZone AnteriorZone
        {
            get => (DockZone)this.GetValue(AnteriorZoneProperty);
            set => this.SetValue(AnteriorZoneProperty, value);
        }

        public static readonly DependencyProperty PosteriorZoneProperty = DPUtils.Register(_ => _.PosteriorZone, (d, e) => d.OnDirectChildZoneChanged(e));
        public DockZone PosteriorZone
        {
            get => (DockZone)this.GetValue(PosteriorZoneProperty);
            set => this.SetValue(PosteriorZoneProperty, value);
        }

        public static readonly DependencyProperty AnteriorSizeProperty = DPUtils.Register(_ => _.AnteriorSize);
        public double AnteriorSize
        {
            get => (double)this.GetValue(AnteriorSizeProperty);
            set => this.SetValue(AnteriorSizeProperty, value);
        }

        public static readonly DependencyProperty DockOrientationProperty = DPUtils.Register(_ => _.DockOrientation, eDockOrientation.Empty);
        public eDockOrientation DockOrientation
        {
            get => (eDockOrientation)this.GetValue(DockOrientationProperty);
            set => this.SetValue(DockOrientationProperty, value);
        }

        public static readonly DependencyProperty SelectedIndexProperty = DPUtils.Register(_ => _.SelectedIndex, 0);
        public int SelectedIndex
        {
            get => (int)this.GetValue(SelectedIndexProperty);
            set => this.SetValue(SelectedIndexProperty, value);
        }

        public static readonly DependencyProperty TabStripPlacementProperty = DPUtils.Register(_ => _.TabStripPlacement, Dock.Bottom);
        public Dock TabStripPlacement
        {
            get => (Dock)this.GetValue(TabStripPlacementProperty);
            set => this.SetValue(TabStripPlacementProperty, value);
        }

        public ReadOnlyObservableCollection<DockingContentAdapterModel> LocallyDockedElements { get; }

        // ============================[ Public Interface ]====================================

        public void Add (IDockableDisplayElement panel)
        {
            this.Add(panel.DockingAdapter);
        }

        public void Add (DockingContentAdapterModel panelAdapter)
        {
            m_locallyDockedElements.Add(panelAdapter);
            panelAdapter.SetNewLocation(this);
            this.DockOrientation = m_locallyDockedElements.Count == 1 ? eDockOrientation.Single : eDockOrientation.Tabbed;
        }

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

        // ============================[ Private Utilities ]====================================

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

        private void OnDirectChildZoneChanged (DependencyPropertyChangedEventArgs<DockZone> e)
        {
            if (e.OldValue != null)
            {
                e.OldValue.Manager = null;
                if (e.OldValue.ParentZone == this)
                {
                    e.OldValue.ParentZone = null;
                }
            }

            if (e.NewValue != null)
            {
                e.NewValue.Manager = this.Manager;
                e.NewValue.ParentZone = this;
            }
        }

        public void GenerateAndAdd<T> (object state = null) where T : IDockableDisplayElement
        {
            var panel = this.Manager.BuildNewDisplayElement<T>();
            this.Add(panel);
            panel.DockingAdapter.FinalizeSetup(state);
        }

        internal Result<DockZone> InsertAndReparentAllChildrenOnToNewZone ()
        {
            DockZone empty = new DockZone();
            empty.Manager = this.Manager;

            DockZone newZone = new DockZone();
            newZone.Manager = this.Manager;

            newZone.AnteriorSize = this.AnteriorSize;
            newZone.AnteriorZone = this.PosteriorZone;
            newZone.PosteriorZone = this.PosteriorZone;
            m_locallyDockedElements.ForEach(newZone.Add);
            newZone.SelectedIndex = this.SelectedIndex;

            m_locallyDockedElements.Clear();
            this.SetSplitChildZones(eDockOrientation.Vertical, empty, newZone);
            return Result<DockZone>.Success(newZone);
        }

        internal void HandlePreTearOff()
        {
            this.CollapseAndDistributeSibling();
        }

        private bool CollapseAndDistributeSibling()
        {
            var parent = this.ParentZone;

            // It's a root dock zone :/
            if (parent == null)
            {
                return false;
            }

            // Otherwise the plan is to make the parent zone essentially the child zone
            //  This is accomplished by clearing the parent and setting all the save child
            //  values onto it.
            DockZone saveZone = parent.AnteriorZone == this ? parent.PosteriorZone : parent.AnteriorZone;
            parent.AnteriorZone = null;
            parent.PosteriorZone = null;
            parent.m_locallyDockedElements.Clear();
            if (saveZone.DockOrientation != eDockOrientation.Empty)
            {
                parent.AnteriorSize = saveZone.AnteriorSize;
                parent.AnteriorZone = saveZone.PosteriorZone;
                parent.PosteriorZone = saveZone.PosteriorZone;
                saveZone.m_locallyDockedElements.ForEach(parent.Add);
                saveZone.Dispose();
            }
            return true;
        }

        internal DockingSerialization.ZoneData GenerateSerializationState ()
        {
            var data = new DockingSerialization.ZoneData
            {
                Id = this.Id,
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
                data.AnteriorSize = this.AnteriorSize;
                data.AnteriorZone = this.AnteriorZone.GenerateSerializationState();
                data.PosteriorZone = this.PosteriorZone.GenerateSerializationState();
            }

            return data;
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

            m_locallyDockedElements.ForEach(duplicate.Add);
            duplicate.DockOrientation = this.DockOrientation;

            this.AnteriorZone = null;
            this.PosteriorZone = null;
            this.m_locallyDockedElements.Clear();
            this.DockOrientation = eDockOrientation.Empty;

            return duplicate;
        }

        internal void DropContentInto (DockZone dropTarget, eDockOrientation dockSelection, bool asAnterior)
        {
            var thisDuplicate = this.GenerateNewAndEmptyInto();
            if (this.HasParentZone)
            {
                this.CollapseAndDistributeSibling();
            }

            if (eDockOrientation.AnyLeafDisplay.HasFlag(dockSelection))
            {
                var allLocals = TreeTraversal<DockZone>.All(thisDuplicate).SelectMany(z => z.m_locallyDockedElements);
                allLocals.ForEach(dropTarget.Add);
            }
            else
            {
                var targetDuplicate = dropTarget.GenerateNewAndEmptyInto();
                dropTarget.SetSplitChildZones(dockSelection,
                    asAnterior ? thisDuplicate : targetDuplicate,
                    asAnterior ? targetDuplicate : thisDuplicate
                );
            }
        }

        internal void BuildFromState (DockingSerialization.ZoneData data)
        {
            //  Reset
            this.AnteriorZone = null;
            this.PosteriorZone = null;
            m_locallyDockedElements.Clear();

            if (data.DockOrientation == eDockOrientation.Tabbed)
            {
                m_locallyDockedElements.AddEach(data.DisplayState.Select(s => DockingSerialization.BuildDisplayElement(this.Manager, s)).Select(d => d.DockingAdapter));
            }
            else
            {
                this.AnteriorSize = data.AnteriorSize;
                this.AnteriorZone = new DockZone
                {
                    ParentZone = this,
                    Manager = this.Manager,
                };
                this.AnteriorZone.BuildFromState(data.AnteriorZone);

                this.PosteriorZone = new DockZone
                {
                    ParentZone = this,
                    Manager = this.Manager,
                };
                this.PosteriorZone.BuildFromState(data.PosteriorZone);
            }

            this.DockOrientation = data.DockOrientation;
        }
    }
}
