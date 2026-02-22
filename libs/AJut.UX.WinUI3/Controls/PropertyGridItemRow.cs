namespace AJut.UX.Controls
{
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;
    using DPUtils = AJut.UX.DPUtils<PropertyGridItemRow>;

    // ===========[ PropertyGridItemRow ]=======================================
    // Per-property row presenter used internally by PropertyGrid. The ListView
    // sets each row's DataContext to the corresponding PropertyEditTarget.
    //
    // EditorTemplateSelector is pushed from PropertyGrid code (ContainerContentChanging)
    // and forwarded to the ContentControl in the ControlTemplate via TemplateBinding,
    // allowing it to select the type-appropriate editor DataTemplate.
    //
    // This is the WinUI3 workaround for the WPF pattern of binding ContentTemplateSelector
    // via RelativeSource AncestorType (which is unavailable in WinUI3 DataTemplates).

    public class PropertyGridItemRow : Control
    {
        public PropertyGridItemRow ()
        {
            this.DefaultStyleKey = typeof(PropertyGridItemRow);
        }

        public static readonly DependencyProperty EditorTemplateSelectorProperty = DPUtils.Register(
            _ => _.EditorTemplateSelector
        );
        public DataTemplateSelector EditorTemplateSelector
        {
            get => (DataTemplateSelector)this.GetValue(EditorTemplateSelectorProperty);
            set => this.SetValue(EditorTemplateSelectorProperty, value);
        }
    }
}
