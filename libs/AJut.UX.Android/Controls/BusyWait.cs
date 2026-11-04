namespace AJut.UX.Controls
{
    using System;
    using AJut.UX.Helpers;
    using AJut.UX.Theming;
    using Android.Content;
    using Android.Content.Res;
    using Android.Graphics.Drawables;
    using Android.Runtime;
    using Android.Util;
    using Android.Views;
    using Android.Widget;

    /// <summary>
    /// A busy indicator: the platform's own spinner beside a line of text, the way WPF's BusyWait puts its spinning glyph
    /// beside its content. <see cref="IsSpinning"/> turns it on and off, and <see cref="SpinnerDock"/> puts the spinner on
    /// any side of the text.
    /// </summary>
    /// <remarks>
    /// <para>
    /// As an overlay (<see cref="IsOverlay"/>) it is laid over what it covers, filling the same space. While spinning it
    /// fills that space with the blockout fill and takes every touch, so nothing under it can be used, with the spinner and
    /// text in its middle; stopped, it draws nothing and lets touches through. So an overlay can sit over a page for good
    /// and be switched on while the page is busy, the way WPF's stack navigation shows its busy overlay.
    /// </para>
    /// <para>
    /// Stopped inline, the spinner is hidden but keeps its room, so the text does not move when it starts again. In a
    /// layout, android:text, ajutIsSpinning, ajutIsOverlay, ajutSpinnerSize and ajutSpinnerDock set the five.
    /// </para>
    /// </remarks>
    [Register("ajut.ux.controls.BusyWait")]
    public class BusyWait : FrameLayout
    {
        // The look is MobileLook.BusyWait's, which UIKit's BusyWait draws too. The literals are the unthemed look: plain
        // text colors inline, and as an overlay the fill and text WPF's stack navigation overlay uses unthemed.
        private static readonly ThemedColor kForeground = ThemedColor.FromArgb(MobileLook.BusyWait.Foreground, 0xDE000000, 0xFFFFFFFF);
        private static readonly ThemedColor kOverlayBackground = ThemedColor.FromArgb(MobileLook.BusyWait.OverlayBackground, 0xC0000000, 0xC0000000);
        private static readonly ThemedColor kOverlayForeground = ThemedColor.FromArgb(MobileLook.BusyWait.OverlayForeground, 0xFFDDDDDD, 0xFFDDDDDD);

        private LinearLayout m_row = null!;
        private ProgressBar m_spinner = null!;
        private TextView m_text = null!;
        private ColorDrawable? m_overlayFill;
        private Drawable? m_backgroundBeforeOverlay;
        private ViewOutlineProvider? m_outlineBeforeOverlay;
        private float m_translationZBeforeOverlay;
        private bool m_isShowingOverlay;
        private bool m_hasTintedSpinner;
        private float m_density = 1f;
        private bool m_isSpinning = true;
        private bool m_isOverlay;
        private float m_spinnerSize = MobileLook.BusyWait.SpinnerSize;
        private eBusyWaitSpinnerDock m_spinnerDock = eBusyWaitSpinnerDock.Right;

        // ===========[ Setup/Construction/Teardown ]===================================
        public BusyWait (Context context) : base(context)
        {
            this.Initialize(context, null);
        }

        public BusyWait (Context context, IAttributeSet? attrs) : base(context, attrs)
        {
            this.Initialize(context, attrs);
        }

        public BusyWait (Context context, IAttributeSet? attrs, int defStyleAttr) : base(context, attrs, defStyleAttr)
        {
            this.Initialize(context, attrs);
        }

        protected BusyWait (IntPtr javaReference, JniHandleOwnership transfer) : base(javaReference, transfer)
        {
        }

        // ===========[ Properties ]===================================
        /// <summary>Whether the spinner is spinning. On by default, as on WPF.</summary>
        public bool IsSpinning
        {
            get => m_isSpinning;
            set
            {
                if (m_isSpinning != value)
                {
                    m_isSpinning = value;
                    this.ApplySpinning();
                }
            }
        }

        /// <summary>
        /// Whether this covers what is under it while spinning: the blockout fill, every touch taken, and the spinner and
        /// text in the middle. Stopped, an overlay draws nothing and lets touches through. Off by default.
        /// </summary>
        public bool IsOverlay
        {
            get => m_isOverlay;
            set
            {
                if (m_isOverlay != value)
                {
                    m_isOverlay = value;
                    this.ArrangeRow();
                    this.ApplyLook();
                    this.ApplySpinning();
                }
            }
        }

        /// <summary>Which side of the text the spinner sits on. Right by default, as on WPF.</summary>
        public eBusyWaitSpinnerDock SpinnerDock
        {
            get => m_spinnerDock;
            set
            {
                if (m_spinnerDock != value)
                {
                    m_spinnerDock = value;
                    this.ArrangeRow();
                }
            }
        }

        /// <summary>The spinner's width and height, in dp</summary>
        public float SpinnerSize
        {
            get => m_spinnerSize;
            set
            {
                if (m_spinnerSize != value)
                {
                    m_spinnerSize = value;
                    this.ArrangeRow();
                }
            }
        }

        /// <summary>The text beside the spinner. Null or empty shows the spinner alone.</summary>
        public string? Text
        {
            get => m_text.Text;
            set
            {
                m_text.Text = value;
                this.ArrangeRow();
            }
        }

        /// <summary>The text view, for styling the text the way the app styles its other text</summary>
        public TextView TextLabel => m_text;

        // ===========[ Helper Methods ]===================================
        private void Initialize (Context context, IAttributeSet? attrs)
        {
            m_density = context.Resources?.DisplayMetrics?.Density ?? 1f;
            m_spinner = new ProgressBar(context) { Indeterminate = true };
            m_text = new TextView(context) { Gravity = GravityFlags.Center };
            m_row = new LinearLayout(context) { Orientation = Android.Widget.Orientation.Horizontal };
            m_row.SetGravity(GravityFlags.Center);
            this.AddView(m_row, new FrameLayout.LayoutParams(ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent, GravityFlags.Center));

            if (attrs != null)
            {
                using TypedArray values = context.ObtainStyledAttributes(attrs, Resource.Styleable.AJutBusyWait);
                m_text.Text = values.GetString(Resource.Styleable.AJutBusyWait_android_text);
                m_isSpinning = values.GetBoolean(Resource.Styleable.AJutBusyWait_ajutIsSpinning, true);
                m_isOverlay = values.GetBoolean(Resource.Styleable.AJutBusyWait_ajutIsOverlay, false);
                m_spinnerSize = values.GetDimension(Resource.Styleable.AJutBusyWait_ajutSpinnerSize, m_spinnerSize * m_density) / m_density;
                m_spinnerDock = (eBusyWaitSpinnerDock)values.GetInt(Resource.Styleable.AJutBusyWait_ajutSpinnerDock, (int)eBusyWaitSpinnerDock.Right);
                values.Recycle();
            }

            this.ArrangeRow();
            this.ApplyLook();
            this.ApplySpinning();
        }

        // Left and Right lay the spinner and text out in a row, Top and Bottom in a column; the spacing goes between the
        // two only when there is text to space from
        private void ArrangeRow ()
        {
            bool isRow = (m_spinnerDock == eBusyWaitSpinnerDock.Left) || (m_spinnerDock == eBusyWaitSpinnerDock.Right);
            bool isSpinnerFirst = (m_spinnerDock == eBusyWaitSpinnerDock.Left) || (m_spinnerDock == eBusyWaitSpinnerDock.Top);
            bool hasText = !string.IsNullOrEmpty(m_text.Text);
            int spinnerSize = (int)(m_spinnerSize * m_density);
            int spacing = hasText ? (int)(MobileLook.BusyWait.SpinnerSpacing * m_density) : 0;

            m_row.RemoveAllViews();
            m_row.Orientation = isRow ? Android.Widget.Orientation.Horizontal : Android.Widget.Orientation.Vertical;
            m_text.Visibility = hasText ? ViewStates.Visible : ViewStates.Gone;

            var spinnerLayout = new LinearLayout.LayoutParams(spinnerSize, spinnerSize) { Gravity = GravityFlags.Center };
            var textLayout = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent) { Gravity = GravityFlags.Center };
            LinearLayout.LayoutParams second = isSpinnerFirst ? textLayout : spinnerLayout;
            if (isRow)
            {
                second.MarginStart = spacing;
            }
            else
            {
                second.TopMargin = spacing;
            }

            if (isSpinnerFirst)
            {
                m_row.AddView(m_spinner, spinnerLayout);
                m_row.AddView(m_text, textLayout);
            }
            else
            {
                m_row.AddView(m_text, textLayout);
                m_row.AddView(m_spinner, spinnerLayout);
            }

            // An overlay keeps its spinner and text clear of its edges
            int padding = m_isOverlay ? (int)(MobileLook.BusyWait.OverlayPadding * m_density) : 0;
            var rowLayout = (FrameLayout.LayoutParams)m_row.LayoutParameters!;
            rowLayout.SetMargins(padding, padding, padding, padding);
            m_row.LayoutParameters = rowLayout;
        }

        // Unthemed and inline, the spinner keeps the platform's own color, as a stock spinner beside it would. Themed, it
        // takes the text color, as WPF's spinner takes its foreground, and as an overlay it always takes the overlay's.
        //
        // The platform's color is never put back by clearing the tint: a stock spinner's drawable carries its own tint
        // (the theme's colorControlActivated), and setting the spinner's tint, even to null, replaces it, which leaves the
        // drawable's own white. So the spinner is left alone until it needs a color, and once it has had one it goes back
        // to the theme's colorControlActivated rather than to no tint.
        private void ApplyLook ()
        {
            Context context = this.Context!;
            ColorArgb foreground = ThemeColors.Resolve(context, m_isOverlay ? kOverlayForeground : kForeground);
            m_text.SetTextColor(foreground.ToAndroidColor());
            if (m_isOverlay || ThemeColors.IsThemeLoaded)
            {
                m_spinner.IndeterminateTintList = ColorStateList.ValueOf(foreground.ToAndroidColor());
                m_hasTintedSpinner = true;
            }
            else if (m_hasTintedSpinner)
            {
                m_spinner.IndeterminateTintList = ResolveThemeColor(context, Android.Resource.Attribute.ColorControlActivated);
            }

            m_overlayFill = m_isOverlay ? new ColorDrawable(ThemeColors.Resolve(context, kOverlayBackground).ToAndroidColor()) : null;
        }

        // A theme attribute's color, as a color state list or a plain color; null when the theme has none
        private static ColorStateList? ResolveThemeColor (Context context, int attribute)
        {
            var value = new TypedValue();
            if ((context.Theme == null) || !context.Theme.ResolveAttribute(attribute, value, true))
            {
                return null;
            }

            return (value.ResourceId != 0)
                ? context.Resources?.GetColorStateList(value.ResourceId, context.Theme)
                : ColorStateList.ValueOf(new Android.Graphics.Color(value.Data));
        }

        // A spinning overlay fills itself and takes touches (a clickable view keeps every touch that lands on it from what
        // is under it). It is raised above its siblings while it shows, since Android draws and hands touches to a raised
        // sibling (a Material button lifts itself) above one that is not, whatever their order; it casts no shadow. What
        // the overlay replaced, its background and how high it sat, is put back when it stops.
        private void ApplySpinning ()
        {
            // Above any button raised among its siblings, a pressed Material button included
            const float kOverlayRaise = 32f;

            bool showsOverlay = m_isOverlay && m_isSpinning;
            if (showsOverlay && !m_isShowingOverlay)
            {
                m_backgroundBeforeOverlay = this.Background;
                m_translationZBeforeOverlay = this.TranslationZ;
                m_outlineBeforeOverlay = this.OutlineProvider;
                this.OutlineProvider = null;
                this.TranslationZ = kOverlayRaise * m_density;
                this.Clickable = true;
            }
            else if (!showsOverlay && m_isShowingOverlay)
            {
                this.Background = m_backgroundBeforeOverlay;
                this.TranslationZ = m_translationZBeforeOverlay;
                this.OutlineProvider = m_outlineBeforeOverlay;
                this.Clickable = false;
                m_backgroundBeforeOverlay = null;
                m_outlineBeforeOverlay = null;
            }

            // The fill is set every time, since ApplyLook makes a new one for a new look
            if (showsOverlay)
            {
                this.Background = m_overlayFill;
            }

            m_isShowingOverlay = showsOverlay;
            m_row.Visibility = (m_isOverlay && !m_isSpinning) ? ViewStates.Invisible : ViewStates.Visible;
            m_spinner.Visibility = (m_isOverlay || m_isSpinning) ? ViewStates.Visible : ViewStates.Invisible;
        }
    }
}
