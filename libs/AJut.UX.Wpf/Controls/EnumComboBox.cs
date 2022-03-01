namespace AJut.UX.Controls
{
    using System;
    using System.Diagnostics;
    using System.Windows;
    using System.Windows.Controls;

    using DPUtils = AJut.UX.DPUtils<EnumComboBox>;

    /// <summary>
    /// Adds a <see cref="ComboBox"/> child (so style isn't effected) and infers options from selected item (item must always be selected).
    /// </summary>
    [TemplatePart(Name = nameof(PART_ComboBox), Type = typeof(ComboBox))]
    public class EnumComboBox : Control
    {
        bool m_ignoreSelectionChange;
        static EnumComboBox ()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(EnumComboBox), new FrameworkPropertyMetadata(typeof(EnumComboBox)));
        }

        public override void OnApplyTemplate ()
        {
            if (this.PART_ComboBox != null)
            {
                this.PART_ComboBox.SelectionChanged -= this.OnUserSelectedNewItem;
            }

            this.PART_ComboBox = (ComboBox)this.GetTemplateChild(nameof(PART_ComboBox));
            this.PART_ComboBox.SelectionChanged += this.OnUserSelectedNewItem;
            this.OnNewSelectedItem(this.SelectedItem);
        }
        

        public ComboBox PART_ComboBox { get; private set; }

        public static readonly DependencyProperty SelectedItemProperty = DPUtils.Register(_ => _.SelectedItem, (d,e)=>d.OnNewSelectedItem(e.NewValue));
        public object SelectedItem
        {
            get => this.GetValue(SelectedItemProperty);
            set => this.SetValue(SelectedItemProperty, value);
        }

        private void OnNewSelectedItem (object newValue)
        {
            if (PART_ComboBox == null || newValue == null)
            {
                return;
            }

            Type itemType = newValue.GetType();
            Debug.Assert(itemType.IsEnum);

            m_ignoreSelectionChange = true;
            try
            {
                this.PART_ComboBox.ItemsSource = Enum.GetValues(itemType);
                this.PART_ComboBox.SelectedItem = newValue;
            }
            finally { m_ignoreSelectionChange = false; }
        }

        private void OnUserSelectedNewItem (object sender, SelectionChangedEventArgs e)
        {
            if (!m_ignoreSelectionChange)
            {
                this.SetCurrentValue(SelectedItemProperty, this.PART_ComboBox.SelectedItem);
            }
        }
    }
}
