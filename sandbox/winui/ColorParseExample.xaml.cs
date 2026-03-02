// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace AJutShowRoomWinUI
{
    using System;
    using AJut.UX;
    using AJut.UX.Helpers;
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;
    using Windows.UI;
    using DPUtils = AJut.UX.DPUtils<ColorParseExample>;


    public sealed partial class ColorParseExample : UserControl
    {
        public ColorParseExample ()
        {
            this.InitializeComponent();
            this.ParsedColor = Color.FromArgb(255, 12, 34, 56);
            this.ParsedColorText = AJutColorHelper.GetSmallestHexString(this.ParsedColor.A, this.ParsedColor.R, this.ParsedColor.G, this.ParsedColor.B);
        }

        public static readonly DependencyProperty ParsedColorProperty = DPUtils.Register(_ => _.ParsedColor);
        public Color ParsedColor
        {
            get => (Color)this.GetValue(ParsedColorProperty);
            set => this.SetValue(ParsedColorProperty, value);
        }


        public static readonly DependencyProperty ParsedColorTextProperty = DPUtils.Register(_ => _.ParsedColorText, (d,e)=>d.OnParsedColorTextChanged(e.NewValue));
        public string ParsedColorText
        {
            get => (string)this.GetValue(ParsedColorTextProperty);
            set => this.SetValue(ParsedColorTextProperty, value);
        }


        private void OnParsedColorTextChanged (string newValue)
        {
            if (CoerceUtils.TryGetColorFromString(newValue, out var color))
            {
                this.ParsedColor = color;
            }
        }

    }
}
