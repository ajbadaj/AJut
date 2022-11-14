namespace AJut.UX.Controls
{
    using System.Windows;
    using System.Windows.Media;
    using System.Windows.Controls;
    using AJut.UX;
    using DPUtils = AJut.UX.DPUtils<StackNavActiveDrawerPresenter>;

    /// <summary>
    /// The stack nav presenter that displays the drawer of the active <see cref="IStackNavDisplayControl"/>
    /// </summary>
    public class StackNavActiveDrawerPresenter : Control, IStackNavPresenter
    {
        static StackNavActiveDrawerPresenter ()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(StackNavActiveDrawerPresenter), new FrameworkPropertyMetadata(typeof(StackNavActiveDrawerPresenter)));
        }

        public StackNavActiveDrawerPresenter ()
        {
            this.SetupBasicNavigatorCommandBindings();
        }

        public static readonly DependencyProperty DrawerDisplayTemplateProperty = DPUtils.Register(_ => _.DrawerDisplayTemplate);
        public DataTemplate DrawerDisplayTemplate
        {
            get => (DataTemplate)this.GetValue(DrawerDisplayTemplateProperty);
            set => this.SetValue(DrawerDisplayTemplateProperty, value);
        }

        public static readonly DependencyProperty DrawerDisplayTemplateSelectorProperty = DPUtils.Register(_ => _.DrawerDisplayTemplateSelector);
        public DataTemplateSelector DrawerDisplayTemplateSelector
        {
            get => (DataTemplateSelector)this.GetValue(DrawerDisplayTemplateSelectorProperty);
            set => this.SetValue(DrawerDisplayTemplateSelectorProperty, value);
        }

        public static readonly DependencyProperty AdditionalBottomDisplayProperty = DPUtils.Register(_ => _.AdditionalBottomDisplay);
        public DependencyObject AdditionalBottomDisplay
        {
            get => (DependencyObject)this.GetValue(AdditionalBottomDisplayProperty);
            set => this.SetValue(AdditionalBottomDisplayProperty, value);
        }

        public static readonly DependencyProperty NavigatorProperty = DPUtils.Register(_ => _.Navigator);
        public StackNavFlowController Navigator
        {
            get => (StackNavFlowController)this.GetValue(NavigatorProperty);
            set => this.SetValue(NavigatorProperty, value);
        }


        public static readonly DependencyProperty DrawerSeparatorThicknessProperty = DPUtils.Register(_ => _.DrawerSeparatorThickness);
        public Thickness DrawerSeparatorThickness
        {
            get => (Thickness)this.GetValue(DrawerSeparatorThicknessProperty);
            set => this.SetValue(DrawerSeparatorThicknessProperty, value);
        }

        public static readonly DependencyProperty DrawerSeparatorFillProperty = DPUtils.Register(_ => _.DrawerSeparatorFill);
        public Brush DrawerSeparatorFill
        {
            get => (Brush)this.GetValue(DrawerSeparatorFillProperty);
            set => this.SetValue(DrawerSeparatorFillProperty, value);
        }

        public static readonly DependencyProperty DrawerSeparatorCornerRadiusProperty = DPUtils.Register(_ => _.DrawerSeparatorCornerRadius);
        public CornerRadius DrawerSeparatorCornerRadius
        {
            get => (CornerRadius)this.GetValue(DrawerSeparatorCornerRadiusProperty);
            set => this.SetValue(DrawerSeparatorCornerRadiusProperty, value);
        }

        public static readonly DependencyProperty HeaderPaddingProperty = DPUtils.Register(_ => _.HeaderPadding);
        public Thickness HeaderPadding
        {
            get => (Thickness)this.GetValue(HeaderPaddingProperty);
            set => this.SetValue(HeaderPaddingProperty, value);
        }

        public static readonly DependencyProperty HeaderTextFontSizeProperty = DPUtils.Register(_ => _.HeaderTextFontSize);
        public double HeaderTextFontSize
        {
            get => (double)this.GetValue(HeaderTextFontSizeProperty);
            set => this.SetValue(HeaderTextFontSizeProperty, value);
        }


        public static readonly DependencyProperty InsetBorderBrushProperty = DPUtils.Register(_ => _.InsetBorderBrush);
        public Brush InsetBorderBrush
        {
            get => (Brush)this.GetValue(InsetBorderBrushProperty);
            set => this.SetValue(InsetBorderBrushProperty, value);
        }

        public static readonly DependencyProperty InsetBorderThicknessProperty = DPUtils.Register(_ => _.InsetBorderThickness);
        public Thickness InsetBorderThickness
        {
            get => (Thickness)this.GetValue(InsetBorderThicknessProperty);
            set => this.SetValue(InsetBorderThicknessProperty, value);
        }

        public static readonly DependencyProperty InsetBorderCornerRadiusProperty = DPUtils.Register(_ => _.InsetBorderCornerRadius);
        public CornerRadius InsetBorderCornerRadius
        {
            get => (CornerRadius)this.GetValue(InsetBorderCornerRadiusProperty);
            set => this.SetValue(InsetBorderCornerRadiusProperty, value);
        }
    }
}
