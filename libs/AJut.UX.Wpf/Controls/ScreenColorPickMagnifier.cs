namespace AJut.UX.Controls
{
    using System;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Interop;
    using System.Windows.Media;
    using System.Windows.Media.Imaging;
    using AJut.UX.Helpers;
    using DPUtils = DPUtils<ScreenColorPickMagnifier>;

    // ===========[ ScreenColorPickMagnifier ]====================================
    // What a pick from screen would take, from the moment the pick starts: a title bar saying what to do next
    // (press, then release), a swatch of the color under the cursor, its hex, and the pixels around it blown up
    // like pixel art, with the target pixel outlined in a color that stands out against it. It shows faded until
    // the button is pressed. ShowSample fills it from a ScreenColorPicker sample; the eyedropper on the color
    // editors shows it in a small window of its own (see ScreenColorPickMagnifier.Host).
    public class ScreenColorPickMagnifier : Control
    {
        private const double kOutlineThickness = 2.0;
        private const double kDipsPerInch = 96.0;

        private WriteableBitmap m_pixels;

        // ===========[ Setup/Construction/Teardown ]===================================
        static ScreenColorPickMagnifier ()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ScreenColorPickMagnifier), new FrameworkPropertyMetadata(typeof(ScreenColorPickMagnifier)));
        }

        public ScreenColorPickMagnifier ()
        {
            this.UpdateTargetOutlineSize();
        }

        // ===========[ Dependency Properties ]===================================
        public static readonly DependencyProperty SampleColorProperty = DPUtils.Register(_ => _.SampleColor);
        /// <summary>The color under the cursor, the one a release would pick</summary>
        public Color SampleColor
        {
            get => (Color)this.GetValue(SampleColorProperty);
            set => this.SetValue(SampleColorProperty, value);
        }

        public static readonly DependencyProperty HexProperty = DPUtils.Register(_ => _.Hex);
        /// <summary><see cref="SampleColor"/> as #RRGGBB</summary>
        public string Hex
        {
            get => (string)this.GetValue(HexProperty);
            set => this.SetValue(HexProperty, value);
        }

        public static readonly DependencyProperty PixelsProperty = DPUtils.Register(_ => _.Pixels);
        /// <summary>The pixels around the cursor, one bitmap pixel each, for the template to blow up without smoothing</summary>
        public ImageSource Pixels
        {
            get => (ImageSource)this.GetValue(PixelsProperty);
            set => this.SetValue(PixelsProperty, value);
        }

        public static readonly DependencyProperty TargetOutlineBrushProperty = DPUtils.Register(_ => _.TargetOutlineBrush);
        /// <summary>The outline around the target pixel, picked to stand out against its color</summary>
        public Brush TargetOutlineBrush
        {
            get => (Brush)this.GetValue(TargetOutlineBrushProperty);
            set => this.SetValue(TargetOutlineBrushProperty, value);
        }

        public static readonly DependencyProperty IsPressingProperty = DPUtils.Register(_ => _.IsPressing);
        /// <summary>True while the mouse button is held, so letting go would pick; set by each sample</summary>
        public bool IsPressing
        {
            get => (bool)this.GetValue(IsPressingProperty);
            set => this.SetValue(IsPressingProperty, value);
        }

        public static readonly DependencyProperty TitleBarBackgroundProperty = DPUtils.Register(_ => _.TitleBarBackground);
        /// <summary>The background of the title bar that says what to do next</summary>
        public Brush TitleBarBackground
        {
            get => (Brush)this.GetValue(TitleBarBackgroundProperty);
            set => this.SetValue(TitleBarBackgroundProperty, value);
        }

        public static readonly DependencyProperty TitleBarForegroundProperty = DPUtils.Register(_ => _.TitleBarForeground);
        /// <summary>The text color of the title bar that says what to do next</summary>
        public Brush TitleBarForeground
        {
            get => (Brush)this.GetValue(TitleBarForegroundProperty);
            set => this.SetValue(TitleBarForegroundProperty, value);
        }

        public static readonly DependencyProperty PreviewSizeProperty = DPUtils.Register(_ => _.PreviewSize, 150.0, (d, e) => d.UpdateTargetOutlineSize());
        /// <summary>The width and height of the blown up pixels</summary>
        public double PreviewSize
        {
            get => (double)this.GetValue(PreviewSizeProperty);
            set => this.SetValue(PreviewSizeProperty, value);
        }

        public static readonly DependencyProperty PixelCountProperty = DPUtils.Register(_ => _.PixelCount, ScreenColorPickOptions.kDefaultMagnifierPixelCount, (d, e) => d.UpdateTargetOutlineSize());
        /// <summary>How many pixels across the preview shows, set by each sample</summary>
        public int PixelCount
        {
            get => (int)this.GetValue(PixelCountProperty);
            set => this.SetValue(PixelCountProperty, value);
        }

        private static readonly DependencyPropertyKey TargetOutlineSizePropertyKey = DPUtils.RegisterReadOnly(_ => _.TargetOutlineSize);
        public static readonly DependencyProperty TargetOutlineSizeProperty = TargetOutlineSizePropertyKey.DependencyProperty;
        /// <summary>The size of the outline around the target pixel: one blown up pixel, plus the outline on each side</summary>
        public double TargetOutlineSize
        {
            get => (double)this.GetValue(TargetOutlineSizeProperty);
            protected set => this.SetValue(TargetOutlineSizePropertyKey, value);
        }

        // ===========[ Public Interface Methods ]===================================
        /// <summary>Shows <paramref name="sample"/></summary>
        public void ShowSample (ScreenColorPickSample sample)
        {
            this.SampleColor = sample.Color.ToMediaColor();
            this.Hex = sample.Hex;
            this.TargetOutlineBrush = sample.OutlineColor.ToBrush();
            this.PixelCount = sample.PixelCount;
            this.IsPressing = sample.IsPressing;

            if (m_pixels == null || m_pixels.PixelWidth != sample.PixelCount)
            {
                m_pixels = new WriteableBitmap(sample.PixelCount, sample.PixelCount, kDipsPerInch, kDipsPerInch, PixelFormats.Pbgra32, null);
                this.Pixels = m_pixels;
            }

            const int kBytesPerPixel = 4;
            int stride = sample.PixelCount * kBytesPerPixel;
            var bgra = new byte[stride * sample.PixelCount];
            sample.WriteBgra(bgra, 1);
            m_pixels.WritePixels(new Int32Rect(0, 0, sample.PixelCount, sample.PixelCount), bgra, stride, 0);
        }

        // ===========[ Helper Methods ]===================================
        private void UpdateTargetOutlineSize ()
        {
            this.TargetOutlineSize = (this.PreviewSize / Math.Max(1, this.PixelCount)) + (kOutlineThickness * 2.0);
        }

        // ===========[ Subclasses/structs ]===================================
        /// <summary>
        /// Shows a <see cref="ScreenColorPickMagnifier"/> during a pick from screen, in a small window of its own: no
        /// chrome, no taskbar button, above the pick's overlay, and never activated, since the overlay losing
        /// activation cancels the pick.
        /// </summary>
        internal sealed class Host : IScreenColorPickMagnifier
        {
            private MagnifierWindow m_window;
            private ScreenColorPickMagnifier m_magnifier;

            public void ShowSample (ScreenColorPickSample sample)
            {
                if (m_window == null)
                {
                    m_magnifier = new ScreenColorPickMagnifier();
                    m_window = new MagnifierWindow(m_magnifier);
                }

                m_magnifier.ShowSample(sample);
                if (!m_window.IsVisible)
                {
                    m_window.Show();
                }

                // Placed before it renders, so it never shows a frame anywhere else
                m_window.UpdateLayout();
                ScreenColorPicker.PlaceMagnifierWindow(new WindowInteropHelper(m_window).Handle, sample);
            }

            public void Hide ()
            {
                m_window?.Close();
                m_window = null;
                m_magnifier = null;
            }

            /// <summary>
            /// The magnifier's window: transparent, so the magnifier's own border is the only edge it has.
            /// </summary>
            /// <remarks>
            /// It is a type of its own so an app's implicit Window style never reaches it. An implicit style applies
            /// to elements of exactly its target type, and an app that themes every Window (the AJut themed window
            /// style, with its glowing border and chrome) would otherwise draw a window frame around the magnifier.
            /// </remarks>
            private sealed class MagnifierWindow : Window
            {
                public MagnifierWindow (ScreenColorPickMagnifier magnifier)
                {
                    this.Content = magnifier;
                    this.WindowStyle = WindowStyle.None;
                    this.AllowsTransparency = true;
                    this.Background = Brushes.Transparent;
                    this.BorderThickness = new Thickness(0);
                    this.ResizeMode = ResizeMode.NoResize;
                    this.SizeToContent = SizeToContent.WidthAndHeight;
                    this.ShowInTaskbar = false;
                    this.ShowActivated = false;
                    this.Topmost = true;
                    this.Focusable = false;
                    this.IsHitTestVisible = false;
                }
            }
        }
    }
}
