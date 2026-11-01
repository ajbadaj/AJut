namespace AJut.UX.Controls
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using Android.Content;
    using Android.Content.Res;
    using Android.Runtime;
    using Android.Util;
    using Android.Views;
    using Android.Widget;

    /// <summary>
    /// A <see cref="ToggleStrip"/> of an enum's values, inferred from the type of <see cref="SelectedItem"/>. The options
    /// come from AJut.UX's <see cref="EnumSelectionOptions"/>, so values marked [Browsable(false)] or
    /// [ExcludeFromSelection] are left out, the same on every AJut toolkit.
    /// </summary>
    /// <remarks>
    /// Selection works the way WinUI3's EnumToggleStrip does:
    /// <list type="bullet">
    /// <item>Single-select (the default): <see cref="SelectedItem"/> is the chosen value.</item>
    /// <item>Multi-select on a [Flags] enum: <see cref="SelectedItem"/> is the combined value, and <see cref="SelectedItems"/> holds each flag.</item>
    /// <item>Multi-select on any other enum: <see cref="SelectedItem"/> is the most recently toggled value, and <see cref="SelectedItems"/> holds them all.</item>
    /// </list>
    /// Set SelectedItem from code to pick the enum; in a layout, ajutAllowMultiSelect, ajutAllowNoSelection,
    /// ajutEnsureUniformSize, ajutOverflowBehavior, ajutOrientation, android:enabled and the look's ajutCornerRadius,
    /// ajutSeparatorThickness, ajutItemPadding, ajutItemPaddingHorizontal and ajutItemPaddingVertical pass through to the
    /// strip.
    /// </remarks>
    [Register("ajut.ux.controls.EnumToggleStrip")]
    public class EnumToggleStrip : FrameLayout
    {
        private ToggleStrip m_strip = null!;
        private Enum? m_valueOfTheEnumType;
        private bool m_isSyncing;
        private object? m_selectedItem;
        private IList m_selectedItems = new List<object>();

        // ===========[ Setup/Construction/Teardown ]===================================
        public EnumToggleStrip (Context context) : base(context)
        {
            this.Initialize(context, null);
        }

        public EnumToggleStrip (Context context, IAttributeSet? attrs) : base(context, attrs)
        {
            this.Initialize(context, attrs);
        }

        public EnumToggleStrip (Context context, IAttributeSet? attrs, int defStyleAttr) : base(context, attrs, defStyleAttr)
        {
            this.Initialize(context, attrs);
        }

        protected EnumToggleStrip (IntPtr javaReference, JniHandleOwnership transfer) : base(javaReference, transfer)
        {
        }

        protected override void OnAttachedToWindow ()
        {
            base.OnAttachedToWindow();
            m_strip.SelectionChanged -= this.OnStripSelectionChanged;
            m_strip.SelectionChanged += this.OnStripSelectionChanged;
        }

        protected override void OnDetachedFromWindow ()
        {
            m_strip.SelectionChanged -= this.OnStripSelectionChanged;
            base.OnDetachedFromWindow();
        }

        // ===========[ Events ]===================================
        /// <summary>Raised when <see cref="SelectedItem"/> changes, from a tap or from code. Bind two-way through this.</summary>
        public event EventHandler<EventArgs<object?>>? SelectedItemChanged;

        // ===========[ Properties ]===================================
        /// <summary>The selected enum value (the combined value for a multi-select [Flags] strip). Its type picks the options.</summary>
        public object? SelectedItem
        {
            get => m_selectedItem;
            set
            {
                if (Equals(m_selectedItem, value))
                {
                    return;
                }

                m_selectedItem = value;
                if (!m_isSyncing && (value is Enum enumValue))
                {
                    if (enumValue.GetType() != m_valueOfTheEnumType?.GetType())
                    {
                        m_valueOfTheEnumType = enumValue;
                        this.RebuildItems();
                    }
                    else
                    {
                        this.PushSelectionFromValue(enumValue);
                    }
                }

                this.SelectedItemChanged?.Invoke(this, new EventArgs<object?>(value));
            }
        }

        /// <summary>Every selected value, in strip order.</summary>
        public IList SelectedItems => m_selectedItems;

        /// <summary>Whether more than one value can be selected; on a [Flags] enum they combine into <see cref="SelectedItem"/>.</summary>
        public bool AllowMultiSelect
        {
            get => m_strip.AllowMultiSelect;
            set
            {
                m_strip.AllowMultiSelect = value;
                this.RebuildItems();
            }
        }

        /// <summary>Whether the last selected value can be deselected, leaving nothing selected.</summary>
        public bool AllowNoSelection
        {
            get => m_strip.AllowNoSelection;
            set
            {
                m_strip.AllowNoSelection = value;
                this.RebuildItems();
            }
        }

        /// <summary>Whether the values take taps; passed on to the strip, which a ViewGroup's own Enabled does not reach.</summary>
        public override bool Enabled
        {
            get => base.Enabled;
            set
            {
                base.Enabled = value;

                // The strip is made in Initialize, so it is not there yet when the base constructor runs
                if (m_strip != null)
                {
                    m_strip.Enabled = value;
                }
            }
        }

        /// <summary>The strip that shows the values, for its layout settings (EnsureUniformSize, OverflowBehavior, Orientation) and its look (CornerRadius, SeparatorThickness, ItemPadding).</summary>
        public ToggleStrip Strip => m_strip;

        // ===========[ Helper Methods ]===================================
        private void Initialize (Context context, IAttributeSet? attrs)
        {
            m_strip = new ToggleStrip(context) { ItemNameSelector = EnumSelectionOptions.GetDisplayName };
            if (attrs != null)
            {
                using TypedArray values = context.ObtainStyledAttributes(attrs, Resource.Styleable.AJutEnumToggleStrip);
                m_strip.AllowMultiSelect = values.GetBoolean(Resource.Styleable.AJutEnumToggleStrip_ajutAllowMultiSelect, false);
                m_strip.AllowNoSelection = values.GetBoolean(Resource.Styleable.AJutEnumToggleStrip_ajutAllowNoSelection, false);
                m_strip.EnsureUniformSize = values.GetBoolean(Resource.Styleable.AJutEnumToggleStrip_ajutEnsureUniformSize, false);
                m_strip.OverflowBehavior = (eToggleStripOverflow)values.GetInt(Resource.Styleable.AJutEnumToggleStrip_ajutOverflowBehavior, (int)eToggleStripOverflow.Clip);
                m_strip.Orientation = (Android.Widget.Orientation)values.GetInt(Resource.Styleable.AJutEnumToggleStrip_ajutOrientation, (int)Android.Widget.Orientation.Horizontal);
                this.Enabled = values.GetBoolean(Resource.Styleable.AJutEnumToggleStrip_android_enabled, true);
                m_strip.CornerRadius = values.GetDimension(Resource.Styleable.AJutEnumToggleStrip_ajutCornerRadius, m_strip.CornerRadius);
                m_strip.SeparatorThickness = values.GetDimensionPixelSize(Resource.Styleable.AJutEnumToggleStrip_ajutSeparatorThickness, m_strip.SeparatorThickness);
                m_strip.ItemPadding = ToggleStrip.ReadItemPadding(
                    values,
                    Resource.Styleable.AJutEnumToggleStrip_ajutItemPadding,
                    Resource.Styleable.AJutEnumToggleStrip_ajutItemPaddingHorizontal,
                    Resource.Styleable.AJutEnumToggleStrip_ajutItemPaddingVertical,
                    m_strip.ItemPadding
                );
                values.Recycle();
            }

            this.AddView(m_strip, new LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent));
        }

        private void RebuildItems ()
        {
            if (m_valueOfTheEnumType == null)
            {
                return;
            }

            // The strip keeps a value of its enum type rather than the Type, so this stays clean under trimming without a
            //  suppression. GetSelectableValues reads the enum's public static fields (one per value, with their
            //  attributes), so its parameter asks for PublicFields to be kept. The trim analyzer treats GetType() on a
            //  value typed Enum as meeting that, which holds because the trimmer keeps every field of an enum type it
            //  keeps (MarkStep.MarkImplicitlyUsedFields), and a live value's type is kept. The trimmer source:
            //  https://github.com/dotnet/runtime/blob/main/src/tools/illink/src/linker/Linker.Steps/MarkStep.cs
            m_isSyncing = true;
            try
            {
                m_strip.ItemsSource = EnumSelectionOptions.GetSelectableValues(m_valueOfTheEnumType.GetType()).ToArray();
            }
            finally
            {
                m_isSyncing = false;
            }

            if (m_selectedItem is Enum current)
            {
                this.PushSelectionFromValue(current);
            }
        }

        private void PushSelectionFromValue (Enum value)
        {
            m_isSyncing = true;
            try
            {
                // AJut.UX's shared rules: a multi-select [Flags] strip sets each flag (selecting before deselecting, so
                // the last-item veto never trips), otherwise this value's item is turned on.
                EnumSelectionOptions.ApplyValueToItems(m_strip.Items, value, m_strip.AllowMultiSelect);
                m_selectedItems = m_strip.Items.Where(item => item.IsSelected).Select(item => item.Data).ToList();
            }
            finally
            {
                m_isSyncing = false;
            }
        }

        private void OnStripSelectionChanged (object? sender, ToggleStripSelectionChange e)
        {
            if (m_isSyncing || (m_valueOfTheEnumType == null))
            {
                return;
            }

            m_isSyncing = true;
            try
            {
                object? value = EnumSelectionOptions.ValueFromSelection(m_valueOfTheEnumType.GetType(), m_strip.AllowMultiSelect, e.CurrentSelection, e.SelectedItem);

                // The items follow the value they made, so picking a [Flags] None turns the other flags off
                if (value is Enum enumValue)
                {
                    EnumSelectionOptions.ApplyValueToItems(m_strip.Items, enumValue, m_strip.AllowMultiSelect);
                }

                m_selectedItems = m_strip.Items.Where(item => item.IsSelected).Select(item => item.Data).ToList();
                this.SelectedItem = value;
            }
            finally
            {
                m_isSyncing = false;
            }
        }
    }
}
