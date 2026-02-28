namespace AJut.UX.Controls
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Collections.Specialized;
    using System.ComponentModel;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;
    using Microsoft.UI.Xaml.Data;
    using Microsoft.UI.Xaml.Media;
    using DPUtils = AJut.UX.DPUtils<ToggleStrip>;

    // ===========[ ToggleStrip ]================================================
    // A horizontal (or vertical) strip of mutually exclusive toggle buttons.
    // Exactly one item is selected at a time (unless AllowNoSelection = true).
    // Items are provided via ItemsSource or built manually via the Items collection.
    //
    // Template parts:
    //   PART_ItemsPanel  - StackPanel that holds the generated toggle buttons
    //
    // Note: CornerRadius distribution and BorderThickness are intentionally set in
    // code (CreateItemButton) because they are index-aware - the first and last buttons
    // receive rounded corners matching the strip's CornerRadius DP, while interior
    // buttons receive squared corners. These are structural, not stylistic, assignments.
    // Button Padding comes from the ItemPadding DP (default 8,4); callers can override
    // via that DP or via ToggleButtonItemStyle.

    [TemplatePart(Name = nameof(PART_ItemsPanel), Type = typeof(StackPanel))]
    public class ToggleStrip : Control
    {
        // ===========[ Const-like ]===============================================
        private static readonly PropertyPath kIsSelectedPath = new PropertyPath("IsSelected");
        private static readonly PropertyPath kNamePath = new PropertyPath("Name");
        private static readonly PropertyPath kBorderBrushPath = new PropertyPath("BorderBrush");

        // ===========[ Instance fields ]==========================================
        private StackPanel PART_ItemsPanel;
        private bool m_isChangingSelectedItem;
        private bool m_isChangingSelectedItemsList;

        // ===========[ Construction ]=============================================
        public ToggleStrip ()
        {
            this.DefaultStyleKey = typeof(ToggleStrip);
            this.Items = new ToggleItemsCollection(this);
            this.Items.CollectionChanged += this.Items_OnCollectionChanged;
        }

        // ===========[ Events ]===================================================
        public event EventHandler<ToggleStripSelectionChangedEventArgs> SelectionChanged;

        // ===========[ Dependency Properties ]====================================

        public static readonly DependencyProperty ItemsSourceProperty = DPUtils.Register(_ => _.ItemsSource, (d, e) => d.OnItemsSourceChanged(e));
        public IEnumerable ItemsSource
        {
            get => (IEnumerable)this.GetValue(ItemsSourceProperty);
            set => this.SetValue(ItemsSourceProperty, value);
        }

        public static readonly DependencyProperty AllowMultiSelectProperty = DPUtils.Register(_ => _.AllowMultiSelect, false);
        public bool AllowMultiSelect
        {
            get => (bool)this.GetValue(AllowMultiSelectProperty);
            set => this.SetValue(AllowMultiSelectProperty, value);
        }

        public static readonly DependencyProperty AllowNoSelectionProperty = DPUtils.Register(_ => _.AllowNoSelection, false);
        public bool AllowNoSelection
        {
            get => (bool)this.GetValue(AllowNoSelectionProperty);
            set => this.SetValue(AllowNoSelectionProperty, value);
        }

        public static readonly DependencyProperty SelectedItemProperty = DPUtils.Register(_ => _.SelectedItem, (d, e) => d.OnSelectedItemChanged(e));
        public object SelectedItem
        {
            get => this.GetValue(SelectedItemProperty);
            set => this.SetValue(SelectedItemProperty, value);
        }

        public static readonly DependencyProperty SelectedItemsProperty = DPUtils.Register(_ => _.SelectedItems, (d, e) => d.OnSelectedItemsChanged(e));
        public IList SelectedItems
        {
            get => (IList)this.GetValue(SelectedItemsProperty);
            set => this.SetValue(SelectedItemsProperty, value);
        }

        public static readonly DependencyProperty DisplayPropertyPathProperty = DPUtils.Register(_ => _.DisplayPropertyPath, "", (d, e) => d.OnDisplayPropertyPathChanged(e.NewValue));
        public string DisplayPropertyPath
        {
            get => (string)this.GetValue(DisplayPropertyPathProperty);
            set => this.SetValue(DisplayPropertyPathProperty, value);
        }

        public static readonly DependencyProperty ItemTemplateProperty = DPUtils.Register(_ => _.ItemTemplate, (d, e) => d.RebuildItems());
        public DataTemplate ItemTemplate
        {
            get => (DataTemplate)this.GetValue(ItemTemplateProperty);
            set => this.SetValue(ItemTemplateProperty, value);
        }

        public static readonly DependencyProperty SeparatorThicknessProperty = DPUtils.Register(_ => _.SeparatorThickness, 1.0, (d, e) => d.RebuildItems());
        public double SeparatorThickness
        {
            get => (double)this.GetValue(SeparatorThicknessProperty);
            set => this.SetValue(SeparatorThicknessProperty, value);
        }

        public static readonly DependencyProperty HasItemsProperty = DPUtils.Register(_ => _.HasItems, false);
        public bool HasItems
        {
            get => (bool)this.GetValue(HasItemsProperty);
            private set => this.SetValue(HasItemsProperty, value);
        }

        public static readonly DependencyProperty ItemsProperty = DPUtils.Register(_ => _.Items);
        public ToggleItemsCollection Items
        {
            get => (ToggleItemsCollection)this.GetValue(ItemsProperty);
            private set => this.SetValue(ItemsProperty, value);
        }

        // ToggleButtonItemStyle: optional Style applied to each generated ToggleStripButton.
        // Use this to customize button appearance (font, colors, etc.) without subclassing.
        // Note: style TargetType must be ToggleStripButton or a compatible base type.
        // Code still sets CornerRadius, BorderThickness, and Padding (via ItemPadding) after
        // applying this style because those values are index-aware or DP-driven.
        public static readonly DependencyProperty ToggleButtonItemStyleProperty = DPUtils.Register(_ => _.ToggleButtonItemStyle, (d, e) => d.RebuildItems());
        public Style ToggleButtonItemStyle
        {
            get => (Style)this.GetValue(ToggleButtonItemStyleProperty);
            set => this.SetValue(ToggleButtonItemStyleProperty, value);
        }

        // ItemPadding: applied to each generated ToggleButton as a local value.
        // Default set in XAML style; override via this DP to adjust without replacing ToggleButtonItemStyle.
        public static readonly DependencyProperty ItemPaddingProperty = DPUtils.Register(_ => _.ItemPadding, new Thickness(8, 4, 8, 4), (d, e) => d.RebuildItems());
        public Thickness ItemPadding
        {
            get => (Thickness)this.GetValue(ItemPaddingProperty);
            set => this.SetValue(ItemPaddingProperty, value);
        }

        // ===========[ Template application ]=====================================
        protected override void OnApplyTemplate ()
        {
            base.OnApplyTemplate();

            this.PART_ItemsPanel = (StackPanel)this.GetTemplateChild(nameof(PART_ItemsPanel));
            this.RebuildItems();
        }

        // ===========[ Private handlers ]=========================================
        private void Items_OnCollectionChanged (object sender, NotifyCollectionChangedEventArgs e)
        {
            this.HasItems = this.Items.Count > 0;
            this.Items.ForEach(i => i.EvaluateOrder());
            this.RebuildItems();
        }

        private void OnDisplayPropertyPathChanged (string newPath)
        {
            this.Items.ForEach(i => i.ResetName(newPath));
        }

        private void OnItemsSourceChanged (DependencyPropertyChangedEventArgs<IEnumerable> e)
        {
            this.SelectedItem = null;
            if (this.SelectedItems is INotifyCollectionChanged)
            {
                this.SelectedItems.Clear();
            }
            else
            {
                this.SelectedItems = new List<object>();
            }

            if (e.OldValue is INotifyCollectionChanged oldSource)
            {
                oldSource.CollectionChanged -= this.ItemsSource_OnCollectionChanged;
            }

            if (e.NewValue is INotifyCollectionChanged newSource)
            {
                newSource.CollectionChanged += this.ItemsSource_OnCollectionChanged;
            }

            if (e.NewValue == null)
            {
                this.Items.Clear();
            }
            else
            {
                int index = 0;
                this.Items.ReplaceAllWith(
                    e.NewValue.OfType<object>().Select(i => new ToggleItem(this, i, index++, this.DisplayPropertyPath))
                );

                // Select the first item by default if not allowing empty selection
                if (this.Items.Count > 0 && (this.SelectedItems == null || this.SelectedItems.Count == 0) && !this.AllowNoSelection)
                {
                    this.Items[0].IsSelected = true;
                }
            }
        }

        private void ItemsSource_OnCollectionChanged (object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                this.Items.Clear();
                return;
            }

            IEnumerable<ToggleItem> addedItems = e.NewItems != null
                ? e.NewItems.OfType<object>().Select(i => new ToggleItem(this, i, -1, this.DisplayPropertyPath))
                : Enumerable.Empty<ToggleItem>();

            IEnumerable<ToggleItem> removedItems = e.OldItems != null
                ? this.Items.Where(ti => e.OldItems.Contains(ti.Data))
                : Enumerable.Empty<ToggleItem>();

            this.Items.AddAndRemove(addedItems, removedItems);
        }

        private void OnSelectedItemChanged (DependencyPropertyChangedEventArgs<object> e)
        {
            if (m_isChangingSelectedItem)
            {
                return;
            }

            if (e.NewValue == null)
            {
                this.PerformModificationWithBlocker(ref m_isChangingSelectedItemsList, () =>
                {
                    if (this.SelectedItems is INotifyCollectionChanged)
                    {
                        this.SelectedItems.Clear();
                    }
                    else
                    {
                        this.SelectedItems = null;
                    }
                });
                return;
            }

            ToggleItem newlySelected = this.Items.FirstOrDefault(ti => ti.Data == e.NewValue);
            if (newlySelected != null)
            {
                newlySelected.IsSelected = true;
            }
        }

        private void OnSelectedItemsChanged (DependencyPropertyChangedEventArgs<IList> e)
        {
            // External changes to SelectedItems are not re-processed to avoid cycles.
            // Selection is driven through SelectedItem or by clicking items directly.
        }

        internal void HandleSelectionChanged (ToggleItem latest)
        {
            IList previousSelectedItems = this.SelectedItems;
            IList newlySelectedItems;

            if (this.AllowMultiSelect)
            {
                newlySelectedItems = this.Items.Where(ti => ti.IsSelected).Select(ti => ti.Data).ToList();
            }
            else
            {
                newlySelectedItems = new List<object> { latest.Data };
            }

            this.PerformModificationWithBlocker(ref m_isChangingSelectedItem,
                () => this.SelectedItem = latest.Data
            );

            this.PerformModificationWithBlocker(ref m_isChangingSelectedItemsList, () =>
            {
                if (this.SelectedItems is INotifyCollectionChanged)
                {
                    this.SelectedItems.Clear();
                    foreach (object obj in newlySelectedItems)
                    {
                        this.SelectedItems.Add(obj);
                    }
                }
                else
                {
                    this.SelectedItems = newlySelectedItems;
                }
            });

            this.SelectionChanged?.Invoke(
                this,
                new ToggleStripSelectionChangedEventArgs(previousSelectedItems, newlySelectedItems)
            );
        }

        // ===========[ Item building ]============================================
        private void RebuildItems ()
        {
            if (this.PART_ItemsPanel == null)
            {
                return;
            }

            this.PART_ItemsPanel.Children.Clear();

            int totalItems = this.Items.Count;
            int index = 0;
            foreach (ToggleItem item in this.Items)
            {
                var btn = this.CreateItemButton(item, index, isLast: index == totalItems - 1);
                this.PART_ItemsPanel.Children.Add(btn);
                ++index;
            }
        }

        private ToggleStripButton CreateItemButton (ToggleItem item, int index, bool isLast)
        {
            var btn = new ToggleStripButton();

            // Apply caller-provided style first so structural overrides below
            // (CornerRadius, BorderThickness, Padding) are set as local values and take precedence.
            if (this.ToggleButtonItemStyle != null)
            {
                btn.Style = this.ToggleButtonItemStyle;
            }

            btn.Padding = this.ItemPadding;

            // 1. Left-edge separator border: first button has no border; others show a
            //    1px left-only divider using the strip's BorderBrush.
            btn.BorderThickness = index == 0
                ? new Thickness(0)
                : new Thickness(this.SeparatorThickness, 0, 0, 0);

            btn.SetBinding(Control.BorderBrushProperty, new Binding
            {
                Source = this,
                Path = kBorderBrushPath,
                Mode = BindingMode.OneWay,
            });

            // 2. Corner radius: distribute the strip's outer CornerRadius to each button
            //    so the group reads as one cohesive control with a shared silhouette.
            bool isFirst = index == 0;
            CornerRadius cr = this.CornerRadius;
            if (isFirst && isLast)
            {
                btn.CornerRadius = cr;
            }
            else if (isFirst)
            {
                btn.CornerRadius = new CornerRadius(cr.TopLeft, 0, 0, cr.BottomLeft);
            }
            else if (isLast)
            {
                btn.CornerRadius = new CornerRadius(0, cr.TopRight, cr.BottomRight, 0);
            }
            else
            {
                btn.CornerRadius = new CornerRadius(0);
            }

            // 3. Bind IsSelected ↔ IsSelected (TwoWay so clicking drives selection logic)
            btn.SetBinding(ToggleStripButton.IsSelectedProperty, new Binding
            {
                Source = item,
                Path = kIsSelectedPath,
                Mode = BindingMode.TwoWay,
            });

            if (this.ItemTemplate != null)
            {
                // Custom template: show item.Data through the user-supplied template
                btn.Content = item.Data;
                btn.ContentTemplate = this.ItemTemplate;
            }
            else
            {
                // Default: show the display name (handles DisplayPropertyPath)
                btn.SetBinding(ContentControl.ContentProperty, new Binding
                {
                    Source = item,
                    Path = kNamePath,
                });
            }

            return btn;
        }

        // ===========[ Helpers ]==================================================
        private void PerformModificationWithBlocker (ref bool blocker, Action action)
        {
            blocker = true;
            try
            {
                action();
            }
            finally
            {
                blocker = false;
            }
        }

        // ===========[ Subclasses ]===============================================

        public class ToggleItem : INotifyPropertyChanged
        {
            // ===========[ Fields ]===============================================
            private string m_name;
            private bool m_isSelected;
            private int m_itemIndex;
            private bool m_hasDefaultName;
            private bool m_isFirstItem;
            private bool m_isLastItem;

            // ===========[ Construction ]=========================================
            internal ToggleItem (ToggleStrip owner, object data, int index, string displayPropertyPath)
            {
                this.Data = data;
                m_itemIndex = index;
                this.Owner = owner;
                this.ResetName(displayPropertyPath);
            }

            // ===========[ Events ]===============================================
            public event PropertyChangedEventHandler PropertyChanged;

            // ===========[ Properties ]==========================================
            public object Data { get; }
            public ToggleStrip Owner { get; }

            public int ItemIndex
            {
                get => m_itemIndex;
                internal set
                {
                    if (this.SetAndRaiseIfChanged(ref m_itemIndex, value))
                    {
                        this.IsFirstItem = (m_itemIndex == 0);
                        this.IsLastItem = (this.Owner.Items.LastOrDefault() == this);
                        if (m_hasDefaultName)
                        {
                            this.ResetNameWithDefault();
                        }
                    }
                }
            }

            public string Name
            {
                get => m_name;
                set => this.SetAndRaiseIfChanged(ref m_name, value);
            }

            public bool IsSelected
            {
                get => m_isSelected;
                set
                {
                    if (!value && !this.Owner.Items.CanBeDeselected())
                    {
                        // Deselect rejected - raise PropertyChanged to revert the ToggleButton's IsChecked
                        this.RaisePropertyChanged(nameof(IsSelected));
                        return;
                    }

                    if (this.SetAndRaiseIfChanged(ref m_isSelected, value))
                    {
                        if (value)
                        {
                            this.Owner.Items.HandleWasSelected(this);
                        }
                        else
                        {
                            this.Owner.Items.HandleWasDeselected(this);
                        }
                    }
                }
            }

            public bool IsFirstItem
            {
                get => m_isFirstItem;
                private set => this.SetAndRaiseIfChanged(ref m_isFirstItem, value);
            }

            public bool IsLastItem
            {
                get => m_isLastItem;
                private set => this.SetAndRaiseIfChanged(ref m_isLastItem, value);
            }

            // ===========[ Internal methods ]=====================================
            internal void EvaluateOrder ()
            {
                m_itemIndex = -1;
                this.ItemIndex = this.Owner.Items.IndexOf(this);
            }

            internal void ResetName (string displayPropertyPath = null)
            {
                if (displayPropertyPath?.Length > 0)
                {
                    try
                    {
                        var prop = this.Data?.GetType().GetProperty(displayPropertyPath);
                        string resolved = prop?.GetValue(this.Data)?.ToString();
                        if (resolved != null)
                        {
                            this.Name = resolved;
                            m_hasDefaultName = false;
                            return;
                        }
                    }
                    catch { }
                }

                this.ResetNameWithDefault();
            }

            internal void SetSelectionWithoutChecking (bool isSelected)
            {
                m_isSelected = isSelected;
                this.RaisePropertyChanged(nameof(IsSelected));
                if (!isSelected)
                {
                    this.Owner.Items.HandleWasDeselected(this);
                }
            }

            // ===========[ Helpers ]=============================================
            private bool SetAndRaiseIfChanged<T> (ref T field, T value, [CallerMemberName] string propertyName = null)
            {
                if (EqualityComparer<T>.Default.Equals(field, value))
                {
                    return false;
                }

                field = value;
                this.RaisePropertyChanged(propertyName);
                return true;
            }

            private void RaisePropertyChanged (string propertyName)
                => this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

            private void ResetNameWithDefault ()
            {
                // Use Data.ToString() when it is meaningfully overridden (i.e. not
                // just the default type-name). Works naturally for strings, enums,
                // and value types. Falls back to positional label otherwise.
                string fromData = this.Data?.ToString();
                bool usable = fromData != null && fromData != this.Data.GetType().FullName;
                this.Name = usable ? fromData : $"Item {m_itemIndex}";
                m_hasDefaultName = !usable;
            }
        }

        public class ToggleItemsCollection : ObservableCollection<ToggleItem>
        {
            // ===========[ Fields ]===============================================
            private ToggleStrip m_owner;

            // ===========[ Construction ]=========================================
            internal ToggleItemsCollection (ToggleStrip owner)
            {
                m_owner = owner;
                this.CollectionChanged += this.Self_OnCollectionChanged;
            }

            // ===========[ Internal methods ]=====================================
            internal bool CanBeDeselected ()
                => m_owner.AllowNoSelection || (m_owner.SelectedItems?.Count ?? 0) > 1;

            internal void HandleWasSelected (ToggleItem newlySelected)
            {
                if (!m_owner.AllowMultiSelect)
                {
                    foreach (ToggleItem item in this)
                    {
                        if (item != newlySelected)
                        {
                            item.SetSelectionWithoutChecking(false);
                        }
                    }
                }

                m_owner.HandleSelectionChanged(newlySelected);
            }

            internal void HandleWasDeselected (ToggleItem toggleItem)
            {
                if (m_owner.SelectedItems is INotifyCollectionChanged)
                {
                    m_owner.SelectedItems.Remove(toggleItem.Data);
                    if (m_owner.SelectedItem == toggleItem.Data)
                    {
                        m_owner.SelectedItem = m_owner.SelectedItems.OfType<object>().LastOrDefault();
                    }
                }
                else if (m_owner.SelectedItems != null)
                {
                    m_owner.SelectedItems = m_owner.SelectedItems.OfType<object>()
                        .Where(i => i != toggleItem.Data)
                        .ToList<object>();
                }
            }

            internal void ReplaceAllWith (IEnumerable<ToggleItem> items)
            {
                this.Clear();
                this.AddEach(items);
            }

            internal void AddAndRemove (IEnumerable<ToggleItem> addedItems, IEnumerable<ToggleItem> removedItems)
            {
                this.AddEach(addedItems);
                this.RemoveEach(removedItems);
            }

            // ===========[ Private handlers ]=====================================
            private void Self_OnCollectionChanged (object sender, NotifyCollectionChangedEventArgs e)
            {
                int index = 0;
                this.ForEach(ti => ti.ItemIndex = index++);
            }
        }

        public class ToggleStripSelectionChangedEventArgs : EventArgs
        {
            public ToggleStripSelectionChangedEventArgs (IList previousSelection, IList currentSelection)
            {
                this.PreviousSelection = previousSelection;
                this.CurrentSelection = currentSelection;
            }

            public IList PreviousSelection { get; }
            public IList CurrentSelection { get; }
        }
    }
}
