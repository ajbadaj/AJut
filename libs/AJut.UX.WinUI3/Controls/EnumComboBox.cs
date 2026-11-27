namespace AJut.UX.Controls
{
    using System;
    using System.Linq;
    using System.Diagnostics;
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;
    using Microsoft.UI.Xaml.Data;
    using DPUtils = AJut.UX.DPUtils<EnumComboBox>;
    using System.Collections.Generic;

    /// <summary>
    /// Adds a <see cref="ComboBox"/> child (so style isn't effected) and infers options from selected item (item must always be selected).
    /// </summary>
    [TemplatePart(Name = nameof(PART_ComboBox), Type = typeof(ComboBox))]
    public class EnumComboBox : Control
    {
        private Type m_determenedItemType = null;

        public EnumComboBox ()
        {
            this.DefaultStyleKey = typeof(EnumComboBox);
        }

        protected override void OnApplyTemplate ()
        {
            base.OnApplyTemplate();

            if (this.PART_ComboBox != null)
            {
                this.PART_ComboBox.SelectionChanged -= this.ComboBox_OnSelectionChanged;
            }

            this.PART_ComboBox = (ComboBox)this.GetTemplateChild(nameof(PART_ComboBox));
            if (this.SelectedItem is Enum)
            {
                this.OnSelectedItemWasChanged(this.SelectedItem);
            }

            this.PART_ComboBox.SelectionChanged -= this.ComboBox_OnSelectionChanged;
            this.PART_ComboBox.SelectionChanged += this.ComboBox_OnSelectionChanged;
        }

        private void ComboBox_OnSelectionChanged (object sender, SelectionChangedEventArgs e)
        {
            if (this.PART_ComboBox.SelectedItem is EnumItemStorage internalSelectedItem)
            {
                this.SelectedItem = internalSelectedItem.Value;
            }
        }

        public ComboBox PART_ComboBox { get; private set; }

        public static readonly DependencyProperty SelectedItemProperty = DPUtils.Register(_ => _.SelectedItem, (d,e)=>d.OnSelectedItemWasChanged(e.NewValue));
        public object SelectedItem
        {
            get => this.GetValue(SelectedItemProperty);
            set
            {
                // WinUI3 takes a new box of the same enum value as a change to a custom dependency property, whether the
                // property is typed object, as this one is, or typed as the enum, and raises it. That matters for a
                // two-way x:Bind straight to another dependency property (SelectedItem="{x:Bind Other.Value,
                // Mode=TwoWay}"): the generated code copies each change across through the other side's CLR setter and
                // compares nothing, and every copy is a new box. If neither side skips an equal value, the two trade it
                // back and forth until the stack overflows.
                //
                // The guard lives here in the CLR setter, not the change handler, because x:Bind sets SelectedItem
                // through it, and by the time the change handler runs, the change has already been raised. It ends the
                // round trip on this side whatever the other property does. Code that calls SetValue on the property
                // directly goes around the guard.
                if (!Equals(this.SelectedItem, value))
                {
                    this.SetValue(SelectedItemProperty, value);
                }
            }
        }

        /// <summary>
        /// The selected item property was changed outside of here - that could me determining a new item type (and items source for the combo box).
        /// </summary>
        /// <param name="newValue"></param>
        private void OnSelectedItemWasChanged (object newValue)
        {
            if (this.PART_ComboBox == null || newValue is not Enum enumValue)
            {
                return;
            }

            Type itemType = newValue.GetType();
            Debug.Assert(itemType.IsEnum);

            if (m_determenedItemType != itemType)
            {
                this.PART_ComboBox.ItemsSource = EnumItemStorage.BuildOptions(itemType);
                m_determenedItemType = itemType;
            }

            this.PART_ComboBox.SelectedItem = ((IEnumerable<EnumItemStorage>)this.PART_ComboBox.ItemsSource).FirstOrDefault(e => ((Enum)e.Value).Equals(enumValue));
        }

        public class EnumItemStorage
        {
            public EnumItemStorage (Enum value)
            {
                this.Value = value;
                this.Name = value.ToString();
            }
            public object Value { get; }
            public string Name { get; }

            // The options the combo box lists for an enum type: AJut.UX's EnumSelectionOptions, which leaves out values
            // marked [Browsable(false)] or [ExcludeFromSelection], the same as EnumToggleStrip. It lives here rather than
            // on EnumComboBox so the list can be checked without making a control.
            internal static EnumItemStorage[] BuildOptions (Type enumType)
            {
                return EnumSelectionOptions.GetSelectableValues(enumType).Select(o => new EnumItemStorage(o)).ToArray();
            }
        }
    }
}
