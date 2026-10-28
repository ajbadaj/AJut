namespace AJut.UX.Controls
{
    using System;
    using System.Collections.Generic;
    using AppKit;
    using CoreGraphics;

    // ===========[ EnumComboBox ]===============================================
    // A pop-up button listing an enum's values. Set SelectedItem to any value of the enum and the
    // list fills with that enum's values; picking one sets SelectedItem and raises
    // SelectedItemChanged.
    //
    // Which values are listed, and what each is called, come from AJut.UX's EnumSelectionOptions,
    // the same as every other stack: values marked [Browsable(false)] or [ExcludeFromSelection]
    // are left out.
    //
    // It handles its own action to keep SelectedItem in step, so listen to SelectedItemChanged
    // rather than setting a Target and Action on it, which would replace that.
    public class EnumComboBox : NSPopUpButton
    {
        private readonly List<object> m_values = new List<object>();
        private Type m_enumType;
        private object m_selectedItem;

        // ===========[ Setup/Construction/Teardown ]===========================
        public EnumComboBox ()
            : base(CGRect.Empty, false)
        {
            this.Activated += this.OnActivated;
        }

        protected override void Dispose (bool disposing)
        {
            if (disposing)
            {
                this.Activated -= this.OnActivated;
            }

            base.Dispose(disposing);
        }

        // ===========[ Events ]================================================
        /// <summary>Raised when <see cref="SelectedItem"/> changes, from a pick or from code</summary>
        public event EventHandler<EventArgs> SelectedItemChanged;

        private void OnActivated (object sender, EventArgs e)
        {
            int index = (int)this.IndexOfSelectedItem;
            if (index >= 0 && index < m_values.Count)
            {
                this.SetSelectedItem(m_values[index], updateMenu: false);
            }
        }

        // ===========[ Properties ]============================================
        /// <summary>
        /// The selected enum value. Setting a value of a different enum type relists the menu with that
        /// type's values. A value that is not an enum is ignored.
        /// </summary>
        /// <remarks>
        /// This hides NSPopUpButton's SelectedItem (the selected NSMenuItem), so the name matches EnumComboBox on
        /// every other stack. The menu item is still there through a cast to NSPopUpButton.
        /// </remarks>
        public new object SelectedItem
        {
            get => m_selectedItem;
            set
            {
                if (value is Enum)
                {
                    this.SetSelectedItem(value, updateMenu: true);
                }
            }
        }

        /// <summary>The values listed, in menu order</summary>
        public IReadOnlyList<object> Values => m_values;

        // ===========[ Helper Methods ]========================================
        private void SetSelectedItem (object value, bool updateMenu)
        {
            if (value.GetType() != m_enumType)
            {
                this.RebuildMenu((Enum)value);
            }

            if (updateMenu)
            {
                this.SelectItem(m_values.IndexOf(value));
            }

            if (Equals(m_selectedItem, value))
            {
                return;
            }

            m_selectedItem = value;
            this.SelectedItemChanged?.Invoke(this, EventArgs.Empty);
        }

        // The enum type is read from the value itself, at the call that reads its fields: the trim analyzer knows the
        // GetType() of a value typed Enum keeps that enum's public fields, and loses that once the type is passed around
        private void RebuildMenu (Enum valueOfEnum)
        {
            m_enumType = valueOfEnum.GetType();
            m_values.Clear();
            m_values.AddRange(EnumSelectionOptions.GetSelectableValues(valueOfEnum.GetType()));

            this.RemoveAllItems();
            var titles = new string[m_values.Count];
            for (int i = 0; i < titles.Length; ++i)
            {
                titles[i] = EnumSelectionOptions.GetDisplayName(m_values[i]);
            }

            this.AddItems(titles);
            this.InvalidateIntrinsicContentSize();
        }
    }
}
