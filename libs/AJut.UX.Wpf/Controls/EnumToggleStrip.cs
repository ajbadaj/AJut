namespace AJut.UX.Controls
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Linq;
    using System.Reflection;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Media;

    using DPUtils = AJut.UX.DPUtils<EnumToggleStrip>;

    // Wraps a ToggleStrip and infers the option list from the enum type of SelectedItem
    // (works the same way EnumComboBox does for ComboBox). Hidden values respect:
    //  * [Browsable(false)] from System.ComponentModel
    //  * [ExcludeFromSelection] from AJut
    //
    // Selection semantics:
    //  * Single-select (default): SelectedItem is the chosen value.
    //  * Multi-select on a [Flags] enum: SelectedItem is the OR'd combined value, SelectedItems is the per-bit list.
    //  * Multi-select on a non-[Flags] enum: SelectedItem is the most recently toggled value (OR is meaningless),
    //    SelectedItems is still the full list.
    [TemplatePart(Name = nameof(PART_ToggleStrip), Type = typeof(ToggleStrip))]
    public class EnumToggleStrip : Control
    {
        // ================== [ Fields ]================================
        private Type m_determinedItemType;
        private bool m_isFlagsEnum;
        private bool m_syncing;

        // ================== [ Construction ]================================
        static EnumToggleStrip ()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(EnumToggleStrip), new FrameworkPropertyMetadata(typeof(EnumToggleStrip)));
        }


        // ================== [ Dependency Properties ]================================
        public static readonly DependencyProperty SelectedItemProperty = DPUtils.Register(_ => _.SelectedItem, (d, e) => d.OnSelectedItemChanged(e.NewValue));
        public object SelectedItem
        {
            get => this.GetValue(SelectedItemProperty);
            set => this.SetValue(SelectedItemProperty, value);
        }

        public static readonly DependencyProperty SelectedItemsProperty = DPUtils.Register(_ => _.SelectedItems);
        public IList SelectedItems
        {
            get => (IList)this.GetValue(SelectedItemsProperty);
            set => this.SetValue(SelectedItemsProperty, value);
        }

        public static readonly DependencyProperty AllowMultiSelectProperty = DPUtils.Register(_ => _.AllowMultiSelect, false, (d, e) => d.RebuildItems());
        public bool AllowMultiSelect
        {
            get => (bool)this.GetValue(AllowMultiSelectProperty);
            set => this.SetValue(AllowMultiSelectProperty, value);
        }

        public static readonly DependencyProperty AllowNoSelectionProperty = DPUtils.Register(_ => _.AllowNoSelection, false, (d, e) => d.RebuildItems());
        public bool AllowNoSelection
        {
            get => (bool)this.GetValue(AllowNoSelectionProperty);
            set => this.SetValue(AllowNoSelectionProperty, value);
        }

        // ----- Forwarded styling DPs (mirror ToggleStrip - flow through via TemplateBinding in our template) -----

        public static readonly DependencyProperty ItemTemplateProperty = DPUtils.Register(_ => _.ItemTemplate);
        public DataTemplate ItemTemplate
        {
            get => (DataTemplate)this.GetValue(ItemTemplateProperty);
            set => this.SetValue(ItemTemplateProperty, value);
        }

        public static readonly DependencyProperty SeparatorBrushProperty = DPUtils.Register(_ => _.SeparatorBrush);
        public Brush SeparatorBrush
        {
            get => (Brush)this.GetValue(SeparatorBrushProperty);
            set => this.SetValue(SeparatorBrushProperty, value);
        }

        public static readonly DependencyProperty SeparatorThicknessProperty = DPUtils.Register(_ => _.SeparatorThickness, 1.0);
        public double SeparatorThickness
        {
            get => (double)this.GetValue(SeparatorThicknessProperty);
            set => this.SetValue(SeparatorThicknessProperty, value);
        }

        public static readonly DependencyProperty BackgroundPressedColorBaseProperty = DPUtils.Register(_ => _.BackgroundPressedColorBase);
        public Color BackgroundPressedColorBase
        {
            get => (Color)this.GetValue(BackgroundPressedColorBaseProperty);
            set => this.SetValue(BackgroundPressedColorBaseProperty, value);
        }

        public static readonly DependencyProperty ForegroundPressedProperty = DPUtils.Register(_ => _.ForegroundPressed);
        public Brush ForegroundPressed
        {
            get => (Brush)this.GetValue(ForegroundPressedProperty);
            set => this.SetValue(ForegroundPressedProperty, value);
        }

        public static readonly DependencyProperty BackgroundHoverProperty = DPUtils.Register(_ => _.BackgroundHover);
        public Brush BackgroundHover
        {
            get => (Brush)this.GetValue(BackgroundHoverProperty);
            set => this.SetValue(BackgroundHoverProperty, value);
        }

        public static readonly DependencyProperty BackgroundHoverOverPressedProperty = DPUtils.Register(_ => _.BackgroundHoverOverPressed);
        public Brush BackgroundHoverOverPressed
        {
            get => (Brush)this.GetValue(BackgroundHoverOverPressedProperty);
            set => this.SetValue(BackgroundHoverOverPressedProperty, value);
        }

        public static readonly DependencyProperty ForegroundHoverProperty = DPUtils.Register(_ => _.ForegroundHover);
        public Brush ForegroundHover
        {
            get => (Brush)this.GetValue(ForegroundHoverProperty);
            set => this.SetValue(ForegroundHoverProperty, value);
        }

        public static readonly DependencyProperty ItemPaddingProperty = DPUtils.Register(_ => _.ItemPadding, new Thickness(6));
        public Thickness ItemPadding
        {
            get => (Thickness)this.GetValue(ItemPaddingProperty);
            set => this.SetValue(ItemPaddingProperty, value);
        }

        public static readonly DependencyProperty CornerRadiusProperty = DPUtils.Register(_ => _.CornerRadius);
        public CornerRadius CornerRadius
        {
            get => (CornerRadius)this.GetValue(CornerRadiusProperty);
            set => this.SetValue(CornerRadiusProperty, value);
        }

        // ================== [ Properties ]================================
        public ToggleStrip PART_ToggleStrip { get; private set; }

        // ================== [ Public Interface Methods ]================================
        public override void OnApplyTemplate ()
        {
            if (this.PART_ToggleStrip != null)
            {
                this.PART_ToggleStrip.SelectionChanged -= this.PART_ToggleStrip_OnSelectionChanged;
            }

            this.PART_ToggleStrip = (ToggleStrip)this.GetTemplateChild(nameof(PART_ToggleStrip));
            if (this.PART_ToggleStrip != null)
            {
                this.PART_ToggleStrip.SelectionChanged += this.PART_ToggleStrip_OnSelectionChanged;

                // Pick up the type from whatever SelectedItem already has (data-before-template case)
                if (this.SelectedItem is Enum existing)
                {
                    m_determinedItemType = existing.GetType();
                    m_isFlagsEnum = m_determinedItemType.IsTaggedWithAttribute<FlagsAttribute>();
                }

                this.RebuildItems();
            }
        }

        // ================== [ Private Helper Methods ]================================
        private void OnSelectedItemChanged (object newValue)
        {
            if (m_syncing || this.PART_ToggleStrip == null || newValue is not Enum enumValue)
            {
                return;
            }

            Type itemType = enumValue.GetType();
            if (itemType != m_determinedItemType)
            {
                m_determinedItemType = itemType;
                m_isFlagsEnum = itemType.IsTaggedWithAttribute<FlagsAttribute>();
                this.RebuildItems();
                return;
            }

            this.PushSelectionFromValue(enumValue);
        }

        private void RebuildItems ()
        {
            if (this.PART_ToggleStrip == null || m_determinedItemType == null)
            {
                return;
            }

            m_syncing = true;
            try
            {
                this.PART_ToggleStrip.AllowMultiSelect = this.AllowMultiSelect;
                this.PART_ToggleStrip.AllowNoSelection = this.AllowNoSelection;
                this.PART_ToggleStrip.ItemsSource = GatherVisibleEnumValues(m_determinedItemType).ToArray();
            }
            finally
            {
                m_syncing = false;
            }

            if (this.SelectedItem is Enum currentValue && currentValue.GetType() == m_determinedItemType)
            {
                this.PushSelectionFromValue(currentValue);
            }
        }

        private static IEnumerable<Enum> GatherVisibleEnumValues (Type enumType)
        {
            foreach (FieldInfo field in enumType.GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                Enum value = (Enum)field.GetValue(null);

                BrowsableAttribute browsable = field.GetCustomAttribute<BrowsableAttribute>(false);
                if (browsable != null && !browsable.Browsable)
                {
                    continue;
                }

                if (field.IsDefined(typeof(ExcludeFromSelectionAttribute), false))
                {
                    continue;
                }

                yield return value;
            }
        }

        private void PushSelectionFromValue (Enum value)
        {
            m_syncing = true;
            try
            {
                if (this.AllowMultiSelect && m_isFlagsEnum)
                {
                    long combined = Convert.ToInt64(value);

                    // Select desired first, then deselect undesired - so CanBeDeselected stays true while we work
                    foreach (ToggleStrip.ToggleItem item in this.PART_ToggleStrip.Items)
                    {
                        long bits = Convert.ToInt64(item.Data);
                        bool shouldBeSelected = bits != 0 && (combined & bits) == bits;
                        if (shouldBeSelected && !item.IsSelected)
                        {
                            item.IsSelected = true;
                        }
                    }
                    foreach (ToggleStrip.ToggleItem item in this.PART_ToggleStrip.Items)
                    {
                        long bits = Convert.ToInt64(item.Data);
                        bool shouldBeSelected = bits != 0 && (combined & bits) == bits;
                        if (!shouldBeSelected && item.IsSelected)
                        {
                            item.IsSelected = false;
                        }
                    }
                }
                else
                {
                    // Single-select (or non-Flags multi-select): force this one on. For single-select the
                    // inner strip auto-deselects siblings; for non-Flags multi we leave existing on, since
                    // SelectedItem only carries one value of intent.
                    ToggleStrip.ToggleItem match = this.PART_ToggleStrip.Items.FirstOrDefault(i => value.Equals(i.Data));
                    if (match != null && !match.IsSelected)
                    {
                        match.IsSelected = true;
                    }
                }

                this.SelectedItems = this.PART_ToggleStrip.Items.Where(i => i.IsSelected).Select(i => i.Data).ToList();
            }
            finally
            {
                m_syncing = false;
            }
        }

        private void PART_ToggleStrip_OnSelectionChanged (object sender, ToggleStrip.ToggleStripSelectionChangedEventArgs e)
        {
            if (m_syncing || m_determinedItemType == null)
            {
                return;
            }

            m_syncing = true;
            try
            {
                this.SelectedItems = e.CurrentSelection;

                if (this.AllowMultiSelect && m_isFlagsEnum)
                {
                    long combined = 0;
                    foreach (object obj in e.CurrentSelection)
                    {
                        combined |= Convert.ToInt64(obj);
                    }
                    this.SelectedItem = Enum.ToObject(m_determinedItemType, combined);
                }
                else
                {
                    this.SelectedItem = this.PART_ToggleStrip.SelectedItem;
                }
            }
            finally
            {
                m_syncing = false;
            }
        }
    }
}
