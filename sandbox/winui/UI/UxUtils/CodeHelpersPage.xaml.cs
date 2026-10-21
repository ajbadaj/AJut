namespace AJutShowRoomWinUI.UI.UxUtils
{
    using System;
    using System.Linq;
    using AJut.UX;
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;
    using Microsoft.UI.Xaml.Media;
    using Windows.UI;
    using DPUtils = AJut.UX.DPUtils<CodeHelpersPage>;

    /// <summary>
    /// Live examples of the C# side helpers in AJut.UX. Every bound property on this page is a DPUtils registered
    /// dependency property, so the DPUtils section shows the page's own code.
    /// </summary>
    public sealed partial class CodeHelpersPage : Page
    {
        private const string kStartingBaseColor = "#3A7BD5";

        public CodeHelpersPage ()
        {
            this.InitializeComponent();
            this.RecomputeColorMath(kStartingBaseColor);
            this.ApplyCoercion("4,1", "12,4");
        }

        // ===========[ Dependency Properties ]========================================
        public static readonly DependencyProperty TreeOutputProperty = DPUtils.Register(_ => _.TreeOutput, string.Empty);
        public string TreeOutput
        {
            get => (string)this.GetValue(TreeOutputProperty);
            set => this.SetValue(TreeOutputProperty, value);
        }

        public static readonly DependencyProperty PreviewThicknessProperty = DPUtils.Register(_ => _.PreviewThickness, new Thickness(1));
        public Thickness PreviewThickness
        {
            get => (Thickness)this.GetValue(PreviewThicknessProperty);
            set => this.SetValue(PreviewThicknessProperty, value);
        }

        public static readonly DependencyProperty PreviewCornerRadiusProperty = DPUtils.Register(_ => _.PreviewCornerRadius, new CornerRadius(0));
        public CornerRadius PreviewCornerRadius
        {
            get => (CornerRadius)this.GetValue(PreviewCornerRadiusProperty);
            set => this.SetValue(PreviewCornerRadiusProperty, value);
        }

        public static readonly DependencyProperty CoercionOutputProperty = DPUtils.Register(_ => _.CoercionOutput, string.Empty);
        public string CoercionOutput
        {
            get => (string)this.GetValue(CoercionOutputProperty);
            set => this.SetValue(CoercionOutputProperty, value);
        }

        /// <summary>
        /// How far to move the base color's HSL lightness. Positive lightens, negative darkens.
        /// </summary>
        public static readonly DependencyProperty LightnessDeltaProperty = DPUtils.Register(_ => _.LightnessDelta, 0.0, (d, e) => d.RecomputeColorMathFromTextBox());
        public double LightnessDelta
        {
            get => (double)this.GetValue(LightnessDeltaProperty);
            set => this.SetValue(LightnessDeltaProperty, value);
        }

        public static readonly DependencyProperty BaseBrushProperty = DPUtils.Register(_ => _.BaseBrush);
        public Brush? BaseBrush
        {
            get => (Brush?)this.GetValue(BaseBrushProperty);
            set => this.SetValue(BaseBrushProperty, value);
        }

        public static readonly DependencyProperty BaseForegroundBrushProperty = DPUtils.Register(_ => _.BaseForegroundBrush);
        public Brush? BaseForegroundBrush
        {
            get => (Brush?)this.GetValue(BaseForegroundBrushProperty);
            set => this.SetValue(BaseForegroundBrushProperty, value);
        }

        public static readonly DependencyProperty AdjustedBrushProperty = DPUtils.Register(_ => _.AdjustedBrush);
        public Brush? AdjustedBrush
        {
            get => (Brush?)this.GetValue(AdjustedBrushProperty);
            set => this.SetValue(AdjustedBrushProperty, value);
        }

        public static readonly DependencyProperty AdjustedForegroundBrushProperty = DPUtils.Register(_ => _.AdjustedForegroundBrush);
        public Brush? AdjustedForegroundBrush
        {
            get => (Brush?)this.GetValue(AdjustedForegroundBrushProperty);
            set => this.SetValue(AdjustedForegroundBrushProperty, value);
        }

        public static readonly DependencyProperty ColorMathReportProperty = DPUtils.Register(_ => _.ColorMathReport, string.Empty);
        public string ColorMathReport
        {
            get => (string)this.GetValue(ColorMathReportProperty);
            set => this.SetValue(ColorMathReportProperty, value);
        }

        // ===========[ Properties ]===================================================
        public string DpUtilsCode { get; } = @"// The alias names the owner once, above the class
using DPUtils = AJut.UX.DPUtils<CodeHelpersPage>;

public sealed partial class CodeHelpersPage : Page
{
    // Registration on one line: the lambda supplies the name and the type, no strings, no typeof
    public static readonly DependencyProperty TreeOutputProperty = DPUtils.Register(_ => _.TreeOutput, string.Empty);
    public string TreeOutput
    {
        get => (string)this.GetValue(TreeOutputProperty);
        set => this.SetValue(TreeOutputProperty, value);
    }

    // With a default and a typed change handler: d is the page, e.NewValue is already a double
    public static readonly DependencyProperty LightnessDeltaProperty = DPUtils.Register(_ => _.LightnessDelta, 0.0, (d, e) => d.RecomputeColorMathFromTextBox());
    public double LightnessDelta
    {
        get => (double)this.GetValue(LightnessDeltaProperty);
        set => this.SetValue(LightnessDeltaProperty, value);
    }

    // No default: a reference type starts null
    public static readonly DependencyProperty BaseBrushProperty = DPUtils.Register(_ => _.BaseBrush);
    public Brush? BaseBrush
    {
        get => (Brush?)this.GetValue(BaseBrushProperty);
        set => this.SetValue(BaseBrushProperty, value);
    }
}";

        public string TreeWalkCode { get; } = @"using AJut.UX; // DependencyObjectXT lives here

private void WalkTree_OnClick (object sender, RoutedEventArgs e)
{
    // Down from the page: the first TextBox anywhere beneath it
    TextBox? firstTextBox = this.GetFirstChildOf<TextBox>();

    // Up from the button: the nearest ScrollViewer above it
    ScrollViewer? scroller = this.WalkTreeButton.GetFirstParentOf<ScrollViewer>();

    // Every Panel between the button and the root
    int panelsAbove = this.WalkTreeButton.SearchParentsOf<Panel>().Count();

    // The button's own visual children, straight from VisualTreeHelper
    int directChildren = this.WalkTreeButton.GetVisualChildren().Count();

    this.TreeOutput =
        $""First TextBox below the page: {firstTextBox?.Name}\n"" +
        $""Nearest ScrollViewer above the button: {(scroller != null ? ""found"" : ""none"")}\n"" +
        $""Panels above the button: {panelsAbove}, visual children of the button: {directChildren}"";
}";

        public string CoerceCode { get; } = @"// 1, 2 or 4 numbers, commas or spaces, into a Thickness or a CornerRadius
Thickness? thickness = CoerceUtils.CoerceThicknessFrom(""4,1"");
CornerRadius? radius = CoerceUtils.CoerceCornerRadiusFrom(""12,4"");

// Hex or a named color into a Color, without throwing on junk
if (CoerceUtils.TryGetColorFromString(""#3A7BD5"", out Color color)) { ... }

// Anything brush-like into a Brush: a Brush, a Color, ""#F0F"", ""linear-gradient(90deg, #A22, #00F)""
Brush brush = CoerceUtils.CoerceBrushFrom(""radial-gradient(circle at 25% 90%, #FF0000, #0000FF)"");

// A relative path into an ms-appx image source
ImageSource? icon = CoerceUtils.CoerceImageSourceFrom(""Assets/app.ico"");

// The CallbackFor variants fit a DP's coercion slot as they are
public static readonly DependencyProperty AccentProperty = DPUtils.Register(_ => _.Accent, new PropertyMetadata(null, CoerceUtils.CallbackForBrush));

// The thickness and corner radius coercions throw FormatException on text they cannot read, so a UI path catches it
try
{
    this.PreviewThickness = CoerceUtils.CoerceThicknessFrom(this.ThicknessText.Text) ?? new Thickness(1);
}
catch (FormatException ex)
{
    this.CoercionOutput = ex.Message;
}";

        public string ColorMathCode { get; } = @"using AJut.UX; // ColorMathExtensions: the WinUI3 Color overloads

private void RecomputeColorMath (string baseColorText)
{
    if (!CoerceUtils.TryGetColorFromString(baseColorText, out Color baseColor))
    {
        this.ColorMathReport = $""Could not read \""{baseColorText}\"" as a color"";
        return;
    }

    // Lighten and Darken move HSL lightness by an amount in [0, 1]
    double delta = this.LightnessDelta;
    Color adjusted = delta >= 0.0 ? baseColor.Lighten(delta) : baseColor.Darken(-delta);

    // A foreground that reads on it, from the WCAG crossover
    Color foreground = adjusted.PickReadableForeground();

    this.BaseBrush = new SolidColorBrush(baseColor);
    this.AdjustedBrush = new SolidColorBrush(adjusted);
    this.AdjustedForegroundBrush = new SolidColorBrush(foreground);
    this.ColorMathReport =
        $""{adjusted.GetSmallestHexString()}: luminance {adjusted.GetRelativeLuminance():0.000}, "" +
        $""{(adjusted.IsDark() ? ""dark"" : ""light"")}, contrast with its foreground {adjusted.GetContrastRatio(foreground):0.00}:1"";
}

// The same math with no framework in it, on ColorArgb bytes, for code shared between WPF and WinUI3
ColorArgb shared = AJutColorHelper.Lighten(new ColorArgb(255, 0x3A, 0x7B, 0xD5), 0.2);";

        // ===========[ Event handlers ]===============================================
        private void WalkTree_OnClick (object sender, RoutedEventArgs e)
        {
            TextBox? firstTextBox = this.GetFirstChildOf<TextBox>();
            ScrollViewer? scroller = this.WalkTreeButton.GetFirstParentOf<ScrollViewer>();
            int panelsAbove = this.WalkTreeButton.SearchParentsOf<Panel>().Count();
            int directChildren = this.WalkTreeButton.GetVisualChildren().Count();

            this.TreeOutput =
                $"First TextBox below the page: {(firstTextBox != null ? firstTextBox.Name : "none")}\n" +
                $"Nearest ScrollViewer above the button: {(scroller != null ? "found" : "none")}\n" +
                $"Panels above the button: {panelsAbove}, visual children of the button: {directChildren}";
        }

        private void ApplyCoercion_OnClick (object sender, RoutedEventArgs e)
        {
            this.ApplyCoercion(this.ThicknessText.Text, this.CornerRadiusText.Text);
        }

        private void BaseColorText_OnTextChanged (object sender, TextChangedEventArgs e)
        {
            this.RecomputeColorMathFromTextBox();
        }

        // ===========[ Helpers ]======================================================
        private void ApplyCoercion (string thicknessText, string cornerRadiusText)
        {
            try
            {
                this.PreviewThickness = CoerceUtils.CoerceThicknessFrom(thicknessText) ?? new Thickness(1);
                this.PreviewCornerRadius = CoerceUtils.CoerceCornerRadiusFrom(cornerRadiusText) ?? new CornerRadius(0);
                this.CoercionOutput =
                    $"Thickness {this.PreviewThickness.Left},{this.PreviewThickness.Top},{this.PreviewThickness.Right},{this.PreviewThickness.Bottom} and " +
                    $"corner radius {this.PreviewCornerRadius.TopLeft},{this.PreviewCornerRadius.TopRight},{this.PreviewCornerRadius.BottomRight},{this.PreviewCornerRadius.BottomLeft}";
            }
            catch (FormatException ex)
            {
                this.CoercionOutput = ex.Message;
            }
        }

        // The slider can push its value before InitializeComponent has wired the named text box, so this reads
        // the starting color until the box exists
        private void RecomputeColorMathFromTextBox ()
        {
            this.RecomputeColorMath(this.BaseColorText?.Text ?? kStartingBaseColor);
        }

        private void RecomputeColorMath (string baseColorText)
        {
            if (!CoerceUtils.TryGetColorFromString(baseColorText, out Color baseColor))
            {
                this.ColorMathReport = $"Could not read \"{baseColorText}\" as a color";
                return;
            }

            double delta = this.LightnessDelta;
            Color adjusted = delta >= 0.0 ? baseColor.Lighten(delta) : baseColor.Darken(-delta);
            Color baseForeground = baseColor.PickReadableForeground();
            Color adjustedForeground = adjusted.PickReadableForeground();

            this.BaseBrush = new SolidColorBrush(baseColor);
            this.BaseForegroundBrush = new SolidColorBrush(baseForeground);
            this.AdjustedBrush = new SolidColorBrush(adjusted);
            this.AdjustedForegroundBrush = new SolidColorBrush(adjustedForeground);
            this.ColorMathReport =
                $"{adjusted.GetSmallestHexString()}: luminance {adjusted.GetRelativeLuminance():0.000}, " +
                $"{(adjusted.IsDark() ? "dark" : "light")}, contrast with its foreground {adjusted.GetContrastRatio(adjustedForeground):0.00}:1";
        }
    }
}
