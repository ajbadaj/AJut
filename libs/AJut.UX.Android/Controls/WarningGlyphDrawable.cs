namespace AJut.UX.Controls
{
    using Android.Graphics;
    using Android.Graphics.Drawables;

    /// <summary>
    /// The warning glyph AJut's Android editors show beside text they can't take: a triangle in the given color with its
    /// exclamation mark cut out of it, the way UIKit's filled warning triangle reads, so whatever is behind the glyph
    /// shows through the mark
    /// </summary>
    internal sealed class WarningGlyphDrawable : Drawable
    {
        // Each a fraction of the glyph's height (or width, for the mark's thickness and the corner rounding)
        private const float kApex = 0.08f;
        private const float kBase = 0.94f;
        private const float kMarkTop = 0.38f;
        private const float kMarkBottom = 0.64f;
        private const float kDotCenter = 0.80f;
        private const float kMarkThickness = 0.12f;
        private const float kCornerRounding = 0.12f;

        // The dot's radius, a fraction of the mark's thickness, so it reads a little heavier than the bar
        private const float kDotRadius = 0.6f;

        private readonly Paint m_fill = new Paint(PaintFlags.AntiAlias);
        private readonly Paint m_cut = new Paint(PaintFlags.AntiAlias);
        private readonly Android.Graphics.Path m_triangle = new Android.Graphics.Path();
        private RectF m_bounds = new RectF();

        public WarningGlyphDrawable (Color color)
        {
            m_fill.Color = color;
            m_cut.SetXfermode(new PorterDuffXfermode(PorterDuff.Mode.Clear!));
            m_cut.StrokeCap = Paint.Cap.Round;
        }

        public override int Opacity => (int)Format.Translucent;

        public override void Draw (Canvas canvas)
        {
            float height = m_bounds.Height();
            float centerX = m_bounds.CenterX();

            // The mark clears what is under it, so the glyph is drawn on a layer of its own; drawn straight onto the
            //  canvas, the cut would clear whatever was painted behind the glyph as well
            int saved = canvas.SaveLayer(m_bounds, null);
            canvas.DrawPath(m_triangle, m_fill);
            canvas.DrawLine(centerX, m_bounds.Top + (height * kMarkTop), centerX, m_bounds.Top + (height * kMarkBottom), m_cut);
            canvas.DrawCircle(centerX, m_bounds.Top + (height * kDotCenter), m_cut.StrokeWidth * kDotRadius, m_cut);
            canvas.RestoreToCount(saved);
        }

        // The shape only changes with the size, so it is made here rather than on every draw
        protected override void OnBoundsChange (Rect bounds)
        {
            base.OnBoundsChange(bounds);
            m_bounds = new RectF(bounds);
            float width = m_bounds.Width();
            float height = m_bounds.Height();
            m_triangle.Reset();
            m_triangle.MoveTo(m_bounds.CenterX(), m_bounds.Top + (height * kApex));
            m_triangle.LineTo(m_bounds.Right, m_bounds.Top + (height * kBase));
            m_triangle.LineTo(m_bounds.Left, m_bounds.Top + (height * kBase));
            m_triangle.Close();
            m_fill.SetPathEffect(new CornerPathEffect(width * kCornerRounding));
            m_cut.StrokeWidth = width * kMarkThickness;
        }

        public override void SetAlpha (int alpha)
        {
            m_fill.Alpha = alpha;
            this.InvalidateSelf();
        }

        public override void SetColorFilter (ColorFilter? colorFilter)
        {
            m_fill.SetColorFilter(colorFilter);
            this.InvalidateSelf();
        }
    }
}
