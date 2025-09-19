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
            set => this.SetValue(SelectedItemProperty, value);
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
                this.PART_ComboBox.ItemsSource = Enum.GetValues(itemType).OfType<Enum>().Select(o => new EnumItemStorage(o)).ToArray();
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
        }
    }
}
