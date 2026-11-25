namespace AJut.UX.Helpers
{
    /// <summary>A point on screen, in physical pixels</summary>
    public readonly record struct ScreenPixelPoint (int X, int Y);

    /// <summary>A rectangle on screen, in physical pixels</summary>
    public readonly record struct ScreenPixelRect (int Left, int Top, int Width, int Height)
    {
        public int Right => this.Left + this.Width;
        public int Bottom => this.Top + this.Height;
    }

    /// <summary>
    /// Shows what a pick from screen would take, for as long as the pick runs: the color under the cursor and the pixels
    /// around it, and whether the left button is held (<see cref="ScreenColorPickSample.IsPressing"/>), so it can tell
    /// the user what to do next. Each stack supplies its own, so it looks and themes like the rest of its controls.
    /// </summary>
    public interface IScreenColorPickMagnifier
    {
        /// <summary>Shows the magnifier for <paramref name="sample"/>, or refreshes and moves it if it is up already</summary>
        void ShowSample (ScreenColorPickSample sample);

        /// <summary>Takes the magnifier down, when the pick ends</summary>
        void Hide ();
    }

    /// <summary>How a pick from screen runs</summary>
    public sealed record ScreenColorPickOptions
    {
        public const int kDefaultMagnifierPixelCount = 25;

        /// <summary>What shows the color and pixels under the cursor while the pick runs, or null for none</summary>
        public IScreenColorPickMagnifier? Magnifier { get; init; }

        /// <summary>
        /// How many pixels across the magnifier shows. An even count is rounded up to odd, so one pixel sits dead
        /// center. See <see cref="kDefaultMagnifierPixelCount"/>.
        /// </summary>
        public int MagnifierPixelCount { get; init; } = kDefaultMagnifierPixelCount;

        /// <summary><see cref="MagnifierPixelCount"/> as it is used: at least one, and odd</summary>
        public int OddMagnifierPixelCount => Math.Max(1, this.MagnifierPixelCount) | 1;
    }

    /// <summary>
    /// What is under the cursor while a pick from screen runs: the target pixel's color, the square of pixels around
    /// it, whether the left button is held, and where on screen it all is, for placing a magnifier out of the way.
    /// </summary>
    public sealed class ScreenColorPickSample
    {
        private const double kMagnifierGapDips = 40.0;
        private const double kDipsPerInch = 96.0;

        private readonly ColorArgb[] m_pixels;

        // ===========[ Setup/Construction/Teardown ]===================================
        /// <summary>Builds a sample from a square of pixels, row by row, with the target pixel at its center</summary>
        /// <param name="pixels">PixelCount x PixelCount colors, row by row; transparent where off every monitor</param>
        /// <param name="pixelCount">Pixels across the square, odd</param>
        /// <param name="cursor">The cursor, on the target pixel</param>
        /// <param name="workArea">The work area of the monitor the cursor is on</param>
        /// <param name="monitorScale">That monitor's scale, 1.0 at 96 DPI</param>
        /// <param name="isPressing">True while the left button is held, so letting it go would pick</param>
        public ScreenColorPickSample (ColorArgb[] pixels, int pixelCount, ScreenPixelPoint cursor, ScreenPixelRect workArea, double monitorScale, bool isPressing)
        {
            if (pixelCount < 1 || (pixelCount % 2) == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(pixelCount), pixelCount, "A sample's square of pixels must be odd across, so one pixel sits at its center");
            }

            if (pixels.Length != pixelCount * pixelCount)
            {
                throw new ArgumentException($"A sample {pixelCount} pixels across needs {pixelCount * pixelCount} pixels, not {pixels.Length}", nameof(pixels));
            }

            m_pixels = pixels;
            this.PixelCount = pixelCount;
            this.Cursor = cursor;
            this.WorkArea = workArea;
            this.MonitorScale = monitorScale;
            this.IsPressing = isPressing;

            int center = pixelCount / 2;
            this.Color = pixels[(center * pixelCount) + center];
            this.OutlineColor = InteractiveSurfaceColors.BuildFromAccent(this.Color).Normal.Border;
        }

        // ===========[ Properties ]===================================
        /// <summary>The color of the target pixel, the one a release would pick</summary>
        public ColorArgb Color { get; }

        /// <summary>A border color that stands out against <see cref="Color"/>, for outlining the target pixel</summary>
        public ColorArgb OutlineColor { get; }

        /// <summary><see cref="Color"/> as #RRGGBB</summary>
        public string Hex => $"#{this.Color.R:X2}{this.Color.G:X2}{this.Color.B:X2}";

        /// <summary>Pixels across the square around the target pixel, always odd</summary>
        public int PixelCount { get; }

        /// <summary>The square of pixels around the target pixel, row by row, transparent where off every monitor</summary>
        public ReadOnlySpan<ColorArgb> Pixels => m_pixels;

        /// <summary>The cursor, in physical pixels</summary>
        public ScreenPixelPoint Cursor { get; }

        /// <summary>The work area of the monitor the cursor is on, in physical pixels</summary>
        public ScreenPixelRect WorkArea { get; }

        /// <summary>The scale of the monitor the cursor is on, 1.0 at 96 DPI</summary>
        public double MonitorScale { get; }

        /// <summary>
        /// True while the left button is held, so letting it go would pick <see cref="Color"/>. False while the pick
        /// waits on a press, from the moment it starts.
        /// </summary>
        public bool IsPressing { get; }

        // ===========[ Public Interface Methods ]===================================
        /// <summary>
        /// Where a magnifier <paramref name="width"/> x <paramref name="height"/> physical pixels goes: centered over
        /// the cursor and a clear gap above it, or below it when there is no room above, and pushed sideways (or up)
        /// to stay on the cursor's monitor.
        /// </summary>
        public ScreenPixelPoint PlaceMagnifier (int width, int height)
        {
            int gap = (int)Math.Round(kMagnifierGapDips * this.MonitorScale);

            int x = this.Cursor.X - (width / 2);
            x = Math.Max(this.WorkArea.Left, Math.Min(x, this.WorkArea.Right - width));

            int y = this.Cursor.Y - gap - height;
            if (y < this.WorkArea.Top)
            {
                y = Math.Min(this.Cursor.Y + gap, this.WorkArea.Bottom - height);
            }

            return new ScreenPixelPoint(x, y);
        }

        /// <summary>
        /// Writes the square of pixels as 32 bit BGRA (premultiplied, which an opaque or fully transparent pixel
        /// already is), each pixel blown up to <paramref name="cellSize"/> x <paramref name="cellSize"/>, for a
        /// bitmap of PixelCount x cellSize on each side.
        /// </summary>
        public void WriteBgra (Span<byte> destination, int cellSize)
        {
            int side = this.PixelCount * cellSize;
            int stride = side * BgraPixels.kBytesPerPixel;
            for (int y = 0; y < side; ++y)
            {
                int rowStart = (y / cellSize) * this.PixelCount;
                for (int x = 0; x < side; ++x)
                {
                    ColorArgb color = m_pixels[rowStart + (x / cellSize)];
                    int index = (y * stride) + (x * BgraPixels.kBytesPerPixel);
                    destination[index] = color.B;
                    destination[index + 1] = color.G;
                    destination[index + 2] = color.R;
                    destination[index + 3] = color.A;
                }
            }
        }

        /// <summary>The scale of a monitor at <paramref name="dpi"/></summary>
        internal static double ScaleForDpi (uint dpi) => dpi / kDipsPerInch;
    }
}
