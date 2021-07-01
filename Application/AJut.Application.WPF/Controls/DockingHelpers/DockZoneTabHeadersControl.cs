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
    using AJut.Application.Docking;
    using DPUtils = AJut.Application.DPUtils<DockZoneTabHeadersControl>;

    public class DockZoneTabHeadersControl : Control
    {
        private readonly ObservableCollection<HeaderItem> m_items = new ObservableCollection<HeaderItem>();
        static DockZoneTabHeadersControl ()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(DockZoneTabHeadersControl), new FrameworkPropertyMetadata(typeof(DockZoneTabHeadersControl)));
        }

        public DockZoneTabHeadersControl ()
        {
            this.Items = new ReadOnlyObservableCollection<HeaderItem>(m_items);
            this.CommandBindings.Add(new CommandBinding(SelectItemCommand, OnSelectedItem, OnCanSelectItem));
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

        public static RoutedUICommand SelectItemCommand = new RoutedUICommand("Select Item", nameof(SelectItemCommand), typeof(DockZoneTabHeadersControl));

        public static readonly DependencyProperty HeaderItemsSourceProperty = DPUtils.Register(_ => _.ItemsSource, (d,e)=>d.OnItemsSourceChanged(e));
        public IEnumerable ItemsSource
        {
            get => (IEnumerable)this.GetValue(HeaderItemsSourceProperty);
            set => this.SetValue(HeaderItemsSourceProperty, value);
        }

        private static readonly DependencyPropertyKey SelectedItemPropertyKey = DPUtils.RegisterReadOnly(_ => _.SelectedItem, (d,e)=>d.SetSelection(e.NewValue));
        public static readonly DependencyProperty SelectedItemProperty = SelectedItemPropertyKey.DependencyProperty;
        public HeaderItem SelectedItem
        {
            get => (HeaderItem)this.GetValue(SelectedItemProperty);
            protected set => this.SetValue(SelectedItemPropertyKey, value);
        }

        public ReadOnlyObservableCollection<HeaderItem> Items { get; }

        public static readonly DependencyProperty ItemsOrientationProperty = DPUtils.Register(_ => _.ItemsOrientation, Orientation.Horizontal);
        public Orientation ItemsOrientation
        {
            get => (Orientation)this.GetValue(ItemsOrientationProperty);
            set => this.SetValue(ItemsOrientationProperty, value);
        }

        private void SetSelection (HeaderItem newValue)
        {
            foreach (var item in this.Items.Where(i => i != newValue))
            {
                item.IsSelected = false;
            }

            newValue.IsSelected = true;
            this.SelectedItem = newValue;
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

                if (_didRemove && m_items.Count > 0 && !m_items.Any(i => i.IsSelected))
                {
                    int _newIndex = Math.Min(m_items.Count, lastSelectedIndex);
                    this.SetSelection(m_items[_newIndex]);
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
