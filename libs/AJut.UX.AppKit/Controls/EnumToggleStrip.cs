namespace AJut.UX.Controls
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using AppKit;

    // ===========[ EnumToggleStrip ]============================================
    // A ToggleStrip of an enum's values. Set SelectedItem to any value of the enum and the strip
    // fills with that enum's values (filtered and named by AJut.UX's EnumSelectionOptions, the
    // same as every other stack).
    //
    // Selection, by enum kind (EnumSelectionOptions' ApplyValueToItems and ValueFromSelection,
    // the same sync rules every stack's EnumToggleStrip uses):
    //  - Single select, or multi select on a plain enum: SelectedItem is the strip's selected
    //    value (with multi select, the one toggled on last), and SelectedItems holds every
    //    selected value.
    //  - Multi select on a [Flags] enum: SelectedItem is every selected flag combined into one
    //    value, and setting it selects each flag it contains. A zero-valued member (None) is never
    //    selected by that, since every value "contains" it.
    //
    // The strip settings that matter for an enum (overflow, uniform sizing, separators, padding)
    // pass straight through to the inner ToggleStrip, which is also public as Strip for anything
    // else.
    public class EnumToggleStrip : NSView
    {
        private readonly ToggleStrip m_strip;
        private Type m_enumType;
        private object m_selectedItem;
        private bool m_isSyncing;

        // ===========[ Setup/Construction/Teardown ]===========================
        public EnumToggleStrip ()
        {
            m_strip = new ToggleStrip { ItemNameSelector = EnumSelectionOptions.GetDisplayName };
            m_strip.TranslatesAutoresizingMaskIntoConstraints = false;
            this.AddSubview(m_strip);
            NSLayoutConstraint.ActivateConstraints(new[]
            {
                m_strip.TopAnchor.ConstraintEqualTo(this.TopAnchor),
                m_strip.BottomAnchor.ConstraintEqualTo(this.BottomAnchor),
                m_strip.LeadingAnchor.ConstraintEqualTo(this.LeadingAnchor),
                m_strip.TrailingAnchor.ConstraintEqualTo(this.TrailingAnchor),
            });

            m_strip.SelectionChanged += this.OnStripSelectionChanged;
        }

        protected override void Dispose (bool disposing)
        {
            if (disposing)
            {
                m_strip.SelectionChanged -= this.OnStripSelectionChanged;
            }

            base.Dispose(disposing);
        }

        // ===========[ Events ]================================================
        /// <summary>Raised when <see cref="SelectedItem"/> changes, from a click or from code</summary>
        public event EventHandler<EventArgs> SelectedItemChanged;

        private void OnStripSelectionChanged (object sender, ToggleStrip.ToggleStripSelectionChangedEventArgs e)
        {
            if (m_isSyncing || m_enumType == null)
            {
                return;
            }

            object value;
            m_isSyncing = true;
            try
            {
                value = EnumSelectionOptions.ValueFromSelection(m_enumType, m_strip.AllowMultiSelect, e.CurrentSelection, m_strip.SelectedItem);

                // The items follow the value they made, so picking a [Flags] None turns the other flags off
                if (value is Enum enumValue)
                {
                    EnumSelectionOptions.ApplyValueToItems(m_strip.Items, enumValue, m_strip.AllowMultiSelect);
                }
            }
            finally
            {
                m_isSyncing = false;
            }

            this.StoreSelectedItem(value);
        }

        // ===========[ Properties ]============================================
        public override bool IsFlipped => true;

        /// <summary>The inner strip, for settings not passed through here</summary>
        public ToggleStrip Strip => m_strip;

        /// <summary>
        /// The selected value; on a [Flags] enum with multi select, every selected flag combined. Setting a
        /// value of a different enum type relists the strip. A value that is not an enum is ignored.
        /// </summary>
        public object SelectedItem
        {
            get => m_selectedItem;
            set
            {
                if (value is Enum valueOfEnum)
                {
                    this.ApplySelectedItem(valueOfEnum);
                }
            }
        }

        /// <summary>Every selected value, in strip order</summary>
        public IList SelectedItems
        {
            get => m_strip.SelectedItems;
            set => m_strip.SelectedItems = value;
        }

        public bool AllowMultiSelect
        {
            get => m_strip.AllowMultiSelect;
            set => m_strip.AllowMultiSelect = value;
        }

        public bool AllowNoSelection
        {
            get => m_strip.AllowNoSelection;
            set => m_strip.AllowNoSelection = value;
        }

        public eToggleStripOverflow OverflowBehavior
        {
            get => m_strip.OverflowBehavior;
            set => m_strip.OverflowBehavior = value;
        }

        /// <summary>Lays the values out left to right (the default) or top to bottom</summary>
        public NSUserInterfaceLayoutOrientation Orientation
        {
            get => m_strip.Orientation;
            set => m_strip.Orientation = value;
        }

        public bool EnsureUniformSize
        {
            get => m_strip.EnsureUniformSize;
            set => m_strip.EnsureUniformSize = value;
        }

        public double SeparatorThickness
        {
            get => m_strip.SeparatorThickness;
            set => m_strip.SeparatorThickness = value;
        }

        public NSEdgeInsets ItemPadding
        {
            get => m_strip.ItemPadding;
            set => m_strip.ItemPadding = value;
        }

        public override CoreGraphics.CGSize IntrinsicContentSize => m_strip.IntrinsicContentSize;

        // ===========[ Helper Methods ]========================================
        // The enum type is read from the value itself, at the call that reads its fields: the trim analyzer knows the
        // GetType() of a value typed Enum keeps that enum's public fields (the Object_GetType case in ILLink's
        // HandleCallAction, https://github.com/dotnet/runtime/tree/main/src/tools/illink), and loses that once the type is
        // passed around. The value has to arrive typed Enum: an object cast to Enum in a local still warned.
        private void ApplySelectedItem (Enum value)
        {
            m_isSyncing = true;
            try
            {
                if (value.GetType() != m_enumType)
                {
                    m_enumType = value.GetType();
                    m_strip.ItemsSource = new List<Enum>(EnumSelectionOptions.GetSelectableValues(value.GetType()));
                }

                EnumSelectionOptions.ApplyValueToItems(m_strip.Items, value, m_strip.AllowMultiSelect);
            }
            finally
            {
                m_isSyncing = false;
            }

            this.StoreSelectedItem(value);
        }

        private void StoreSelectedItem (object value)
        {
            if (Equals(m_selectedItem, value))
            {
                return;
            }

            m_selectedItem = value;
            this.SelectedItemChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
