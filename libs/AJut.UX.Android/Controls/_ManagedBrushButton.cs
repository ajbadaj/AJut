namespace AJut.UX.Controls
{
    using System;
    using AJut.UX.Helpers;
    using Android.Content;
    using Android.Content.Res;
    using Android.Graphics.Drawables;
    using Android.Runtime;
    using Android.Util;
    using Android.Widget;

    /// <summary>
    /// Shared plumbing for the color driven buttons. A stock Android Button paints its states from the theme's
    /// background drawable, so recoloring one button means building a state list drawable and a color state list
    /// by hand for every instance. This base stays a plain Button, so Android still owns the pressed, hovered and
    /// enabled state machine, but it listens for the state change and paints its own surface and text color for
    /// the state it lands in. Subclasses only have to answer "which colors for this state".
    ///
    /// Not meant to be used directly - use BrushButton or AutoBrushButton.
    /// </summary>
    public abstract class _ManagedBrushButton : Button
    {
        private const float kDefaultCornerRadiusDp = 4f;
        private const float kDefaultBorderThicknessDp = 1f;

        private readonly GradientDrawable m_surface = new GradientDrawable();
        private eManagedButtonState m_activeState = eManagedButtonState.Normal;
        private int m_borderThickness;
        private bool m_isInitialized;

        // ===========[ Setup/Construction/Teardown ]===================================
        protected _ManagedBrushButton (Context context) : base(context)
        {
            this.InitializeSurface(context, null);
        }

        protected _ManagedBrushButton (Context context, IAttributeSet? attrs) : base(context, attrs)
        {
            this.InitializeSurface(context, attrs);
        }

        protected _ManagedBrushButton (Context context, IAttributeSet? attrs, int defStyleAttr) : base(context, attrs, defStyleAttr)
        {
            this.InitializeSurface(context, attrs);
        }

        /// <summary>Used by the Android runtime to wrap a Java instance that already exists; nothing to set up.</summary>
        protected _ManagedBrushButton (IntPtr javaReference, JniHandleOwnership transfer) : base(javaReference, transfer)
        {
        }

        // ===========[ Properties ]===================================
        /// <summary>The surface's corner radius, in pixels. Defaults to 4dp; the ajutCornerRadius XML attribute sets it from a layout.</summary>
        public float CornerRadius
        {
            get => m_surface.CornerRadius;
            set => m_surface.SetCornerRadius(value);
        }

        /// <summary>The border's thickness, in pixels. Defaults to 1dp; the ajutBorderThickness XML attribute sets it from a layout.</summary>
        public int BorderThickness
        {
            get => m_borderThickness;
            set
            {
                m_borderThickness = value;
                this.RefreshActiveStateColors();
            }
        }

        /// <summary>The interaction state the button is currently painted for.</summary>
        public eManagedButtonState ActiveState => m_activeState;

        // ===========[ Public Interface Methods ]===================================
        /// <summary>Re-resolves and repaints the colors for the current state. Call when a color source changes.</summary>
        protected void RefreshActiveStateColors ()
        {
            if (!m_isInitialized)
            {
                return;
            }

            SurfaceStateColors colors = this.GetColorsForState(m_activeState);
            m_surface.SetColor(colors.Background.ToAndroidColor());
            m_surface.SetStroke(m_borderThickness, colors.Border.ToAndroidColor());
            this.SetTextColor(colors.Foreground.ToAndroidColor());
        }

        /// <summary>
        /// Finishes setup once the subclass has its own colors ready, and paints the opening state. Every subclass
        /// constructor calls this last; until then a state change has nothing to paint with.
        /// </summary>
        protected void CompleteInitialization ()
        {
            m_isInitialized = true;
            m_activeState = this.EvaluateState();
            this.RefreshActiveStateColors();
        }

        /// <summary>Subclasses answer which colors to paint for a given interaction state.</summary>
        protected abstract SurfaceStateColors GetColorsForState (eManagedButtonState state);

        protected override void DrawableStateChanged ()
        {
            base.DrawableStateChanged();

            // Android calls this from inside the Button constructor, before this class has set anything up.
            if (!m_isInitialized)
            {
                return;
            }

            eManagedButtonState state = this.EvaluateState();
            if (state != m_activeState)
            {
                m_activeState = state;
                this.RefreshActiveStateColors();
            }
        }

        // A night mode switch the activity handles itself (configChanges="uiMode") reaches the view here instead
        // of recreating it, and the literal defaults depend on night mode, so repaint.
        protected override void OnConfigurationChanged (Configuration? newConfig)
        {
            base.OnConfigurationChanged(newConfig);
            this.OnThemeMayHaveChanged();
            this.RefreshActiveStateColors();
        }

        /// <summary>Called when the context's theme may have changed, before the repaint, for subclasses that cache themed colors.</summary>
        protected virtual void OnThemeMayHaveChanged ()
        {
        }

        // ===========[ Helper Methods ]===================================
        private void InitializeSurface (Context context, IAttributeSet? attrs)
        {
            DisplayMetrics? metrics = context.Resources?.DisplayMetrics;
            float cornerRadius = TypedValue.ApplyDimension(ComplexUnitType.Dip, kDefaultCornerRadiusDp, metrics);
            float borderThickness = TypedValue.ApplyDimension(ComplexUnitType.Dip, kDefaultBorderThicknessDp, metrics);
            if (attrs != null)
            {
                using TypedArray values = context.ObtainStyledAttributes(attrs, Resource.Styleable.AJutManagedBrushButton);
                cornerRadius = values.GetDimension(Resource.Styleable.AJutManagedBrushButton_ajutCornerRadius, cornerRadius);
                borderThickness = values.GetDimension(Resource.Styleable.AJutManagedBrushButton_ajutBorderThickness, borderThickness);
                values.Recycle();
            }

            m_surface.SetCornerRadius(cornerRadius);
            m_borderThickness = (int)Math.Round(borderThickness);

            // The stock button raises itself on press through a state list animator. These buttons show press with
            // color, the way the other AJut stacks do, so the elevation change goes.
            this.StateListAnimator = null;

            // The stock background carries the button's padding, and swapping it out drops that padding (text runs
            // to the edges). What the view has now is the theme's padding plus anything the layout set, so keep it.
            int left = this.PaddingLeft;
            int top = this.PaddingTop;
            int right = this.PaddingRight;
            int bottom = this.PaddingBottom;
            this.Background = m_surface;
            this.SetPadding(left, top, right, bottom);
        }

        private eManagedButtonState EvaluateState ()
        {
            if (!this.Enabled)
            {
                return eManagedButtonState.Disabled;
            }

            if (this.Pressed)
            {
                return eManagedButtonState.Pressed;
            }

            return this.Hovered ? eManagedButtonState.PointerOver : eManagedButtonState.Normal;
        }
    }
}
