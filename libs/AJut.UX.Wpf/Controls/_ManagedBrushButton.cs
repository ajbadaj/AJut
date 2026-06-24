namespace AJut.UX.Controls
{
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Media;
    using DPUtils = AJut.UX.DPUtils<_ManagedBrushButton>;

    /// <summary>
    /// Shared plumbing for the brush driven buttons. The standard button drives its background, foreground,
    /// and border through theme-keyed brushes baked into its template, which is why recoloring one button
    /// forces a per instance ResourceDictionary override (and rules out binding). This base sidesteps it: it
    /// stays a plain Button - so ButtonBase still owns the mouse-over / pressed / disabled tracking - but it
    /// computes the brushes for the current state in code and publishes them on the read-only Effective*
    /// dependency properties. The template binds the visual Border / content to those, so the actual visual
    /// assignment stays in XAML; the code only does brush calculation and setting. Subclasses just answer
    /// "which brushes for this state".
    ///
    /// Not meant to be used directly - use BrushButton or AutoBrushButton.
    /// </summary>
    public abstract class _ManagedBrushButton : Button
    {
        // ===========[ Setup/Construction/Teardown ]===================================
        protected _ManagedBrushButton ()
        {
        }

        // ===========[ Dependency Properties ]===================================
        public static readonly DependencyProperty CornerRadiusProperty = DPUtils.Register(_ => _.CornerRadius, new CornerRadius(3));
        public CornerRadius CornerRadius
        {
            get => (CornerRadius)this.GetValue(CornerRadiusProperty);
            set => this.SetValue(CornerRadiusProperty, value);
        }

        private static readonly DependencyPropertyKey EffectiveBackgroundPropertyKey = DPUtils.RegisterReadOnly(_ => _.EffectiveBackground);
        public static readonly DependencyProperty EffectiveBackgroundProperty = EffectiveBackgroundPropertyKey.DependencyProperty;
        public Brush EffectiveBackground
        {
            get => (Brush)this.GetValue(EffectiveBackgroundProperty);
            private set => this.SetValue(EffectiveBackgroundPropertyKey, value);
        }

        private static readonly DependencyPropertyKey EffectiveForegroundPropertyKey = DPUtils.RegisterReadOnly(_ => _.EffectiveForeground);
        public static readonly DependencyProperty EffectiveForegroundProperty = EffectiveForegroundPropertyKey.DependencyProperty;
        public Brush EffectiveForeground
        {
            get => (Brush)this.GetValue(EffectiveForegroundProperty);
            private set => this.SetValue(EffectiveForegroundPropertyKey, value);
        }

        private static readonly DependencyPropertyKey EffectiveBorderBrushPropertyKey = DPUtils.RegisterReadOnly(_ => _.EffectiveBorderBrush);
        public static readonly DependencyProperty EffectiveBorderBrushProperty = EffectiveBorderBrushPropertyKey.DependencyProperty;
        public Brush EffectiveBorderBrush
        {
            get => (Brush)this.GetValue(EffectiveBorderBrushProperty);
            private set => this.SetValue(EffectiveBorderBrushPropertyKey, value);
        }

        // ===========[ Public Interface Methods ]===================================
        public override void OnApplyTemplate ()
        {
            base.OnApplyTemplate();
            this.RefreshActiveStateBrushes();
        }

        /// <summary>Recomputes the effective brushes for the current interaction state. Call when a brush source changes.</summary>
        protected void RefreshActiveStateBrushes ()
        {
            SurfaceBrushes brushes = this.GetBrushesForState(this.GetActiveState());
            this.EffectiveBackground = brushes.Background;
            this.EffectiveForeground = brushes.Foreground;
            this.EffectiveBorderBrush = brushes.Border;
        }

        /// <summary>Subclasses answer which brushes to paint for a given interaction state. Any may be null (no paint).</summary>
        protected abstract SurfaceBrushes GetBrushesForState (eManagedButtonState state);

        // ===========[ Events ]===================================
        protected override void OnPropertyChanged (DependencyPropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);
            if ((e.Property == IsMouseOverProperty)
                || (e.Property == IsPressedProperty)
                || (e.Property == IsEnabledProperty))
            {
                this.RefreshActiveStateBrushes();
            }
        }

        // ===========[ Helper Methods ]===================================
        private eManagedButtonState GetActiveState ()
        {
            if (!this.IsEnabled) { return eManagedButtonState.Disabled; }
            if (this.IsPressed) { return eManagedButtonState.Pressed; }
            if (this.IsMouseOver) { return eManagedButtonState.PointerOver; }
            return eManagedButtonState.Normal;
        }

        // ===========[ Subclasses/structs ]===================================
        /// <summary>The resolved brushes to paint for one interaction state. Any may be null (no paint).</summary>
        protected readonly record struct SurfaceBrushes (Brush Background, Brush Foreground, Brush Border);
    }

    public enum eManagedButtonState
    {
        Normal,
        PointerOver,
        Pressed,
        Disabled,
    }
}
