namespace AJut.UX.Controls
{
    using System.ComponentModel;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Media;
    using AJut.UX.PropertyInteraction;
    using DPUtils = DPUtils<PropertyGridListEditor>;

    // ===========[ PropertyGridListEditor ]========================================
    // Inline editor for list/array/collection properties in the PropertyGrid.
    // Shows an element count text (e.g. "Elements (3)") and an add button (+).
    // DataContext is a PropertyEditTarget with EditContext = PropertyGridListContext.

    [TemplatePart(Name = "PART_CountText", Type = typeof(TextBlock))]
    [TemplatePart(Name = "PART_AddButton", Type = typeof(Button))]
    public class PropertyGridListEditor : Control
    {
        // ===========[ Instance fields ]==========================================
        private PropertyGridListContext m_listContext;

        // ===========[ Construction ]=============================================
        static PropertyGridListEditor ()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(PropertyGridListEditor), new FrameworkPropertyMetadata(typeof(PropertyGridListEditor)));
        }

        public PropertyGridListEditor ()
        {
            this.DataContextChanged += this.OnDataContextChanged;
        }

        // ===========[ Template parts ]==========================================
        private TextBlock PART_CountText { get; set; }
        private Button PART_AddButton { get; set; }

        // ===========[ Dependency Properties ]====================================
        public static readonly DependencyProperty ListElementCountFormatProperty = DPUtils.Register(_ => _.ListElementCountFormat, "Elements ({0})", (d, e) => d.UpdateCountText());
        public string ListElementCountFormat
        {
            get => (string)this.GetValue(ListElementCountFormatProperty);
            set => this.SetValue(ListElementCountFormatProperty, value);
        }

        // ===========[ Template application ]====================================
        public override void OnApplyTemplate ()
        {
            base.OnApplyTemplate();

            if (this.PART_AddButton != null)
            {
                this.PART_AddButton.Click -= this.OnAddButtonClick;
            }

            this.PART_CountText = this.GetTemplateChild("PART_CountText") as TextBlock;
            this.PART_AddButton = this.GetTemplateChild("PART_AddButton") as Button;

            if (this.PART_AddButton != null)
            {
                this.PART_AddButton.Click += this.OnAddButtonClick;
            }

            this.UpdateFromContext();
        }

        // ===========[ Event handlers ]==========================================
        private void OnDataContextChanged (object sender, DependencyPropertyChangedEventArgs e)
        {
            if (m_listContext != null)
            {
                m_listContext.PropertyChanged -= this.OnListContextPropertyChanged;
            }

            m_listContext = null;

            if (e.NewValue is PropertyEditTarget target)
            {
                m_listContext = target.EditContext as PropertyGridListContext;
            }

            if (m_listContext != null)
            {
                m_listContext.PropertyChanged += this.OnListContextPropertyChanged;
            }

            // Walk up to find PropertyGrid and read ListElementCountFormat
            DependencyObject current = VisualTreeHelper.GetParent(this);
            while (current != null)
            {
                if (current is PropertyGrid pg)
                {
                    this.ListElementCountFormat = pg.ListElementCountFormat;
                    break;
                }

                current = VisualTreeHelper.GetParent(current);
            }

            this.UpdateFromContext();
        }

        private void OnListContextPropertyChanged (object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PropertyGridListContext.ElementCount))
            {
                this.UpdateCountText();
            }
        }

        private void OnAddButtonClick (object sender, RoutedEventArgs e)
        {
            m_listContext?.AddElement();
        }

        // ===========[ Private helpers ]==========================================
        private void UpdateFromContext ()
        {
            this.UpdateCountText();

            if (this.PART_AddButton != null)
            {
                this.PART_AddButton.Visibility = (m_listContext?.CanAdd == true)
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
        }

        private void UpdateCountText ()
        {
            if (this.PART_CountText == null)
            {
                return;
            }

            int count = m_listContext?.ElementCount ?? 0;
            this.PART_CountText.Text = string.Format(this.ListElementCountFormat, count);
        }
    }
}
