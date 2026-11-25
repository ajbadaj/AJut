namespace AJut.UX.Controls
{
    using AJut.UX.Helpers;
    using System;
    using System.Threading;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Controls.Primitives;
    using System.Windows.Input;
    using System.Windows.Media;
    using System.Windows.Threading;
    using DPUtils = DPUtils<ColorValueEditControl>;

    // ===========[ ColorValueEditControl ]=========================================
    // WinUI3 port: pending.
    //
    // The eyedropper button (PART_PickFromScreenButton, shown while AllowPickFromScreen) picks EditColor from
    // anywhere on screen through ScreenColorPicker, with a magnifier following the cursor unless
    // ShowPickFromScreenMagnifier is off. With HideWhilePickingFromScreen, the editor hides while the pick is up
    // (IsPickingFromScreen drives that in the template), so it is not in the pick's frozen capture, and comes
    // back once the pick ends. Hiding rather than closing means a popup hosting the editor stays open, so an
    // edit session around it carries on.
    [TemplatePart(Name = nameof(PART_PickFromScreenButton), Type = typeof(ButtonBase))]
    public class ColorValueEditControl : Control
    {
        private bool m_isDoingUpdate = false;
        private CancellationTokenSource m_pickCancel;

        static ColorValueEditControl ()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ColorValueEditControl), new FrameworkPropertyMetadata(typeof(ColorValueEditControl)));
        }

        public ColorValueEditControl()
        {
            //this.Loaded += (e, a) => (this.GetFirstChildOf<TextBox>() as UIElement ?? this).Focus();
            this.Unloaded += this.OnUnloaded;
        }

        public override void OnApplyTemplate ()
        {
            if (this.PART_PickFromScreenButton != null)
            {
                this.PART_PickFromScreenButton.Click -= this.PickFromScreenButton_OnClick;
            }

            base.OnApplyTemplate();

            this.PART_PickFromScreenButton = this.GetTemplateChild(nameof(PART_PickFromScreenButton)) as ButtonBase;
            if (this.PART_PickFromScreenButton != null)
            {
                this.PART_PickFromScreenButton.Click += this.PickFromScreenButton_OnClick;
            }
        }

        private ButtonBase PART_PickFromScreenButton { get; set; }

        protected override void OnKeyUp (KeyEventArgs e)
        {
            if (!e.Handled && e.Key == Key.Escape)
            {
                this.UserRequestedFocusAway?.Invoke(this, EventArgs.Empty);
                return;
            }

            base.OnKeyUp(e);
        }

        public event EventHandler UserRequestedFocusAway;

        public static readonly DependencyProperty EditColorProperty = DPUtils.RegisterFP(_ => _.EditColor, (d,e)=>d.OnEditColorChanged(), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault);
        public Color EditColor
        {
            get => (Color)this.GetValue(EditColorProperty);
            set => this.SetValue(EditColorProperty, value);
        }

        public static readonly DependencyProperty PreferShortStringHexProperty = DPUtils.Register(_ => _.PreferShortStringHex);
        public bool PreferShortStringHex
        {
            get => (bool)this.GetValue(PreferShortStringHexProperty);
            set => this.SetValue(PreferShortStringHexProperty, value);
        }

        public static readonly DependencyProperty HexProperty = DPUtils.RegisterFP(_ => _.Hex, (d, e) => d.OnHexChanged(), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault);
        public string Hex
        {
            get => (string)this.GetValue(HexProperty);
            set => this.SetValue(HexProperty, value);
        }


        public static readonly DependencyProperty AProperty = DPUtils.RegisterFP(_ => _.A, (d, e) => d.OnARGBColorComponentChanged(), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault);
        public byte A
        {
            get => (byte)this.GetValue(AProperty);
            set => this.SetValue(AProperty, value);
        }


        public static readonly DependencyProperty RProperty = DPUtils.RegisterFP(_ => _.R, (d, e) => d.OnARGBColorComponentChanged(), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault);
        public byte R
        {
            get => (byte)this.GetValue(RProperty);
            set => this.SetValue(RProperty, value);
        }


        public static readonly DependencyProperty GProperty = DPUtils.RegisterFP(_ => _.G, (d, e) => d.OnARGBColorComponentChanged(), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault);
        public byte G
        {
            get => (byte)this.GetValue(GProperty);
            set => this.SetValue(GProperty, value);
        }


        public static readonly DependencyProperty BProperty = DPUtils.RegisterFP(_ => _.B, (d, e) => d.OnARGBColorComponentChanged(), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault);
        public byte B
        {
            get => (byte)this.GetValue(BProperty);
            set => this.SetValue(BProperty, value);
        }

        public static readonly DependencyProperty AllowOpacityEditProperty = DPUtils.Register(_ => _.AllowOpacityEdit, true);
        public bool AllowOpacityEdit
        {
            get => (bool)this.GetValue(AllowOpacityEditProperty);
            set => this.SetValue(AllowOpacityEditProperty, value);
        }


        public static readonly DependencyProperty IsReadOnlyProperty = DPUtils.Register(_ => _.IsReadOnly);
        public bool IsReadOnly
        {
            get => (bool)this.GetValue(IsReadOnlyProperty);
            set => this.SetValue(IsReadOnlyProperty, value);
        }


        public static readonly DependencyProperty BackingFillBrushProperty = DPUtils.Register(_ => _.BackingFillBrush);
        public Brush BackingFillBrush
        {
            get => (Brush)this.GetValue(BackingFillBrushProperty);
            set => this.SetValue(BackingFillBrushProperty, value);
        }

        public static readonly DependencyProperty TransparencyLinesBrushProperty = DPUtils.Register(_ => _.TransparencyLinesBrush);
        public Brush TransparencyLinesBrush
        {
            get => (Brush)this.GetValue(TransparencyLinesBrushProperty);
            set => this.SetValue(TransparencyLinesBrushProperty, value);
        }

        public static readonly DependencyProperty AllowPickFromScreenProperty = DPUtils.Register(_ => _.AllowPickFromScreen, true);
        /// <summary>Whether the eyedropper, which picks the color from anywhere on screen, is offered</summary>
        public bool AllowPickFromScreen
        {
            get => (bool)this.GetValue(AllowPickFromScreenProperty);
            set => this.SetValue(AllowPickFromScreenProperty, value);
        }

        public static readonly DependencyProperty HideWhilePickingFromScreenProperty = DPUtils.Register(_ => _.HideWhilePickingFromScreen);
        /// <summary>
        /// Whether the editor hides while the eyedropper's pick is up. The pick works from a frozen capture of the
        /// screen, so an editor left showing is in that capture, over whatever is behind it.
        /// </summary>
        public bool HideWhilePickingFromScreen
        {
            get => (bool)this.GetValue(HideWhilePickingFromScreenProperty);
            set => this.SetValue(HideWhilePickingFromScreenProperty, value);
        }

        public static readonly DependencyProperty ShowPickFromScreenMagnifierProperty = DPUtils.Register(_ => _.ShowPickFromScreenMagnifier, true);
        /// <summary>
        /// Whether a magnifier follows the cursor during an eyedropper pick, showing the color, its hex and the
        /// pixels under it
        /// </summary>
        public bool ShowPickFromScreenMagnifier
        {
            get => (bool)this.GetValue(ShowPickFromScreenMagnifierProperty);
            set => this.SetValue(ShowPickFromScreenMagnifierProperty, value);
        }

        public static readonly DependencyProperty PickFromScreenMagnifierPixelCountProperty = DPUtils.Register(_ => _.PickFromScreenMagnifierPixelCount, ScreenColorPickOptions.kDefaultMagnifierPixelCount);
        /// <summary>How many pixels across the eyedropper's magnifier shows, rounded up to odd so one sits at its center</summary>
        public int PickFromScreenMagnifierPixelCount
        {
            get => (int)this.GetValue(PickFromScreenMagnifierPixelCountProperty);
            set => this.SetValue(PickFromScreenMagnifierPixelCountProperty, value);
        }

        private static readonly DependencyPropertyKey IsPickingFromScreenPropertyKey = DPUtils.RegisterReadOnly(_ => _.IsPickingFromScreen);
        public static readonly DependencyProperty IsPickingFromScreenProperty = IsPickingFromScreenPropertyKey.DependencyProperty;
        /// <summary>
        /// True while the eyedropper's pick from screen is up. With <see cref="HideWhilePickingFromScreen"/> set, the
        /// default template hides the editor for it.
        /// </summary>
        public bool IsPickingFromScreen
        {
            get => (bool)this.GetValue(IsPickingFromScreenProperty);
            protected set => this.SetValue(IsPickingFromScreenPropertyKey, value);
        }

        private void OnUnloaded (object sender, RoutedEventArgs e)
        {
            // An editor taken away mid-pick takes the pick's overlay down with it
            m_pickCancel?.Cancel();
        }

        private async void PickFromScreenButton_OnClick (object sender, RoutedEventArgs e)
        {
            if (this.IsReadOnly || !this.AllowPickFromScreen || this.IsPickingFromScreen)
            {
                return;
            }

            m_pickCancel = new CancellationTokenSource();
            this.IsPickingFromScreen = true;
            try
            {
                // An editor hiding for the pick renders out of the way before the screen is captured
                if (this.HideWhilePickingFromScreen)
                {
                    await Dispatcher.Yield(DispatcherPriority.ContextIdle);
                }

                var options = new ScreenColorPickOptions
                {
                    Magnifier = this.ShowPickFromScreenMagnifier ? new ScreenColorPickMagnifier.Host() : null,
                    MagnifierPixelCount = this.PickFromScreenMagnifierPixelCount,
                };

                ColorArgb? picked = await ScreenColorPicker.PickAsync(options, m_pickCancel.Token);
                if (picked.HasValue)
                {
                    this.SetCurrentValue(EditColorProperty, picked.Value.ToMediaColor());
                }
            }
            catch (Exception exc)
            {
                Logger.LogError("[SCREEN-COLOR-PICK] Picking a color from the screen failed", exc);
            }
            finally
            {
                this.IsPickingFromScreen = false;
                m_pickCancel.Dispose();
                m_pickCancel = null;
            }
        }


        private void OnEditColorChanged ()
        {
            if (m_isDoingUpdate)
            {
                return;
            }

            try
            {
                m_isDoingUpdate = true;
                this.SetCurrentValue(HexProperty, CoerceUtils.GetSmallestHexString(this.EditColor));
                this.SetCurrentValue(AProperty, this.EditColor.A);
                this.SetCurrentValue(RProperty, this.EditColor.R);
                this.SetCurrentValue(GProperty, this.EditColor.G);
                this.SetCurrentValue(BProperty, this.EditColor.B);
            }
            finally
            {
                m_isDoingUpdate = false;
            }
        }

        private void OnHexChanged ()
        {
            if (m_isDoingUpdate)
            {
                return;
            }
            try
            {
                m_isDoingUpdate = true;
                if (CoerceUtils.TryGetColorFromString(this.Hex, out Color newColor))
                {
                    this.SetCurrentValue(EditColorProperty, newColor);
                    this.SetCurrentValue(AProperty, this.EditColor.A);
                    this.SetCurrentValue(RProperty, this.EditColor.R);
                    this.SetCurrentValue(GProperty, this.EditColor.G);
                    this.SetCurrentValue(BProperty, this.EditColor.B);
                }
            }
            finally
            {
                m_isDoingUpdate = false;
            }
            
        }

        private void OnARGBColorComponentChanged ()
        {
            if (m_isDoingUpdate)
            {
                return;
            }

            try
            {
                m_isDoingUpdate = true;
                this.SetCurrentValue(EditColorProperty, Color.FromArgb(this.A, this.R, this.G, this.B));
                this.SetCurrentValue(HexProperty, CoerceUtils.GetSmallestHexString(this.EditColor));
            }
            finally
            {
                m_isDoingUpdate = false;
            }
        }
    }
}
