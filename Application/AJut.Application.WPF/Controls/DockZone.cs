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
    using AJut.Tree;
    using AJut.TypeManagement;
    using APUtils = AJut.Application.APUtils<DockZone>;
    using DPUtils = AJut.Application.DPUtils<DockZone>;
    using REUtils = AJut.Application.REUtils<DockZone>;

    public enum eDockOrientation
    {
        Empty,
        Horizontal,
        Vertical,
        Tabbed,
        Single,
    }

    public sealed class DockZone : Control
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
        public DockZone ()
        {
            this.LocallyDockedElements = new ReadOnlyObservableCollection<DockingContentAdapterModel>(m_locallyDockedElements);
            this.CommandBindings.Add(new CommandBinding(ClosePanelCommand, OnClosePanel, OnCanClosePanel));
        }

        // ============================[ Events / Commands ]====================================

        // Identifies the group of docking the zone is part of, allows things to share docking
        public static DependencyProperty GroupIdProperty = APUtils.Register(GetGroupId, SetGroupId);
        public static string GetGroupId (DependencyObject obj) => (string)obj.GetValue(GroupIdProperty);
        public static void SetGroupId (DependencyObject obj, string value) => obj.SetValue(GroupIdProperty, value);

        public static RoutedEvent NotifyCloseSupressionEvent = REUtils.Register<RoutedEventHandler>(nameof(NotifyCloseSupressionEvent));
        public static RoutedUICommand ClosePanelCommand = new RoutedUICommand("Close Panel", nameof(ClosePanelCommand), typeof(DockZone), new InputGestureCollection(new[] { new KeyGesture(Key.F4, ModifierKeys.Control) }));

        // ============================[ Properties ]====================================

        public DockingManager Manager { get; internal set; }

        public static readonly DependencyProperty ParentZoneProperty = DPUtils.Register(_ => _.ParentZone, (d,e)=>d.HasParentZone = e.HasNewValue);
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

        public static readonly DependencyProperty AnteriorZoneProperty = DPUtils.Register(_ => _.AnteriorZone, (d,e)=>d.OnDirectChildZoneChanged(e));
        public DockZone AnteriorZone
        {
            get => (DockZone)this.GetValue(AnteriorZoneProperty);
            set => this.SetValue(AnteriorZoneProperty, value);
        }

        public static readonly DependencyProperty PosteriorZoneProperty = DPUtils.Register(_ => _.PosteriorZone, (d,e)=>d.OnDirectChildZoneChanged(e));
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

        public bool Close (IDockableDisplayElement panel)
        {
            return this.Close(panel.DockingAdapter);
        }
        public bool Close (DockingContentAdapterModel panelAdapter)
        {
            if (!m_locallyDockedElements.Remove(panelAdapter))
            {
                return false;
            }

            switch (m_locallyDockedElements.Count)
            {
                case 0: this.CollapseAndDistributeSibling(); break;
                case 1: this.DockOrientation = eDockOrientation.Single; break;
            }

            return true;
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
            this.Close((DockingContentAdapterModel)e.Parameter);
        }

        private void OnDirectChildZoneChanged (DependencyPropertyChangedEventArgs<DockZone> e)
        {
            if (e.OldValue != null)
            {
                e.OldValue.Manager = null;
                e.OldValue.ParentZone = null;
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

        private void CollapseAndDistributeSibling()
        {
            var parent = this.ParentZone;

            // It's a root dock zone :/
            if (parent == null)
            {
                return;
            }

            // Otherwise the plan is to make the parent zone essentially the child zone
            //  This is accomplished by clearing the parent and setting all the save child
            //  values onto it.
            DockZone saveZone = parent.AnteriorZone == this ? parent.PosteriorZone : parent.AnteriorZone;
            parent.AnteriorZone = null;
            parent.PosteriorZone = null;
            parent.m_locallyDockedElements.Clear();

            parent.AnteriorSize = saveZone.AnteriorSize;
            parent.AnteriorZone = saveZone.AnteriorZone;
            parent.PosteriorZone = saveZone.PosteriorZone;
            saveZone.m_locallyDockedElements.ForEach(parent.Add);
        }

        internal DockingSerialization.ZoneData GenerateSerializationState ()
        {
            var data = new DockingSerialization.ZoneData();
            data.DockOrientation = this.DockOrientation;
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
