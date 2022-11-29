namespace AJut.UX.Controls
{
    using System;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Input;
    using System.Windows.Media;
    using DPUtils = DPUtils<ColorValueEditControl>;

    public class ColorValueEditControl : Control
    {
        private bool m_isDoingUpdate = false;
        static ColorValueEditControl ()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ColorValueEditControl), new FrameworkPropertyMetadata(typeof(ColorValueEditControl)));
        }

        public ColorValueEditControl()
        {
            //this.Loaded += (e, a) => (this.GetFirstChildOf<TextBox>() as UIElement ?? this).Focus();
        }

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


        private void OnEditColorChanged ()
        {
            if (m_isDoingUpdate)
            {
                return;
            }

            try
            {
                m_isDoingUpdate = true;
                this.SetCurrentValue(HexProperty, ColorHelper.GetSmallestHexString(this.EditColor));
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
                if (ColorHelper.TryGetColorFromHex(this.Hex, out Color newColor))
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
                this.SetCurrentValue(HexProperty, ColorHelper.GetSmallestHexString(this.EditColor));
            }
            finally
            {
                m_isDoingUpdate = false;
            }
        }
    }
}
