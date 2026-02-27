namespace AJut.UX.Controls
{
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;
    using Microsoft.UI.Xaml.Input;
    using Microsoft.UI.Xaml.Media;
    using System.ComponentModel;
    using AJut.UX;
    using DPUtils = AJut.UX.DPUtils<PropertyGridItemRow>;

    // ===========[ PropertyGridItemRow ]=======================================
    // Per-property row presenter used internally by PropertyGrid.
    //
    // DataContext is FlatTreeItem (set by FlatTreeListControl's ListView).
    // The row template binds to FlatTreeItem properties directly, and the
    // editor ContentControl uses Source.EffectiveEditorTarget so it always
    // receives a PropertyEditTarget regardless of elevation.
    //
    // EditorTemplateSelector is resolved on Loaded by walking up the visual
    // tree to find the enclosing PropertyGrid.
    //
    // LabelColumnWidth is read from the enclosing PropertyGrid on Loaded.
    // ApplyLabelColumnWidth() computes the PART_LabelBorder.Width by
    // subtracting FlatTreeItem.IndentWidth and kExpanderColumnWidth.

    [TemplatePart(Name = nameof(PART_LabelBorder), Type = typeof(Border))]
    public class PropertyGridItemRow : Control
    {
        // ===========[ Statics ]==========================================
        private const double kExpanderColumnWidth = 18.0;

        // ===========[ Instance fields ]==========================================
        private FlatTreeItem m_flatTreeItem;

        // ===========[ Construction ]=============================================
        public PropertyGridItemRow ()
        {
            this.DefaultStyleKey = typeof(PropertyGridItemRow);
            this.Loaded += this.OnLoaded;
            this.DataContextChanged += this.OnDataContextChanged;
        }

        // ===========[ Template parts ]==========================================
        private Border PART_LabelBorder { get; set; }

        // ===========[ Dependency Properties ]====================================
        public static readonly DependencyProperty EditorTemplateSelectorProperty = DPUtils.Register(_ => _.EditorTemplateSelector);
        public DataTemplateSelector EditorTemplateSelector
        {
            get => (DataTemplateSelector)this.GetValue(EditorTemplateSelectorProperty);
            set => this.SetValue(EditorTemplateSelectorProperty, value);
        }

        public static readonly DependencyProperty LabelColumnWidthProperty = DPUtils.Register(_ => _.LabelColumnWidth, double.NaN, (d, e) => d.ApplyLabelColumnWidth());
        public double LabelColumnWidth
        {
            get => (double)this.GetValue(LabelColumnWidthProperty);
            set => this.SetValue(LabelColumnWidthProperty, value);
        }

        // ===========[ Pointer overrides — drive HoverStates VSM group ]=========
        protected override void OnPointerEntered (PointerRoutedEventArgs e)
        {
            base.OnPointerEntered(e);
            VisualStateManager.GoToState(this, "Hovered", true);
        }

        protected override void OnPointerExited (PointerRoutedEventArgs e)
        {
            base.OnPointerExited(e);
            VisualStateManager.GoToState(this, "NotHovered", true);
        }

        // ===========[ Template application ]====================================
        protected override void OnApplyTemplate ()
        {
            base.OnApplyTemplate();
            this.PART_LabelBorder = this.GetTemplateChild(nameof(PART_LabelBorder)) as Border;
            this.ApplyLabelColumnWidth();
            if (m_flatTreeItem != null)
            {
                VisualStateManager.GoToState(this, m_flatTreeItem.IsSelected ? "Selected" : "Normal", false);
            }
        }

        // ===========[ Events ]===================================================
        private void OnLoaded (object sender, RoutedEventArgs e)
        {
            // Walk up the visual tree to find the enclosing PropertyGrid.
            DependencyObject current = VisualTreeHelper.GetParent(this);
            while (current != null)
            {
                if (current is PropertyGrid pg)
                {
                    if (this.EditorTemplateSelector == null)
                    {
                        this.EditorTemplateSelector = pg.ItemTemplateSelector;
                    }

                    if (double.IsNaN(this.LabelColumnWidth))
                    {
                        this.LabelColumnWidth = pg.LabelColumnWidth;
                    }

                    // Apply ElementPadding from PropertyGrid as this row's Padding so the
                    // template's {TemplateBinding Padding} insets the content from the edges.
                    this.Padding = pg.ElementPadding;

                    return;
                }

                current = VisualTreeHelper.GetParent(current);
            }
        }

        private void OnDataContextChanged (FrameworkElement sender, DataContextChangedEventArgs e)
        {
            if (m_flatTreeItem != null)
            {
                m_flatTreeItem.IsSelectedChanged -= this.OnIsSelectedChanged;
                m_flatTreeItem.PropertyChanged -= this.OnFlatTreeItemPropertyChanged;
            }

            m_flatTreeItem = e.NewValue as FlatTreeItem;
            VisualStateManager.GoToState(this, "NotHovered", false);
            if (m_flatTreeItem != null)
            {
                m_flatTreeItem.IsSelectedChanged += this.OnIsSelectedChanged;
                m_flatTreeItem.PropertyChanged += this.OnFlatTreeItemPropertyChanged;
                VisualStateManager.GoToState(this, m_flatTreeItem.IsSelected ? "Selected" : "Normal", false);
                this.ApplyLabelColumnWidth();
            }
            else
            {
                VisualStateManager.GoToState(this, "Normal", false);
            }
        }

        private void OnIsSelectedChanged (object sender, System.EventArgs e)
        {
            VisualStateManager.GoToState(this, m_flatTreeItem?.IsSelected == true ? "Selected" : "Normal", false);
        }

        private void OnFlatTreeItemPropertyChanged (object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(FlatTreeItem.IndentWidth))
            {
                this.ApplyLabelColumnWidth();
            }
        }

        // ===========[ Private helpers ]===========================================
        private void ApplyLabelColumnWidth ()
        {
            if (this.PART_LabelBorder == null)
            {
                return;
            }

            if (double.IsNaN(this.LabelColumnWidth) || m_flatTreeItem == null)
            {
                this.PART_LabelBorder.Width = double.NaN;
                return;
            }

            double available = this.LabelColumnWidth - m_flatTreeItem.IndentWidth - kExpanderColumnWidth;
            this.PART_LabelBorder.Width = available > 0 ? available : 0;
        }
    }
}
