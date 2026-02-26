namespace AJut.UX.Controls
{
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;
    using Microsoft.UI.Xaml.Media;
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
        public static readonly DependencyProperty ContentTemplateProperty = DPUtils.Register(_ => _.ContentTemplate);
        public DataTemplate ContentTemplate
        {
            get => (DataTemplate)this.GetValue(ContentTemplateProperty);
            set => this.SetValue(ContentTemplateProperty, value);
        }

        // ExpanderGlyphForeground: foreground for the expand/collapse chevron glyph.
        // Set by FlatTreeListControl via ContainerContentChanging so callers can
        // customize the glyph color without loading the full AJut theme.
        public static readonly DependencyProperty ExpanderGlyphForegroundProperty = DPUtils.Register(_ => _.ExpanderGlyphForeground);
        public Brush ExpanderGlyphForeground
        {
            get => (Brush)this.GetValue(ExpanderGlyphForegroundProperty);
            set => this.SetValue(ExpanderGlyphForegroundProperty, value);
        }
    }
}
