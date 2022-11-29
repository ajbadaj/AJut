namespace AJut.UX.Controls
{
    using System;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Input;
    using System.Windows.Media;
    using AJut.UX.AttachedProperties;
    using DPUtils = DPUtils<ColorEditIngressControl>;

    public class ColorEditIngressControl : Control, IUserEditNotifier
    {
        private Color? m_editCache;
        static ColorEditIngressControl()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ColorEditIngressControl), new FrameworkPropertyMetadata(typeof(ColorEditIngressControl)));
        }
        
        public ColorEditIngressControl()
        {
            this.Focusable = true;
            ClickableXTA.SetIsTrackingClick(this, true);
            ClickableXTA.AddClickHandler(this, (e, a) => this.ShowEditDisplay = !this.ShowEditDisplay);
        }

        protected override void OnKeyUp (KeyEventArgs e)
        {
            if (e.Key == Key.Return)
            {
                this.ShowEditDisplay = true;
            }
            else
            {
                base.OnKeyUp(e);
            }
        }

        /// <summary>
        /// An event that signifies a user edit has completed - this is slightly different than bound or otherwise modified <see cref="EditColor"/> changes in that this
        /// event signifies: an edit initiation, a single or even several changes, and a completion have all occurred - not just a change. Changes made outside user edit
        /// similarly do not notify via this event.
        /// </summary>
        public event EventHandler<UserEditAppliedEventArgs> UserEditComplete;

        public static readonly DependencyProperty EditColorProperty = DPUtils.Register(_ => _.EditColor);
        public Color EditColor
        {
            get => (Color)this.GetValue(EditColorProperty);
            set => this.SetValue(EditColorProperty, value);
        }

        public static readonly DependencyProperty HighlightBorderBrushProperty = DPUtils.Register(_ => _.HighlightBorderBrush);
        public Brush HighlightBorderBrush
        {
            get => (Brush)this.GetValue(HighlightBorderBrushProperty);
            set => this.SetValue(HighlightBorderBrushProperty, value);
        }

        public static readonly DependencyProperty PreferShortStringHexProperty = DPUtils.Register(_ => _.PreferShortStringHex);
        public bool PreferShortStringHex
        {
            get => (bool)this.GetValue(PreferShortStringHexProperty);
            set => this.SetValue(PreferShortStringHexProperty, value);
        }

        public static readonly DependencyProperty IsReadOnlyProperty = DPUtils.Register(_ => _.IsReadOnly);
        public bool IsReadOnly
        {
            get => (bool)this.GetValue(IsReadOnlyProperty);
            set => this.SetValue(IsReadOnlyProperty, value);
        }

        public static readonly DependencyProperty CornerBannerDimensionsProperty = DPUtils.Register(_ => _.CornerBannerDimensions, 32.0);
        public double CornerBannerDimensions
        {
            get => (double)this.GetValue(CornerBannerDimensionsProperty);
            set => this.SetValue(CornerBannerDimensionsProperty, value);
        }

        public static readonly DependencyProperty CornerBannerIconFontSizeProperty = DPUtils.Register(_ => _.CornerBannerIconFontSize, 12.0);
        public double CornerBannerIconFontSize
        {
            get => (double)this.GetValue(CornerBannerIconFontSizeProperty);
            set => this.SetValue(CornerBannerIconFontSizeProperty, value);
        }

        public static readonly DependencyProperty PopupWidthProperty = DPUtils.Register(_ => _.PopupWidth, 235.0);
        public double PopupWidth
        {
            get => (double)this.GetValue(PopupWidthProperty);
            set => this.SetValue(PopupWidthProperty, value);
        }

        public static readonly DependencyProperty ShowEditDisplayProperty = DPUtils.Register(_ => _.ShowEditDisplay, false, (d,e)=>d.OnShowEditDisplayChanged(e));
        public bool ShowEditDisplay
        {
            get => (bool)this.GetValue(ShowEditDisplayProperty);
            set => this.SetValue(ShowEditDisplayProperty, value);
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


        private void OnShowEditDisplayChanged (DependencyPropertyChangedEventArgs<bool> e)
        {
            if (e.NewValue)
            {
                m_editCache = this.EditColor;
            }
            else
            {
                if (!(m_editCache?.Equals(this.EditColor) ?? false))
                {
                    this.UserEditComplete?.Invoke(this, new UserEditAppliedEventArgs(m_editCache.Value, this.EditColor));

                }

                m_editCache = null;

            }

        }

    }
}
