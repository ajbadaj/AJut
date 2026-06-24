namespace AJut.UX.Controls
{
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Media;
    using DPUtils = AJut.UX.DPUtils<BrushButton>;

    /// <summary>
    /// A button whose per state brushes are plain dependency properties - set them, bind them, or point
    /// them at a {ThemeResource}, no per instance ResourceDictionary override required. Every slot defaults
    /// (in the default style) to the matching standard WinUI button brush, so an untouched BrushButton
    /// behaves exactly like a normal button - full hover / pressed / disabled feedback - and you override
    /// only the slots you care about. For "one color, everything derived" use AutoBrushButton instead.
    /// </summary>
    public class BrushButton : _ManagedBrushButton
    {
        public BrushButton ()
        {
            this.DefaultStyleKey = typeof(BrushButton);
        }

        // ===========[ Dependency Properties ]===================================
        public static readonly DependencyProperty NormalBackgroundProperty = DPUtils.Register(_ => _.NormalBackground, (d, e) => d.RefreshActiveStateBrushes());
        public Brush NormalBackground { get => (Brush)this.GetValue(NormalBackgroundProperty); set => this.SetValue(NormalBackgroundProperty, value); }

        public static readonly DependencyProperty NormalForegroundProperty = DPUtils.Register(_ => _.NormalForeground, (d, e) => d.RefreshActiveStateBrushes());
        public Brush NormalForeground { get => (Brush)this.GetValue(NormalForegroundProperty); set => this.SetValue(NormalForegroundProperty, value); }

        public static readonly DependencyProperty NormalBorderBrushProperty = DPUtils.Register(_ => _.NormalBorderBrush, (d, e) => d.RefreshActiveStateBrushes());
        public Brush NormalBorderBrush { get => (Brush)this.GetValue(NormalBorderBrushProperty); set => this.SetValue(NormalBorderBrushProperty, value); }

        public static readonly DependencyProperty PointerOverBackgroundProperty = DPUtils.Register(_ => _.PointerOverBackground, (d, e) => d.RefreshActiveStateBrushes());
        public Brush PointerOverBackground { get => (Brush)this.GetValue(PointerOverBackgroundProperty); set => this.SetValue(PointerOverBackgroundProperty, value); }

        public static readonly DependencyProperty PointerOverForegroundProperty = DPUtils.Register(_ => _.PointerOverForeground, (d, e) => d.RefreshActiveStateBrushes());
        public Brush PointerOverForeground { get => (Brush)this.GetValue(PointerOverForegroundProperty); set => this.SetValue(PointerOverForegroundProperty, value); }

        public static readonly DependencyProperty PointerOverBorderBrushProperty = DPUtils.Register(_ => _.PointerOverBorderBrush, (d, e) => d.RefreshActiveStateBrushes());
        public Brush PointerOverBorderBrush { get => (Brush)this.GetValue(PointerOverBorderBrushProperty); set => this.SetValue(PointerOverBorderBrushProperty, value); }

        public static readonly DependencyProperty PressedBackgroundProperty = DPUtils.Register(_ => _.PressedBackground, (d, e) => d.RefreshActiveStateBrushes());
        public Brush PressedBackground { get => (Brush)this.GetValue(PressedBackgroundProperty); set => this.SetValue(PressedBackgroundProperty, value); }

        public static readonly DependencyProperty PressedForegroundProperty = DPUtils.Register(_ => _.PressedForeground, (d, e) => d.RefreshActiveStateBrushes());
        public Brush PressedForeground { get => (Brush)this.GetValue(PressedForegroundProperty); set => this.SetValue(PressedForegroundProperty, value); }

        public static readonly DependencyProperty PressedBorderBrushProperty = DPUtils.Register(_ => _.PressedBorderBrush, (d, e) => d.RefreshActiveStateBrushes());
        public Brush PressedBorderBrush { get => (Brush)this.GetValue(PressedBorderBrushProperty); set => this.SetValue(PressedBorderBrushProperty, value); }

        public static readonly DependencyProperty DisabledBackgroundProperty = DPUtils.Register(_ => _.DisabledBackground, (d, e) => d.RefreshActiveStateBrushes());
        public Brush DisabledBackground { get => (Brush)this.GetValue(DisabledBackgroundProperty); set => this.SetValue(DisabledBackgroundProperty, value); }

        public static readonly DependencyProperty DisabledForegroundProperty = DPUtils.Register(_ => _.DisabledForeground, (d, e) => d.RefreshActiveStateBrushes());
        public Brush DisabledForeground { get => (Brush)this.GetValue(DisabledForegroundProperty); set => this.SetValue(DisabledForegroundProperty, value); }

        public static readonly DependencyProperty DisabledBorderBrushProperty = DPUtils.Register(_ => _.DisabledBorderBrush, (d, e) => d.RefreshActiveStateBrushes());
        public Brush DisabledBorderBrush { get => (Brush)this.GetValue(DisabledBorderBrushProperty); set => this.SetValue(DisabledBorderBrushProperty, value); }

        // ===========[ Public Interface Methods ]===================================
        protected override SurfaceBrushes GetBrushesForState (eManagedButtonState state)
        {
            return state switch
            {
                eManagedButtonState.PointerOver => new SurfaceBrushes(this.PointerOverBackground, this.PointerOverForeground, this.PointerOverBorderBrush),
                eManagedButtonState.Pressed => new SurfaceBrushes(this.PressedBackground, this.PressedForeground, this.PressedBorderBrush),
                eManagedButtonState.Disabled => new SurfaceBrushes(this.DisabledBackground, this.DisabledForeground, this.DisabledBorderBrush),
                _ => new SurfaceBrushes(this.NormalBackground, this.NormalForeground, this.NormalBorderBrush),
            };
        }
    }
}
