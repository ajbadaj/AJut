namespace AJut.Application.Controls
{
    using System;
    using System.Collections;
    using System.Collections.ObjectModel;
    using System.Collections.Specialized;
    using System.Linq;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Input;
    using System.Windows.Media;
    using AJut.Application.Docking;
    using DPUtils = AJut.Application.DPUtils<DockZoneTabHeadersControl>;

    public class DockZoneTabHeadersControl : Control
    {
        private bool m_selectionReentrancyBlocker = false;

        public static RoutedUICommand SelectItemCommand = new RoutedUICommand("Select Item", nameof(SelectItemCommand), typeof(DockZoneTabHeadersControl));

        private readonly ObservableCollection<HeaderItem> m_items = new ObservableCollection<HeaderItem>();
        static DockZoneTabHeadersControl ()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(DockZoneTabHeadersControl), new FrameworkPropertyMetadata(typeof(DockZoneTabHeadersControl)));
        }

        public DockZoneTabHeadersControl ()
        {
            this.Items = new ReadOnlyObservableCollection<HeaderItem>(m_items);
            this.CommandBindings.Add(new CommandBinding(SelectItemCommand, OnSelectedItem, OnCanSelectItem));
            this.CommandBindings.Add(new CommandBinding(DragDropElement.HorizontalDragInitiatedCommand, OnInitiateElementReorder, CanInitiateElementReorder));
            this.CommandBindings.Add(new CommandBinding(DragDropElement.VerticalDragInitiatedCommand, OnInitiateTearOff, CanInitiateTearoff));
        }

        public static readonly DependencyProperty HeaderBorderProperty = DPUtils.Register(_ => _.HeaderBorder);
        public Brush HeaderBorder
        {
            get => (Brush)this.GetValue(HeaderBorderProperty);
            set => this.SetValue(HeaderBorderProperty, value);
        }

        public static readonly DependencyProperty HeaderBackgroundProperty = DPUtils.Register(_ => _.HeaderBackground);
        public Brush HeaderBackground
        {
            get => (Brush)this.GetValue(HeaderBackgroundProperty);
            set => this.SetValue(HeaderBackgroundProperty, value);
        }

        public static readonly DependencyProperty HeaderHighlightBackgroundProperty = DPUtils.Register(_ => _.HeaderHighlightBackground);
        public Brush HeaderHighlightBackground
        {
            get => (Brush)this.GetValue(HeaderHighlightBackgroundProperty);
            set => this.SetValue(HeaderHighlightBackgroundProperty, value);
        }

        public static readonly DependencyProperty HeaderSelectedBackgroundProperty = DPUtils.Register(_ => _.HeaderSelectedBackground);
        public Brush HeaderSelectedBackground
        {
            get => (Brush)this.GetValue(HeaderSelectedBackgroundProperty);
            set => this.SetValue(HeaderSelectedBackgroundProperty, value);
        }

        public static readonly DependencyProperty HeaderItemsSourceProperty = DPUtils.Register(_ => _.ItemsSource, (d,e)=>d.OnItemsSourceChanged(e));
        public IEnumerable ItemsSource
        {
            get => (IEnumerable)this.GetValue(HeaderItemsSourceProperty);
            set => this.SetValue(HeaderItemsSourceProperty, value);
        }

        private static readonly DependencyPropertyKey SelectedItemPropertyKey = DPUtils.RegisterReadOnly(_ => _.SelectedItem, (d,e)=>d.OnSelectedItemChanged(e));

        public static readonly DependencyProperty SelectedItemProperty = SelectedItemPropertyKey.DependencyProperty;
        public HeaderItem SelectedItem
        {
            get => (HeaderItem)this.GetValue(SelectedItemProperty);
            protected set => this.SetValue(SelectedItemPropertyKey, value);
        }


        public static readonly DependencyProperty SelectedIndexProperty = DPUtils.Register(_ => _.SelectedIndex, (d,e)=>d.OnSelectedIndexChanged(e));
        public int SelectedIndex
        {
            get => (int)this.GetValue(SelectedIndexProperty);
            set => this.SetValue(SelectedIndexProperty, value);
        }


        public ReadOnlyObservableCollection<HeaderItem> Items { get; }

        public static readonly DependencyProperty ItemsOrientationProperty = DPUtils.Register(_ => _.ItemsOrientation, Orientation.Horizontal);
        public Orientation ItemsOrientation
        {
            get => (Orientation)this.GetValue(ItemsOrientationProperty);
            set => this.SetValue(ItemsOrientationProperty, value);
        }

        private void OnSelectedIndexChanged (DependencyPropertyChangedEventArgs<int> e)
        {
            if (m_selectionReentrancyBlocker)
            {
                return;
            }

            try
            {
                m_selectionReentrancyBlocker = true;
                if (e.HasNewValue && e.NewValue > 0 && e.NewValue < this.Items.Count)
                {
                    this.SelectedItem = this.Items[e.NewValue];
                }
                else
                {
                    this.SelectedItem = null;
                }
            }
            finally
            {
                m_selectionReentrancyBlocker = false;
            }
        }

        private void OnSelectedItemChanged (DependencyPropertyChangedEventArgs<HeaderItem> e)
        {
            if (m_selectionReentrancyBlocker)
            {
                return;
            }

            try
            {
                m_selectionReentrancyBlocker = true;
                this.SetSelection(e.NewValue);
            }
            finally
            {
                m_selectionReentrancyBlocker = false;
            }
        }

        private void OnCanSelectItem (object sender, CanExecuteRoutedEventArgs e)
        {
            if ((((e.OriginalSource as FrameworkElement)?.DataContext as HeaderItem)?.IsSelected ?? true) == false)
            {
                e.CanExecute = true;
            }
        }

        private void OnSelectedItem (object sender, ExecutedRoutedEventArgs e)
        {
            this.SetSelection((HeaderItem)((FrameworkElement)e.OriginalSource).DataContext);
        }

        private void SetSelection (HeaderItem newValue)
        {
            foreach (var item in this.Items)
            {
                item.IsSelected = false;
            }

            if (newValue != null)
            {
                newValue.IsSelected = true;
                this.SelectedItem = newValue;
                this.SelectedIndex = this.Items.IndexOf(this.SelectedItem);
            }
            else
            {
                this.SelectedItem = null;
                this.SelectedIndex = -1;
            }

            
        }

        private void CanInitiateElementReorder (object sender, CanExecuteRoutedEventArgs e)
        {
            if (DragDropElement.CanDoDragReorder((UIElement)e.OriginalSource, e.Parameter as ActiveDragTracking))
            {
                e.CanExecute = true;
            }
        }

        DockingContentAdapterModel m_activeReorderTarget;
        private async void OnInitiateElementReorder (object sender, ExecutedRoutedEventArgs e)
        {
            var dragTracking = (ActiveDragTracking)e.Parameter;
            if (dragTracking.SenderContext is HeaderItem header)
            {
                m_activeReorderTarget = header.Adapter;
                header.IsDragging = true;
                try
                {
                    await DragDropElement.DoDragReorder((UIElement)e.OriginalSource, dragTracking).ConfigureAwait(false);
                }
                finally
                {
                    var newHeader = m_items.First(h => h.Adapter == m_activeReorderTarget);
                    newHeader.IsDragging = false;
                    m_activeReorderTarget = null;
                }
            }
        }

        private void CanInitiateTearoff (object sender, CanExecuteRoutedEventArgs e)
        {
            if (e.Parameter is ActiveDragTracking)
            {
                e.CanExecute = true;
            }
        }

        private async void OnInitiateTearOff (object sender, ExecutedRoutedEventArgs e)
        {
            var tracking = (ActiveDragTracking)e.Parameter;

            var window = Window.GetWindow(tracking.DragOwner);
            Point desktopMouseLocation = (Point)((Vector)window.PointToScreen(tracking.DragOwner.TranslatePoint(tracking.StartPoint, window)) - (Vector)tracking.StartPoint);

            DockingContentAdapterModel target = ((HeaderItem)tracking.SenderContext).Adapter;
            var result = target.DockingOwner.DoTearoff(target.Display, desktopMouseLocation);
            if (result)
            {
                await target.DockingOwner.RunDragSearch(result.Value, target.Location).ConfigureAwait(false);
            }
        }

        private void OnItemsSourceChanged (DependencyPropertyChangedEventArgs<IEnumerable> e)
        {
            if (e.OldValue is INotifyCollectionChanged oldOC)
            {
                oldOC.CollectionChanged -= _OnItemsSourceCollectionChanged;
            }
            m_items.Clear();

            if (e.HasNewValue)
            {
                m_items.AddEach(e.NewValue.OfType<DockingContentAdapterModel>().Select(a => new HeaderItem(a)));
                if (e.NewValue is INotifyCollectionChanged newOC)
                {
                    newOC.CollectionChanged += _OnItemsSourceCollectionChanged;
                }

                if (this.SelectedItem == null)
                {
                    this.SetSelection(m_items.First());
                }
            }

            void _OnItemsSourceCollectionChanged (object _sender, NotifyCollectionChangedEventArgs _e)
            {
                if (_e.NewItems != null)
                {
                    m_items.InsertEach(_e.NewStartingIndex, _e.NewItems.OfType<DockingContentAdapterModel>().Select(a => new HeaderItem(a)));
                }

                int lastSelectedIndex = m_items.FirstIndexMatching(i => i.IsSelected);
                bool _didRemove = false;
                if (_e.OldItems != null)
                {
                    m_items.RemoveAll(i => _e.OldItems.Contains(i.Adapter));
                    _didRemove = true;
                }
                if (_e.Action == NotifyCollectionChangedAction.Reset)
                {
                    m_items.Clear();
                    _didRemove = true;
                }

                if (m_activeReorderTarget != null)
                {
                    var newHeader = m_items.FirstOrDefault(h => h.Adapter == m_activeReorderTarget);
                    if (newHeader != null)
                    {
                        newHeader.IsDragging = true;
                        this.SetSelection(newHeader);
                    }
                }
                else if (_didRemove && m_items.Count > 0 && !m_items.Any(i => i.IsSelected))
                {
                    int _newIndex = Math.Min(m_items.Count - 1, lastSelectedIndex);
                    if (_newIndex >= 0)
                    {
                        this.SetSelection(m_items[_newIndex]);
                    }
                }
            }
        }

        public class HeaderItem : NotifyPropertyChanged
        {
            private bool m_isSelected;
            private bool m_isDragging;

            public HeaderItem (DockingContentAdapterModel adapter)
            {
                this.Adapter = adapter;
            }

            public DockingContentAdapterModel Adapter { get; }

            public bool IsSelected
            {
                get => m_isSelected;
                set => this.SetAndRaiseIfChanged(ref m_isSelected, value);
            }

            public bool IsDragging
            {
                get => m_isDragging;
                set => this.SetAndRaiseIfChanged(ref m_isDragging, value);
            }
        }
    }
}
