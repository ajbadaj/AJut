namespace AJut.UX.Controls
{
    using System;
    using Android.Content;
    using Android.Graphics;
    using Android.Runtime;
    using Android.Util;
    using Android.Views;
    using AndroidColor = Android.Graphics.Color;

    /// <summary>
    /// A hue, saturation and value color picker drawn as one view: a saturation and value square for the current
    /// hue, a hue bar under it, and an alpha bar under that. Drag in any of the three to change the color.
    /// </summary>
    /// <remarks>
    /// Android has no stock color picker the way WinUI3 has ColorPicker, so ColorEditIngressControl opens this one.
    /// It keeps its own hue, saturation and value rather than deriving them from the color each time: a gray or
    /// black color has no hue, so converting back would snap the hue bar to red the moment saturation or value hit
    /// zero, and the square would lose the hue the user was on.
    /// </remarks>
    [Register("ajut.ux.controls.HsvColorPicker")]
    public class HsvColorPicker : View
    {
        private const float kDefaultWidthDp = 280f;
        private const float kSquareAspect = 0.6f;
        private const float kBarHeightDp = 28f;
        private const float kGapDp = 14f;
        private const float kThumbRadiusDp = 9f;
        private const float kCheckerCellDp = 6f;

        private static readonly int[] kHueSpectrum = new int[]
        {
            unchecked((int)0xFFFF0000), unchecked((int)0xFFFFFF00), unchecked((int)0xFF00FF00), unchecked((int)0xFF00FFFF),
            unchecked((int)0xFF0000FF), unchecked((int)0xFFFF00FF), unchecked((int)0xFFFF0000),
        };

        private readonly Paint m_fillPaint = new Paint(PaintFlags.AntiAlias);
        private readonly Paint m_thumbPaint = new Paint(PaintFlags.AntiAlias);
        private readonly float[] m_hsv = new float[3];
        private Shader? m_squareSaturationShader;
        private Shader? m_squareValueShader;
        private Shader? m_hueShader;
        private Shader? m_alphaShader;
        private RectF m_squareRect = new RectF();
        private RectF m_hueRect = new RectF();
        private RectF m_alphaRect = new RectF();
        private Shader? m_checkerShader;
        private float m_density = 1f;
        private float m_hue;
        private float m_saturation;
        private float m_value = 1f;
        private int m_alpha = 255;
        private bool m_isAlphaEnabled = true;
        private eRegion m_dragRegion = eRegion.None;

        // ===========[ Setup/Construction/Teardown ]===================================
        public HsvColorPicker (Context context) : base(context)
        {
            this.Initialize(context);
        }

        public HsvColorPicker (Context context, IAttributeSet? attrs) : base(context, attrs)
        {
            this.Initialize(context);
        }

        public HsvColorPicker (Context context, IAttributeSet? attrs, int defStyleAttr) : base(context, attrs, defStyleAttr)
        {
            this.Initialize(context);
        }

        protected HsvColorPicker (IntPtr javaReference, JniHandleOwnership transfer) : base(javaReference, transfer)
        {
        }

        // ===========[ Events ]===================================
        /// <summary>Raised whenever <see cref="Color"/> changes, by a drag or from code.</summary>
        public event EventHandler<EventArgs<AndroidColor>>? ColorChanged;

        // ===========[ Properties ]===================================
        /// <summary>The picked color. Setting a gray (or black) keeps the hue the picker was already on.</summary>
        public AndroidColor Color
        {
            get => this.ComposeColor();
            set
            {
                AndroidColor.ColorToHSV(value, m_hsv);
                bool hasHue = (m_hsv[1] > 0f) && (m_hsv[2] > 0f);
                if (hasHue)
                {
                    m_hue = m_hsv[0];
                }

                m_saturation = m_hsv[1];
                m_value = m_hsv[2];
                m_alpha = m_isAlphaEnabled ? value.A : 255;
                this.OnColorEdited();
            }
        }

        /// <summary>The current hue, 0 to 360.</summary>
        public float Hue => m_hue;

        /// <summary>Whether the alpha bar shows. With it off the picked color is always opaque.</summary>
        public bool IsAlphaEnabled
        {
            get => m_isAlphaEnabled;
            set
            {
                if (m_isAlphaEnabled == value)
                {
                    return;
                }

                m_isAlphaEnabled = value;
                if (!value && (m_alpha != 255))
                {
                    m_alpha = 255;
                    this.OnColorEdited();
                }

                this.RequestLayout();
            }
        }

        // ===========[ Public Interface Methods ]===================================
        protected override void OnMeasure (int widthMeasureSpec, int heightMeasureSpec)
        {
            int defaultWidth = (int)(kDefaultWidthDp * m_density);
            int width = ResolveSize(defaultWidth + this.PaddingLeft + this.PaddingRight, widthMeasureSpec);
            float thumbReach = (kThumbRadiusDp + 2f) * m_density;
            float contentWidth = width - this.PaddingLeft - this.PaddingRight - (thumbReach * 2f);

            float contentHeight = (thumbReach * 2f) + (contentWidth * kSquareAspect) + ((kGapDp + kBarHeightDp) * m_density);
            if (m_isAlphaEnabled)
            {
                contentHeight += (kGapDp + kBarHeightDp) * m_density;
            }

            int height = ResolveSize((int)Math.Ceiling(contentHeight) + this.PaddingTop + this.PaddingBottom, heightMeasureSpec);
            this.SetMeasuredDimension(width, height);
        }

        protected override void OnSizeChanged (int w, int h, int oldw, int oldh)
        {
            base.OnSizeChanged(w, h, oldw, oldh);

            // 1. Lay out the three regions top to bottom inside the padding
            //    Inset by a thumb's reach, so a thumb at either end of a bar or an edge of the square is not clipped.
            float thumbReach = (kThumbRadiusDp + 2f) * m_density;
            float left = this.PaddingLeft + thumbReach;
            float right = w - this.PaddingRight - thumbReach;
            float top = this.PaddingTop + thumbReach;
            float barHeight = kBarHeightDp * m_density;
            float gap = kGapDp * m_density;

            m_squareRect = new RectF(left, top, right, top + ((right - left) * kSquareAspect));
            m_hueRect = new RectF(left, m_squareRect.Bottom + gap, right, m_squareRect.Bottom + gap + barHeight);
            m_alphaRect = new RectF(left, m_hueRect.Bottom + gap, right, m_hueRect.Bottom + gap + barHeight);

            // 2. The value fade and hue spectrum only depend on size; the rest follow the color as well
            m_squareValueShader = new LinearGradient(0, m_squareRect.Top, 0, m_squareRect.Bottom, AndroidColor.Transparent, AndroidColor.Black, Shader.TileMode.Clamp!);
            m_hueShader = new LinearGradient(m_hueRect.Left, 0, m_hueRect.Right, 0, kHueSpectrum, null, Shader.TileMode.Clamp!);
            this.RebuildColorShaders();
        }

        protected override void OnDraw (Canvas canvas)
        {
            base.OnDraw(canvas);
            float radius = 4f * m_density;

            // ------ The saturation (left to right) and value (top to bottom) square for the current hue
            m_fillPaint.SetShader(m_squareSaturationShader);
            canvas.DrawRoundRect(m_squareRect, radius, radius, m_fillPaint);
            m_fillPaint.SetShader(m_squareValueShader);
            canvas.DrawRoundRect(m_squareRect, radius, radius, m_fillPaint);

            // ------ The hue bar
            m_fillPaint.SetShader(m_hueShader);
            canvas.DrawRoundRect(m_hueRect, radius, radius, m_fillPaint);

            // ------ The alpha bar, over a checkerboard so transparency reads
            if (m_isAlphaEnabled)
            {
                m_fillPaint.SetShader(this.GetCheckerShader());
                canvas.DrawRoundRect(m_alphaRect, radius, radius, m_fillPaint);
                m_fillPaint.SetShader(m_alphaShader);
                canvas.DrawRoundRect(m_alphaRect, radius, radius, m_fillPaint);
            }

            m_fillPaint.SetShader(null);

            // ------ Thumbs
            this.DrawThumb(canvas, m_squareRect.Left + (m_saturation * m_squareRect.Width()), m_squareRect.Top + ((1f - m_value) * m_squareRect.Height()));
            this.DrawThumb(canvas, m_hueRect.Left + ((m_hue / 360f) * m_hueRect.Width()), m_hueRect.CenterY());
            if (m_isAlphaEnabled)
            {
                this.DrawThumb(canvas, m_alphaRect.Left + ((m_alpha / 255f) * m_alphaRect.Width()), m_alphaRect.CenterY());
            }
        }

        public override bool OnTouchEvent (MotionEvent? e)
        {
            if ((e == null) || !this.Enabled)
            {
                return false;
            }

            switch (e.ActionMasked)
            {
                case MotionEventActions.Down:
                    m_dragRegion = this.FindRegion(e.GetX(), e.GetY());
                    if (m_dragRegion == eRegion.None)
                    {
                        return false;
                    }

                    // A drag inside the picker is the picker's, even inside a scrolling parent.
                    this.Parent?.RequestDisallowInterceptTouchEvent(true);
                    this.ApplyDrag(e.GetX(), e.GetY());
                    return true;

                case MotionEventActions.Move:
                    this.ApplyDrag(e.GetX(), e.GetY());
                    return true;

                case MotionEventActions.Up:
                    this.ApplyDrag(e.GetX(), e.GetY());
                    m_dragRegion = eRegion.None;
                    this.PerformClick();
                    return true;

                case MotionEventActions.Cancel:
                    m_dragRegion = eRegion.None;
                    return true;
            }

            return base.OnTouchEvent(e);
        }

        // The drag itself is the click; nothing else to do, but a view that takes touches has to answer this.
        public override bool PerformClick () => base.PerformClick();

        // ===========[ Helper Methods ]===================================
        private void Initialize (Context context)
        {
            m_density = context.Resources?.DisplayMetrics?.Density ?? 1f;
            m_thumbPaint.SetStyle(Paint.Style.Stroke!);
        }

        private AndroidColor ComposeColor ()
        {
            m_hsv[0] = m_hue;
            m_hsv[1] = m_saturation;
            m_hsv[2] = m_value;
            int rgb = AndroidColor.HSVToColor(m_hsv);
            return new AndroidColor((m_alpha << 24) | (rgb & 0x00FFFFFF));
        }

        private void OnColorEdited ()
        {
            this.RebuildColorShaders();
            this.Invalidate();
            this.ColorChanged?.Invoke(this, new EventArgs<AndroidColor>(this.ComposeColor()));
        }

        // Built here rather than in OnDraw, so drawing allocates nothing; a color change or a resize rebuilds them.
        private void RebuildColorShaders ()
        {
            if (m_squareRect.Width() <= 0f)
            {
                return;
            }

            m_hsv[0] = m_hue;
            m_hsv[1] = 1f;
            m_hsv[2] = 1f;
            int pureHue = AndroidColor.HSVToColor(m_hsv);
            m_squareSaturationShader = new LinearGradient(m_squareRect.Left, 0, m_squareRect.Right, 0, AndroidColor.White, new AndroidColor(pureHue), Shader.TileMode.Clamp!);

            int opaque = this.ComposeColor().ToArgb() | unchecked((int)0xFF000000);
            m_alphaShader = new LinearGradient(m_alphaRect.Left, 0, m_alphaRect.Right, 0, new AndroidColor(opaque & 0x00FFFFFF), new AndroidColor(opaque), Shader.TileMode.Clamp!);
        }

        private eRegion FindRegion (float x, float y)
        {
            // Let a touch land a little outside a bar and still count, since the bars are thin targets for a finger.
            float slop = kThumbRadiusDp * m_density;
            if (_Contains(m_squareRect, x, y, slop))
            {
                return eRegion.Square;
            }

            if (_Contains(m_hueRect, x, y, slop))
            {
                return eRegion.Hue;
            }

            if (m_isAlphaEnabled && _Contains(m_alphaRect, x, y, slop))
            {
                return eRegion.Alpha;
            }

            return eRegion.None;
        }

        private void ApplyDrag (float x, float y)
        {
            switch (m_dragRegion)
            {
                case eRegion.Square:
                    m_saturation = _Fraction(x, m_squareRect.Left, m_squareRect.Right);
                    m_value = 1f - _Fraction(y, m_squareRect.Top, m_squareRect.Bottom);
                    break;

                case eRegion.Hue:
                    m_hue = _Fraction(x, m_hueRect.Left, m_hueRect.Right) * 360f;
                    break;

                case eRegion.Alpha:
                    m_alpha = (int)Math.Round(_Fraction(x, m_alphaRect.Left, m_alphaRect.Right) * 255f);
                    break;

                default:
                    return;
            }

            this.OnColorEdited();
        }

        private void DrawThumb (Canvas canvas, float x, float y)
        {
            float radius = kThumbRadiusDp * m_density;
            m_thumbPaint.StrokeWidth = 4f * m_density;
            m_thumbPaint.Color = new AndroidColor(unchecked((int)0x80000000));
            canvas.DrawCircle(x, y, radius, m_thumbPaint);
            m_thumbPaint.StrokeWidth = 2f * m_density;
            m_thumbPaint.Color = AndroidColor.White;
            canvas.DrawCircle(x, y, radius, m_thumbPaint);
        }

        private Shader GetCheckerShader ()
        {
            if (m_checkerShader == null)
            {
                int cell = Math.Max(1, (int)(kCheckerCellDp * m_density));
                Bitmap checker = Bitmap.CreateBitmap(cell * 2, cell * 2, Bitmap.Config.Argb8888!)!;
                checker.EraseColor(unchecked((int)0xFFFFFFFF));
                using (var canvas = new Canvas(checker))
                using (var paint = new Paint { Color = new AndroidColor(unchecked((int)0xFFCCCCCC)) })
                {
                    canvas.DrawRect(0, 0, cell, cell, paint);
                    canvas.DrawRect(cell, cell, cell * 2, cell * 2, paint);
                }

                m_checkerShader = new BitmapShader(checker, Shader.TileMode.Repeat!, Shader.TileMode.Repeat!);
            }

            return m_checkerShader;
        }

        private static bool _Contains (RectF rect, float x, float y, float slop)
        {
            return (x >= rect.Left - slop)
                && (x <= rect.Right + slop)
                && (y >= rect.Top - slop)
                && (y <= rect.Bottom + slop);
        }

        private static float _Fraction (float position, float start, float end)
        {
            return Math.Clamp((position - start) / (end - start), 0f, 1f);
        }

        // ===========[ Subclasses/structs ]===================================
        private enum eRegion
        {
            None,
            Square,
            Hue,
            Alpha,
        }
    }
}
