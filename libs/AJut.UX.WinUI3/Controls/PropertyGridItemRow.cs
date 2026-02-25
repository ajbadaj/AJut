namespace AJut.UX.Controls
{
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;
    using Microsoft.UI.Xaml.Media;
    using DPUtils = AJut.UX.DPUtils<PropertyGridItemRow>;

    // ===========[ PropertyGridItemRow ]=======================================
    // Per-property row presenter used internally by PropertyGrid.
    //
    // When used inside a FlatTreeListControl (the new default), the DataContext
    // is set by the RowTemplate DataTemplate to FlatTreeItem.Source
    // (= PropertyEditTarget). EditorTemplateSelector is resolved by walking up
    // the visual tree to the enclosing PropertyGrid on Loaded.
    //
    // When used directly in a plain ListView the EditorTemplateSelector can also
    // be pushed via ContainerContentChanging for backward compatibility.

    public class PropertyGridItemRow : Control
    {
        public PropertyGridItemRow ()
        {
            this.DefaultStyleKey = typeof(PropertyGridItemRow);
            this.Loaded += this.OnLoaded;
        }

        public static readonly DependencyProperty EditorTemplateSelectorProperty = DPUtils.Register(
            _ => _.EditorTemplateSelector
        );
        public DataTemplateSelector EditorTemplateSelector
        {
            get => (DataTemplateSelector)this.GetValue(EditorTemplateSelectorProperty);
            set => this.SetValue(EditorTemplateSelectorProperty, value);
        }

        private void OnLoaded (object sender, RoutedEventArgs e)
        {
            if (this.EditorTemplateSelector != null)
            {
                return;
            }

            // Walk up the visual tree to find the enclosing PropertyGrid and inherit its selector.
            DependencyObject current = VisualTreeHelper.GetParent(this);
            while (current != null)
            {
                if (current is PropertyGrid pg)
                {
                    this.EditorTemplateSelector = pg.ItemTemplateSelector;
                    return;
                }

                current = VisualTreeHelper.GetParent(current);
            }
        }
    }
}
