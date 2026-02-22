namespace AJut.UX.Controls
{
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;
    using DPUtils = AJut.UX.DPUtils<FlatTreeItemRow>;

    // ===========[ FlatTreeItemRow ]============================================
    // Per-row presenter used internally by FlatTreeListControl. The ListView sets
    // each row's DataContext to the corresponding FlatTreeItem, so all inner
    // template bindings (IndentWidth, IsExpanded, HasChildren) use {Binding ...}.
    //
    // ContentTemplate is set from outside (by FlatTreeListControl) to apply the
    // user-provided row content template. The ControlTemplate forwards it via
    // TemplateBinding to the inner ContentPresenter.

    public class FlatTreeItemRow : Control
    {
        // ===========[ Construction ]=============================================
        public FlatTreeItemRow ()
        {
            this.DefaultStyleKey = typeof(FlatTreeItemRow);
        }

        // ===========[ Dependency Properties ]====================================
        // ContentTemplate is applied to the ContentPresenter that renders the
        // user-supplied row content. DataContext (= FlatTreeItem) is the Content.
        public static readonly DependencyProperty ContentTemplateProperty = DPUtils.Register(
            _ => _.ContentTemplate
        );
        public DataTemplate ContentTemplate
        {
            get => (DataTemplate)this.GetValue(ContentTemplateProperty);
            set => this.SetValue(ContentTemplateProperty, value);
        }
    }
}
