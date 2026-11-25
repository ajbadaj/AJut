namespace AJut.UX.Controls
{
    using System;
    using System.IO;
    using System.Runtime.InteropServices.WindowsRuntime;
    using AJut.UX.Helpers;
    using Microsoft.UI.Windowing;
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;
    using Microsoft.UI.Xaml.Media;
    using Microsoft.UI.Xaml.Media.Imaging;
    using DPUtils = AJut.UX.DPUtils<ScreenColorPickMagnifier>;

    // ===========[ ScreenColorPickMagnifier ]====================================
    // What a pick from screen would take, from the moment the pick starts: a title bar saying what to do next
    // (press, then release), a swatch of the color under the cursor, its hex, and the pixels around it blown up
    // like pixel art, with the target pixel outlined in a color that stands out against it. ShowSample fills it
    // from a ScreenColorPicker sample; the eyedropper on ColorEditIngressControl shows it in a small window of
    // its own, faded until the button is pressed (see ScreenColorPickMagnifier.Host).
    //
    // WinUI3's Image smooths whatever it scales, so the pixels are blown up into the bitmap itself, at the
    // target monitor's scale, rather than left to the Image.
    public class ScreenColorPickMagnifier : Control
    {
        private const double kOutlineThickness = 2.0;

        private WriteableBitmap m_pixels;
        private byte[] m_pixelBytes;

        // ===========[ Setup/Construction/Teardown ]===================================
        public ScreenColorPickMagnifier ()
        {
            this.DefaultStyleKey = typeof(ScreenColorPickMagnifier);
            this.UpdateTargetOutlineSize();
        }

        protected override void OnApplyTemplate ()
        {
            base.OnApplyTemplate();
            this.UpdatePressState();
        }

        // ===========[ Dependency Properties ]===================================
        public static readonly DependencyProperty SampleBrushProperty = DPUtils.Register(_ => _.SampleBrush);
        /// <summary>The color under the cursor, the one a release would pick</summary>
        public Brush SampleBrush
        {
            get => (Brush)this.GetValue(SampleBrushProperty);
            set => this.SetValue(SampleBrushProperty, value);
        }

        public static readonly DependencyProperty HexProperty = DPUtils.Register(_ => _.Hex);
        /// <summary>The color under the cursor as #RRGGBB</summary>
        public string Hex
        {
            get => (string)this.GetValue(HexProperty);
            set => this.SetValue(HexProperty, value);
        }

        public static readonly DependencyProperty PixelsProperty = DPUtils.Register(_ => _.Pixels);
        /// <summary>The pixels around the cursor, already blown up</summary>
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

        public static readonly DependencyProperty IsPressingProperty = DPUtils.Register(_ => _.IsPressing, (d, e) => d.UpdatePressState());
        /// <summary>True while the mouse button is held, so letting go would pick; set by each sample</summary>
        public bool IsPressing
        {
            get => (bool)this.GetValue(IsPressingProperty);
            set => this.SetValue(IsPressingProperty, value);
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

        public static readonly DependencyProperty TargetOutlineSizeProperty = DPUtils.Register(_ => _.TargetOutlineSize);
        /// <summary>The size of the outline around the target pixel: one blown up pixel, plus the outline on each side</summary>
        public double TargetOutlineSize
        {
            get => (double)this.GetValue(TargetOutlineSizeProperty);
            private set => this.SetValue(TargetOutlineSizeProperty, value);
        }

        // ===========[ Public Interface Methods ]===================================
        /// <summary>Shows <paramref name="sample"/></summary>
        public void ShowSample (ScreenColorPickSample sample)
        {
            const int kBytesPerPixel = 4;

            this.SampleBrush = sample.Color.ToBrush();
            this.Hex = sample.Hex;
            this.TargetOutlineBrush = sample.OutlineColor.ToBrush();
            this.PixelCount = sample.PixelCount;
            this.IsPressing = sample.IsPressing;

            // Each sample pixel becomes a block of physical pixels, sized for the monitor the magnifier is shown on
            int cellSize = Math.Max(1, (int)Math.Round(this.PreviewSize * sample.MonitorScale / sample.PixelCount));
            int side = sample.PixelCount * cellSize;
            if (m_pixels == null || m_pixels.PixelWidth != side)
            {
                m_pixels = new WriteableBitmap(side, side);
                m_pixelBytes = new byte[side * side * kBytesPerPixel];
                this.Pixels = m_pixels;
            }

            sample.WriteBgra(m_pixelBytes, cellSize);
            using (Stream pixelStream = m_pixels.PixelBuffer.AsStream())
            {
                pixelStream.Write(m_pixelBytes, 0, m_pixelBytes.Length);
            }

            m_pixels.Invalidate();
        }

        // ===========[ Helper Methods ]===================================
        private void UpdateTargetOutlineSize ()
        {
            this.TargetOutlineSize = (this.PreviewSize / Math.Max(1, this.PixelCount)) + (kOutlineThickness * 2.0);
        }

        private void UpdatePressState ()
        {
            VisualStateManager.GoToState(this, this.IsPressing ? "Pressing" : "WaitingForPress", false);
        }

        // ===========[ Subclasses/structs ]===================================
        /// <summary>
        /// Shows a <see cref="ScreenColorPickMagnifier"/> during a pick from screen, in a small window of its own: no
        /// chrome, not in the task switcher, above the pick's overlay, and never activated, since the overlay losing
        /// activation cancels the pick.
        /// </summary>
        internal sealed class Host : IScreenColorPickMagnifier
        {
            // A first size to show at, before the magnifier has laid out and can say its own
            private const double kFirstGuessWidthDips = 260.0;
            private const double kFirstGuessHeightDips = 230.0;

            // How see-through the window is until the button is pressed, so it is plain the pick has started but
            // not yet taking anything
            private const double kWaitingForPressOpacity = 0.65;

            private readonly ElementTheme m_theme;
            private Window m_window;
            private ScreenColorPickMagnifier m_magnifier;
            private ScreenColorPickSample m_latestSample;
            private nint m_windowHandle;
            private bool m_isShownPressing;

            /// <param name="theme">The theme of the control the pick started from, for the magnifier to match</param>
            public Host (ElementTheme theme)
            {
                m_theme = theme;
            }

            public void ShowSample (ScreenColorPickSample sample)
            {
                m_latestSample = sample;
                if (m_window == null)
                {
                    this.OpenWindow(sample);
                }

                m_magnifier.ShowSample(sample);
                if (m_isShownPressing != sample.IsPressing)
                {
                    this.FadeWindow(sample.IsPressing);
                }

                if (m_magnifier.IsLoaded)
                {
                    this.PlaceWindow();
                }
            }

            public void Hide ()
            {
                if (m_magnifier != null)
                {
                    m_magnifier.Loaded -= this.Magnifier_OnLoaded;
                }

                m_window?.Close();
                m_window = null;
                m_magnifier = null;
                m_latestSample = null;
                m_windowHandle = IntPtr.Zero;
                m_isShownPressing = false;
            }

            private void OpenWindow (ScreenColorPickSample sample)
            {
                m_magnifier = new ScreenColorPickMagnifier
                {
                    RequestedTheme = m_theme,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Top,
                };
                m_magnifier.Loaded += this.Magnifier_OnLoaded;

                m_window = new Window { Content = m_magnifier };
                m_windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(m_window);

                AppWindow appWindow = m_window.AppWindow;
                appWindow.IsShownInSwitchers = false;
                if (appWindow.Presenter is OverlappedPresenter presenter)
                {
                    presenter.SetBorderAndTitleBar(false, false);
                    presenter.IsResizable = false;
                    presenter.IsMaximizable = false;
                    presenter.IsMinimizable = false;
                    presenter.IsAlwaysOnTop = true;
                }

                ScreenColorPicker.PlaceMagnifierWindow(
                    m_windowHandle,
                    sample,
                    (int)Math.Ceiling(kFirstGuessWidthDips * sample.MonitorScale),
                    (int)Math.Ceiling(kFirstGuessHeightDips * sample.MonitorScale)
                );

                // Faded or not before it first shows, so it never flashes in at the wrong opacity
                this.FadeWindow(sample.IsPressing);
                appWindow.Show(false);
            }

            // A WinUI3 Window draws over an opaque background, so fading the magnifier itself would only dim it
            // against that; the whole window is faded instead, to see through it
            private void FadeWindow (bool isPressing)
            {
                m_isShownPressing = isPressing;
                m_window.SetOpacity(isPressing ? 1.0 : kWaitingForPressOpacity);
            }

            private void Magnifier_OnLoaded (object sender, RoutedEventArgs e)
            {
                this.PlaceWindow();
            }

            // Sizes the window to the magnifier and places it for the latest sample
            private void PlaceWindow ()
            {
                if (m_magnifier == null || m_latestSample == null)
                {
                    return;
                }

                m_magnifier.Measure(new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));
                Windows.Foundation.Size size = m_magnifier.DesiredSize;
                if (size.Width <= 0 || size.Height <= 0)
                {
                    return;
                }

                ScreenColorPicker.PlaceMagnifierWindow(
                    m_windowHandle,
                    m_latestSample,
                    (int)Math.Ceiling(size.Width * m_latestSample.MonitorScale),
                    (int)Math.Ceiling(size.Height * m_latestSample.MonitorScale)
                );
            }
        }
    }
}
