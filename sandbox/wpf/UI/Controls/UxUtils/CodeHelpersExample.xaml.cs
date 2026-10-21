namespace TheAJutShowRoom.UI.Controls.UxUtils
{
    using System;
    using System.Linq;
    using System.Text;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Media;
    using AJut.UX;
    using DPUtils = AJut.UX.DPUtils<CodeHelpersExample>;

    public partial class CodeHelpersExample : UserControl
    {
        public CodeHelpersExample ()
        {
            this.InitializeComponent();
        }

        // ===========[ Dependency Properties ]===============================================

        public static readonly DependencyProperty TreeProbeResultProperty = DPUtils.Register(_ => _.TreeProbeResult, "Press a button above to run a tree walk.");
        public string TreeProbeResult
        {
            get => (string)this.GetValue(TreeProbeResultProperty);
            set => this.SetValue(TreeProbeResultProperty, value);
        }

        public static readonly DependencyProperty CoercedBrushProperty = DPUtils.Register(_ => _.CoercedBrush);
        public Brush? CoercedBrush
        {
            get => (Brush?)this.GetValue(CoercedBrushProperty);
            set => this.SetValue(CoercedBrushProperty, value);
        }

        public static readonly DependencyProperty CoercedThicknessProperty = DPUtils.Register(_ => _.CoercedThickness, new Thickness(1));
        public Thickness CoercedThickness
        {
            get => (Thickness)this.GetValue(CoercedThicknessProperty);
            set => this.SetValue(CoercedThicknessProperty, value);
        }

        public static readonly DependencyProperty CoercedThicknessReportProperty = DPUtils.Register(_ => _.CoercedThicknessReport, "BorderThickness: 1");
        public string CoercedThicknessReport
        {
            get => (string)this.GetValue(CoercedThicknessReportProperty);
            set => this.SetValue(CoercedThicknessReportProperty, value);
        }

        public static readonly DependencyProperty AnalyzedBrushProperty = DPUtils.Register(_ => _.AnalyzedBrush);
        public Brush? AnalyzedBrush
        {
            get => (Brush?)this.GetValue(AnalyzedBrushProperty);
            set => this.SetValue(AnalyzedBrushProperty, value);
        }

        public static readonly DependencyProperty ReadableForegroundBrushProperty = DPUtils.Register(_ => _.ReadableForegroundBrush);
        public Brush? ReadableForegroundBrush
        {
            get => (Brush?)this.GetValue(ReadableForegroundBrushProperty);
            set => this.SetValue(ReadableForegroundBrushProperty, value);
        }

        public static readonly DependencyProperty LightenedBrushProperty = DPUtils.Register(_ => _.LightenedBrush);
        public Brush? LightenedBrush
        {
            get => (Brush?)this.GetValue(LightenedBrushProperty);
            set => this.SetValue(LightenedBrushProperty, value);
        }

        public static readonly DependencyProperty DarkenedBrushProperty = DPUtils.Register(_ => _.DarkenedBrush);
        public Brush? DarkenedBrush
        {
            get => (Brush?)this.GetValue(DarkenedBrushProperty);
            set => this.SetValue(DarkenedBrushProperty, value);
        }

        public static readonly DependencyProperty ColorReportProperty = DPUtils.Register(_ => _.ColorReport, "Press Analyze to run the color math.");
        public string ColorReport
        {
            get => (string)this.GetValue(ColorReportProperty);
            set => this.SetValue(ColorReportProperty, value);
        }

        // ===========[ Code Text ]===========================================================

        public string DPUtilsCode { get; } =
@"using DPUtils = AJut.UX.DPUtils<CodeHelpersExample>;

public partial class CodeHelpersExample : UserControl
{
    // Registration on one line, wrapper expanded: the name, owner and type all come from the lambda
    public static readonly DependencyProperty TreeProbeResultProperty = DPUtils.Register(_ => _.TreeProbeResult, ""Press a button above to run a tree walk."");
    public string TreeProbeResult
    {
        get => (string)this.GetValue(TreeProbeResultProperty);
        set => this.SetValue(TreeProbeResultProperty, value);
    }

    // Other shapes, all from the same helper:
    //   DPUtils.Register(_ => _.Value, (d, e) => d.OnValueChanged(e))                        change callback, typed args
    //   DPUtils.RegisterFP(_ => _.Value, 0.0f, (d, e) => ..., (d, v) => ..., FrameworkPropertyMetadataOptions.BindsTwoWayByDefault)
    //   DPUtils.RegisterReadOnly(_ => _.FinalText)                                           returns the DependencyPropertyKey
}";

        public string APUtilsCode { get; } =
@"public static class BorderXTA
{
    private static readonly APUtilsRegistrationHelper APUtils = new APUtilsRegistrationHelper(typeof(BorderXTA));

    public static DependencyProperty CornerRadiusProperty = APUtils.Register(GetCornerRadius, SetCornerRadius);
    public static CornerRadius GetCornerRadius (DependencyObject obj) => (CornerRadius)obj.GetValue(CornerRadiusProperty);
    public static void SetCornerRadius (DependencyObject obj, CornerRadius value) => obj.SetValue(CornerRadiusProperty, value);
}

// Read-only attached properties use RegisterReadOnly and expose only the getter:
//   private static DependencyPropertyKey IsPressedPropertyKey = APUtils.RegisterReadOnly(GetIsPressed, SetIsPressed);
//   public static DependencyProperty IsPressedProperty = IsPressedPropertyKey.DependencyProperty;";

        public string TreeWalkCode { get; } =
@"using AJut.UX;   // the extension methods live in DependencyObjectXT

// Walk up the visual tree to the nearest ScrollViewer
ScrollViewer? scroller = button.GetFirstParentOf<ScrollViewer>();

// Walk down the visual tree to the first TextBlock
TextBlock? first = this.GetFirstChildOf<TextBlock>();

// Every parent, nearest first; eTraversalTree.Logical or .Both change which tree is walked
IEnumerable<FrameworkElement> parents = button.SearchParentsOf<FrameworkElement>(eTraversalTree.Visual);";

        public string CoerceCode { get; } =
@"Brush? brush = CoerceUtils.CoerceBrushFrom(""radial-gradient(circle at 30% 30%, #FFF, #246)"");
Thickness? thickness = CoerceUtils.CoerceThicknessFrom(""8,2"");   // 1, 2 or 4 numbers, comma or space separated
Color color = CoerceUtils.CoerceColorFrom(""#2A6F97"");

// As a dependency property coercion callback:
public static readonly DependencyProperty FillProperty = DPUtils.RegisterFP(
    _ => _.Fill, null, (d, e) => d.OnFillChanged(), (d, v) => CoerceUtils.CoerceBrushFrom(v)
);";

        public string ColorMathCode { get; } =
@"using AJut.UX;   // ColorMathExtensions on System.Windows.Media.Color

Color color = CoerceUtils.CoerceColorFrom(""#2A6F97"");
double luminance = color.GetRelativeLuminance();   // 0 (black) to 1 (white)
bool isDark = color.IsDark();
Color readable = color.PickReadableForeground();   // near-white on dark, near-black on light
Color lighter = color.Lighten(0.2);
Color darker = color.Darken(0.2);
double contrast = color.GetContrastRatio(readable);

// The same math without WPF types, from AJut.UX:
ColorArgb argb = color.ToColorArgb();
HslColor hsl = AJutColorHelper.RgbToHsl(argb);";

        // ===========[ Handlers ]============================================================

        private void FindParentScroller_OnClick (object sender, RoutedEventArgs e)
        {
            ScrollViewer? scroller = ((DependencyObject)sender).GetFirstParentOf<ScrollViewer>();
            this.TreeProbeResult = scroller == null
                ? "GetFirstParentOf<ScrollViewer>() found nothing."
                : $"GetFirstParentOf<ScrollViewer>() found a {scroller.GetType().Name} named '{scroller.Name}' with viewport {scroller.ViewportWidth:0}x{scroller.ViewportHeight:0}.";
        }

        private void FindFirstTextBlock_OnClick (object sender, RoutedEventArgs e)
        {
            TextBlock? first = this.GetFirstChildOf<TextBlock>();
            this.TreeProbeResult = first == null
                ? "GetFirstChildOf<TextBlock>() found nothing."
                : $"GetFirstChildOf<TextBlock>() found the TextBlock reading: \"{first.Text}\"";
        }

        private void ListParents_OnClick (object sender, RoutedEventArgs e)
        {
            var report = new StringBuilder();
            int depth = 0;
            foreach (FrameworkElement parent in ((DependencyObject)sender).SearchParentsOf<FrameworkElement>())
            {
                string name = String.IsNullOrEmpty(parent.Name) ? "" : $" '{parent.Name}'";
                report.AppendLine($"{depth++,3}: {parent.GetType().Name}{name}");
            }

            this.TreeProbeResult = report.ToString();
        }

        private void CoerceBrush_OnClick (object sender, RoutedEventArgs e)
        {
            this.CoercedBrush = CoerceUtils.CoerceBrushFrom(this.BrushInput.Text);
        }

        private void CoerceThickness_OnClick (object sender, RoutedEventArgs e)
        {
            // CoerceThicknessFrom throws on text it cannot parse, so the demo reports that rather than letting it escape
            try
            {
                Thickness? thickness = CoerceUtils.CoerceThicknessFrom(this.ThicknessInput.Text);
                if (thickness.HasValue)
                {
                    this.CoercedThickness = thickness.Value;
                    this.CoercedThicknessReport = $"BorderThickness: {thickness.Value}";
                }
                else
                {
                    this.CoercedThicknessReport = "No thickness came back.";
                }
            }
            catch (Exception ex)
            {
                this.CoercedThicknessReport = ex.Message;
            }
        }

        private void AnalyzeColor_OnClick (object sender, RoutedEventArgs e)
        {
            if (!CoerceUtils.TryGetColorFromString(this.ColorInput.Text, out Color color))
            {
                this.ColorReport = $"'{this.ColorInput.Text}' is not a color.";
                return;
            }

            Color readable = color.PickReadableForeground();
            this.AnalyzedBrush = new SolidColorBrush(color);
            this.ReadableForegroundBrush = new SolidColorBrush(readable);
            this.LightenedBrush = new SolidColorBrush(color.Lighten(0.2));
            this.DarkenedBrush = new SolidColorBrush(color.Darken(0.2));
            this.ColorReport = $"luminance {color.GetRelativeLuminance():0.000}, IsDark {color.IsDark()}, readable foreground {readable.GetSmallestHexString()}, contrast {color.GetContrastRatio(readable):0.00}:1";
        }
    }
}
