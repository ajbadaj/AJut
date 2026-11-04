namespace AJut.UX.Controls
{
    using System;
    using Android.Content;
    using Android.Content.Res;
    using Android.Runtime;
    using Android.Util;
    using Android.Views;

    /// <summary>
    /// A no-frills horizontal wrap panel, the Android twin of WinUI3's SimpleWrapPanel: children flow left to right at the
    /// size they want and the next one drops to a new line when it would run past the width, with
    /// <see cref="HorizontalSpacing"/> between children on a line and <see cref="VerticalSpacing"/> between lines. The
    /// layout is AJut.UX's <see cref="SimpleWrapLayout"/>, shared with every stack's wrap panel. A child that is gone takes
    /// no place, and showing it again reflows the panel, as Android relays out on any visibility change to or from gone.
    /// In a right to left layout the flow runs right to left, as a mirrored WinUI3 panel does. It paints nothing, so there
    /// is nothing to theme. In a layout, ajutHorizontalSpacing and ajutVerticalSpacing set the spacing.
    /// </summary>
    [Register("ajut.ux.controls.SimpleWrapPanel")]
    public class SimpleWrapPanel : ViewGroup
    {
        private const float kDefaultHorizontalSpacingDp = 8f;
        private const float kDefaultVerticalSpacingDp = 6f;

        private float m_horizontalSpacing;
        private float m_verticalSpacing;

        // ===========[ Setup/Construction/Teardown ]===================================
        public SimpleWrapPanel (Context context) : this(context, null)
        {
        }

        public SimpleWrapPanel (Context context, IAttributeSet? attrs) : this(context, attrs, 0)
        {
        }

        public SimpleWrapPanel (Context context, IAttributeSet? attrs, int defStyleAttr) : base(context, attrs, defStyleAttr)
        {
            float density = context.Resources?.DisplayMetrics?.Density ?? 1f;
            m_horizontalSpacing = kDefaultHorizontalSpacingDp * density;
            m_verticalSpacing = kDefaultVerticalSpacingDp * density;
            if (attrs != null)
            {
                using TypedArray values = context.ObtainStyledAttributes(attrs, Resource.Styleable.AJutSimpleWrapPanel);
                m_horizontalSpacing = Math.Max(0f, values.GetDimension(Resource.Styleable.AJutSimpleWrapPanel_ajutHorizontalSpacing, m_horizontalSpacing));
                m_verticalSpacing = Math.Max(0f, values.GetDimension(Resource.Styleable.AJutSimpleWrapPanel_ajutVerticalSpacing, m_verticalSpacing));
                values.Recycle();
            }
        }

        protected SimpleWrapPanel (IntPtr javaReference, JniHandleOwnership transfer) : base(javaReference, transfer)
        {
        }

        // ===========[ Properties ]===================================
        /// <summary>The gap between children on a line, in pixels, as a view's sizes are. 8dp unless set.</summary>
        public float HorizontalSpacing
        {
            get => m_horizontalSpacing;
            set
            {
                m_horizontalSpacing = Math.Max(0f, value);
                this.RequestLayout();
            }
        }

        /// <summary>The gap between lines, in pixels, as a view's sizes are. 6dp unless set.</summary>
        public float VerticalSpacing
        {
            get => m_verticalSpacing;
            set
            {
                m_verticalSpacing = Math.Max(0f, value);
                this.RequestLayout();
            }
        }

        // ===========[ Public Interface Methods ]===================================
        protected override void OnMeasure (int widthMeasureSpec, int heightMeasureSpec)
        {
            int horizontalPadding = this.PaddingLeft + this.PaddingRight;
            int verticalPadding = this.PaddingTop + this.PaddingBottom;
            double available = MeasureSpec.GetMode(widthMeasureSpec) == MeasureSpecMode.Unspecified
                ? double.PositiveInfinity
                : Math.Max(0, MeasureSpec.GetSize(widthMeasureSpec) - horizontalPadding);

            // Each child is measured as it asks (wrap_content, an exact size) within the space left after padding
            for (int index = 0; index < this.ChildCount; ++index)
            {
                View child = this.GetChildAt(index)!;
                if (child.Visibility != ViewStates.Gone)
                {
                    this.MeasureChild(child, widthMeasureSpec, heightMeasureSpec);
                }
            }

            SimpleWrapLayout layout = SimpleWrapLayout.Compute(this.GetChildSizes(), available, m_horizontalSpacing, m_verticalSpacing);
            this.SetMeasuredDimension(
                ResolveSize((int)Math.Ceiling(layout.Width) + horizontalPadding, widthMeasureSpec),
                ResolveSize((int)Math.Ceiling(layout.Height) + verticalPadding, heightMeasureSpec)
            );
        }

        protected override void OnLayout (bool changed, int left, int top, int right, int bottom)
        {
            int available = Math.Max(0, (right - left) - this.PaddingLeft - this.PaddingRight);
            bool isRightToLeft = this.LayoutDirection == Android.Views.LayoutDirection.Rtl;
            SimpleWrapLayout layout = SimpleWrapLayout.Compute(this.GetChildSizes(), available, m_horizontalSpacing, m_verticalSpacing);
            for (int index = 0; index < this.ChildCount; ++index)
            {
                SimpleWrapLayout.Slot slot = layout.Slots[index];
                if (!slot.IsPlaced)
                {
                    continue;
                }

                int width = (int)Math.Round(slot.Width);
                int height = (int)Math.Round(slot.Height);
                int x = (int)Math.Round(isRightToLeft ? available - slot.X - slot.Width : slot.X) + this.PaddingLeft;
                int y = (int)Math.Round(slot.Y) + this.PaddingTop;
                this.GetChildAt(index)!.Layout(x, y, x + width, y + height);
            }
        }

        // ===========[ Helper Methods ]===================================
        // A gone child wants no size, so the layout gives it no slot
        private WrapItemSize[] GetChildSizes ()
        {
            var sizes = new WrapItemSize[this.ChildCount];
            for (int index = 0; index < sizes.Length; ++index)
            {
                View child = this.GetChildAt(index)!;
                sizes[index] = child.Visibility == ViewStates.Gone ? default : new WrapItemSize(child.MeasuredWidth, child.MeasuredHeight);
            }

            return sizes;
        }
    }
}
