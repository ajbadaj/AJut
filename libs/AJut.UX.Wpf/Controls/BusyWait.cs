namespace AJut.UX.Controls
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Controls;
    using DPUtils = AJut.UX.DPUtils<BusyWait>;

    public class BusyWait : ContentControl
    {
        static BusyWait ()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(BusyWait), new FrameworkPropertyMetadata(typeof(BusyWait)));
        }

        public static readonly DependencyProperty IsSpinningProperty = DPUtils.Register(_ => _.IsSpinning, true);
        public bool IsSpinning
        {
            get => (bool)this.GetValue(IsSpinningProperty);
            set => this.SetValue(IsSpinningProperty, value);
        }

        public static readonly DependencyProperty SpinnerDockProperty = DPUtils.Register(_ => _.SpinnerDock);
        public Dock SpinnerDock
        {
            get => (Dock)this.GetValue(SpinnerDockProperty);
            set => this.SetValue(SpinnerDockProperty, value);
        }

        public static readonly DependencyProperty SpinnerFontSizeProperty = DPUtils.Register(_ => _.SpinnerFontSize);
        public double SpinnerFontSize
        {
            get => (double)this.GetValue(SpinnerFontSizeProperty);
            set => this.SetValue(SpinnerFontSizeProperty, value);
        }

        public static readonly DependencyProperty SpinnerGlyphProperty = DPUtils.Register(_ => _.SpinnerGlyph);
        public string SpinnerGlyph
        {
            get => (string)this.GetValue(SpinnerGlyphProperty);
            set => this.SetValue(SpinnerGlyphProperty, value);
        }

        public static readonly DependencyProperty ContentPaddingProperty = DPUtils.Register(_ => _.ContentPadding);
        public Thickness ContentPadding
        {
            get => (Thickness)this.GetValue(ContentPaddingProperty);
            set => this.SetValue(ContentPaddingProperty, value);
        }
    }
}
