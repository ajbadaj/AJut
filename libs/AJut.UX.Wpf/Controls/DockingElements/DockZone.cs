namespace AJut.UX.Controls
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Collections.Specialized;
    using System.Diagnostics;
    using System.Linq;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Data;
    using System.Windows.Input;
    using AJut.Tree;
    using AJut.UX;
    using AJut.UX.Docking;
    using APUtils = AJut.UX.APUtils<DockZone>;
    using DPUtils = AJut.UX.DPUtils<DockZone>;
    using REUtils = AJut.UX.REUtils<DockZone>;

    public sealed partial class DockZone : Control
    {
        private readonly ObservableCollection<DockZone> m_childZones = new ObservableCollection<DockZone>();

        static DockZone ()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(DockZone), new FrameworkPropertyMetadata(typeof(DockZone)));
            TreeTraversal<DockZone>.SetupDefaults(_GetChildren, _GetParent);

            IEnumerable<DockZone> _GetChildren (DockZone z)
            {
                if (z?.ViewModel == null || z.ViewModel.Orientation == eDockOrientation.Tabbed)
                {
                    return Enumerable.Empty<DockZone>();
                }
                else
                {
                    return z.ChildZones;
                }
            }

            DockZone _GetParent (DockZone z) => z.ViewModel?.Parent?.UI;
        }

        public DockZone ()
        {
            this.ChildZones = new ReadOnlyObservableCollection<DockZone>(m_childZones);
            this.CommandBindings.Add(new CommandBinding(CloseDockedContentCommand, OnCloseDockedContent, OnCanCloseDockedContent));
            this.ViewModel = new DockZoneViewModel(manager: null);
            DragDropElement.AddDragDropItemsSwapHandler(this, HandleDragDropItemsSwapForHeaders);

            m_childZones.CollectionChanged += this.OnChildZoneUIAdded;
            this.Loaded += _OnLoaded;
            void _OnLoaded (object sender, RoutedEventArgs e)
            {
                this.HandleNewChildZonesAdded(m_childZones);
            }
        }

        private void OnChildZoneUIAdded (object sender, NotifyCollectionChangedEventArgs e)
        {
            if (!this.IsLoaded)
            {
                return;
            }

            this.Dispatcher.InvokeAsync(() =>
            {
                if (e.NewItems != null)
                {
                    this.HandleNewChildZonesAdded(e.NewItems.OfType<DockZone>());
                }
            });
        }

        private void HandleNewChildZonesAdded (IEnumerable<DockZone> added)
        {
            if (added.IsNullOrEmpty())
            {
                return;
            }

            var grid = this.GetFirstChildOf<AutoGrid>();
            if (grid == null)
            {
                return;
            }

            foreach (var zone in added)
            {
                if (zone.ViewModel.TakePassAlongUISize(out Size formerSize))
                {
                    grid.SetTargetFixedSizeFor(Grid.GetRow(zone), Grid.GetColumn(zone), formerSize);
                }
            }
        }

        private void HandleDragDropItemsSwapForHeaders (object sender, DragDropItemsSwapEventArgs e)
        {
            if (this.ViewModel.SwapDockedContentOrder(e.MoveFromIndex, e.MoveToIndex))
            {
                e.Handled = true;
            }
        }

        // ============================[ Events / Commands ]====================================

        // Identifies the group of docking the zone is part of, allows things to share docking
        public static DependencyProperty GroupIdProperty = APUtils.Register(GetGroupId, SetGroupId);
        public static string GetGroupId (DependencyObject obj) => (string)obj.GetValue(GroupIdProperty);
        public static void SetGroupId (DependencyObject obj, string value) => obj.SetValue(GroupIdProperty, value);

        public static RoutedEvent NotifyCloseSupressionEvent = REUtils.Register<RoutedEventHandler>(nameof(NotifyCloseSupressionEvent));
        public static RoutedUICommand CloseDockedContentCommand = new RoutedUICommand("Close", nameof(CloseDockedContentCommand), typeof(DockZone), new InputGestureCollection(new[] { new KeyGesture(Key.F4, ModifierKeys.Control) }));

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
                    if (this.ViewModel != null && this.ViewModel.Manager == null)
                    {
                        this.ViewModel.Manager = m_manager;
                    }

                    old?.StopTrackingSizingChanges(this);
                    m_manager?.TrackSizingChanges(this);
                    this.OnResetIsSetup();
                }
            }
        }

        private static readonly DependencyPropertyKey IsSetupPropertyKey = DPUtils.RegisterReadOnly(_ => _.IsSetup);
        public static readonly DependencyProperty IsSetupProperty = IsSetupPropertyKey.DependencyProperty;
        public bool IsSetup
        {
            get => (bool)this.GetValue(IsSetupProperty);
            private set => this.SetValue(IsSetupPropertyKey, value);
        }

        public static readonly DependencyProperty ViewModelProperty = DPUtils.Register(_ => _.ViewModel, (d, e) => d.OnViewModelChanged(e));
        public DockZoneViewModel ViewModel
        {
            get => (DockZoneViewModel)this.GetValue(ViewModelProperty);
            set => this.SetValue(ViewModelProperty, value);
        }

        private static readonly DependencyPropertyKey HasSplitZoneOrientationPropertyKey = DPUtils.RegisterReadOnly(_ => _.HasSplitZoneOrientation);
        public static readonly DependencyProperty HasSplitZoneOrientationProperty = HasSplitZoneOrientationPropertyKey.DependencyProperty;
        public bool HasSplitZoneOrientation
        {
            get => (bool)this.GetValue(HasSplitZoneOrientationProperty);
            private set => this.SetValue(HasSplitZoneOrientationPropertyKey, value);
        }

        private static readonly DependencyPropertyKey IsEmptyPropertyKey = DPUtils.RegisterReadOnly(_ => _.IsEmpty);
        public static readonly DependencyProperty IsEmptyProperty = IsEmptyPropertyKey.DependencyProperty;
        public bool IsEmpty
        {
            get => (bool)this.GetValue(IsEmptyProperty);
            private set => this.SetValue(IsEmptyPropertyKey, value);
        }

        public static readonly DependencyProperty SelectedIndexProperty = DPUtils.Register(_ => _.SelectedIndex, 0);
        public int SelectedIndex
        {
            get => (int)this.GetValue(SelectedIndexProperty);
            set => this.SetValue(SelectedIndexProperty, value);
        }

        public ReadOnlyObservableCollection<DockZone> ChildZones { get; }

        // ============================[ Private Utilities ]====================================
        private void OnCanCloseDockedContent (object sender, CanExecuteRoutedEventArgs e)
        {
            if (e.Parameter is DockingContentAdapterModel panelAdapter)
            {
                if (this.ViewModel.DockedContent.Contains(panelAdapter))
                {
                    e.CanExecute = true;
                }
            }
        }

        private void OnCloseDockedContent (object sender, ExecutedRoutedEventArgs e)
        {
            this.ViewModel.RequestCloseAndRemoveDockedContent((DockingContentAdapterModel)e.Parameter);
        }

        private void OnViewModelChanged (DependencyPropertyChangedEventArgs<DockZoneViewModel> e)
        {
            // If the old value still has a ref to this, clear it
            if (e.HasOldValue)
            {
                if (e.OldValue.UI == this)
                {
                    e.OldValue.UI = null;
                }

                ((INotifyCollectionChanged)e.OldValue.Children).CollectionChanged -= _OnDockZoneViewModelChildrenChanged;
                e.OldValue.PropertyChanged -= _OnViewModelPropertyChanged;
            }

            // Setup these and the new values
            if (e.HasNewValue)
            {
                this.Manager = e.NewValue.Manager;
                e.NewValue.UI = this;

                ((INotifyCollectionChanged)e.NewValue.Children).CollectionChanged -= _OnDockZoneViewModelChildrenChanged;
                ((INotifyCollectionChanged)e.NewValue.Children).CollectionChanged += _OnDockZoneViewModelChildrenChanged;

                e.NewValue.PropertyChanged -= _OnViewModelPropertyChanged;
                e.NewValue.PropertyChanged += _OnViewModelPropertyChanged;

                _InsertEach(0, e.NewValue.Children);
            }
            else
            {
                this.Manager = null;
            }

            this.OnResetIsSetup();

            void _OnDockZoneViewModelChildrenChanged (object sender, NotifyCollectionChangedEventArgs e)
            {
                if (e.OldItems != null)
                {
                    _RemoveAll(m_childZones.Where(c => e.OldItems.Contains(c.ViewModel)));
                }

                if (e.Action == NotifyCollectionChangedAction.Reset)
                {
                    _RemoveAll(m_childZones);
                }

                if (e.NewItems != null)
                {
                    _InsertEach(e.NewStartingIndex, e.NewItems);
                }
            }

            void _RemoveAll (IEnumerable<DockZone> toRemove)
            {
                var copy = toRemove.ToList();
                m_childZones.RemoveEach(copy);
                foreach (var rm in copy)
                {
                    rm.ViewModel?.DestroyUIReference();
                }
            }

            void _InsertEach (int index, IEnumerable children)
            {
                foreach (DockZoneViewModel child in children)
                {
                    m_childZones.Insert(index++, new DockZone() { ViewModel = child });
                }
            }

            void _OnViewModelPropertyChanged (object sender, System.ComponentModel.PropertyChangedEventArgs e)
            {
                this.HasSplitZoneOrientation = this.ViewModel == null
                                                ? false
                                                : this.ViewModel.Orientation.IsFlagInGroup(eDockOrientation.AnySplitOrientation);

                this.IsEmpty = this.ViewModel == null
                                ? true
                                : this.ViewModel.Orientation == eDockOrientation.Empty;
            }
        }

        private void OnResetIsSetup ()
        {
            this.IsSetup = this.ViewModel != null && this.Manager != null;
        }
    }
}
