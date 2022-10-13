namespace AJut.UX.Controls
{
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Media;
    using AJut.UX;
    using DPUtils = AJut.UX.DPUtils<StackNavActiveHeaderPresenter>;

    /// <summary>
    /// The stack nav presenter that displays the header of the active <see cref="IStackNavDisplayControl"/>, and the default navigational commands
    /// </summary>
    public class StackNavActiveHeaderPresenter : Control, IStackNavPresenter
    {
        static StackNavActiveHeaderPresenter ()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(StackNavActiveHeaderPresenter), new FrameworkPropertyMetadata(typeof(StackNavActiveHeaderPresenter)));
        }

        public StackNavActiveHeaderPresenter ()
        {
            this.SetupBasicNavigatorCommandBindings();
        }

        public static readonly DependencyProperty NavigatorProperty = DPUtils.Register(_ => _.Navigator);
        public StackNavFlowController Navigator
        {
            get => (StackNavFlowController)this.GetValue(NavigatorProperty);
            set => this.SetValue(NavigatorProperty, value);
        }

        public static readonly DependencyProperty ShowDrawerButtonProperty = DPUtils.Register(_ => _.ShowDrawerButton);
        public bool ShowDrawerButton
        {
            get => (bool)this.GetValue(ShowDrawerButtonProperty);
            set => this.SetValue(ShowDrawerButtonProperty, value);
        }

        public static readonly DependencyProperty AdditionalLeftSideDisplayProperty = DPUtils.Register(_ => _.AdditionalLeftSideDisplay);
        public object AdditionalLeftSideDisplay
        {
            get => this.GetValue(AdditionalLeftSideDisplayProperty);
            set => this.SetValue(AdditionalLeftSideDisplayProperty, value);
        }

        public static readonly DependencyProperty AdditionalRightSideDisplayProperty = DPUtils.Register(_ => _.AdditionalRightSideDisplay);
        public object AdditionalRightSideDisplay
        {
            get => this.GetValue(AdditionalRightSideDisplayProperty);
            set => this.SetValue(AdditionalRightSideDisplayProperty, value);
        }


        public static readonly DependencyProperty NavButtonForegroundProperty = DPUtils.Register(_ => _.NavButtonForeground);
        public Brush NavButtonForeground
        {
            get => (Brush)this.GetValue(NavButtonForegroundProperty);
            set => this.SetValue(NavButtonForegroundProperty, value);
        }


        public static readonly DependencyProperty NavButtonForegroundHighlightProperty = DPUtils.Register(_ => _.NavButtonForegroundHighlight);
        public Brush NavButtonForegroundHighlight
        {
            get => (Brush)this.GetValue(NavButtonForegroundHighlightProperty);
            set => this.SetValue(NavButtonForegroundHighlightProperty, value);
        }

        public static readonly DependencyProperty NavButtonBackgroundHoverProperty = DPUtils.Register(_ => _.NavButtonBackgroundHover);
        public Brush NavButtonBackgroundHover
        {
            get => (Brush)this.GetValue(NavButtonBackgroundHoverProperty);
            set => this.SetValue(NavButtonBackgroundHoverProperty, value);
        }

        public static readonly DependencyProperty NavButtonBackgroundPressedProperty = DPUtils.Register(_ => _.NavButtonBackgroundPressed);
        public Brush NavButtonBackgroundPressed
        {
            get => (Brush)this.GetValue(NavButtonBackgroundPressedProperty);
            set => this.SetValue(NavButtonBackgroundPressedProperty, value);
        }



        public static readonly DependencyProperty TitleTemplateProperty = DPUtils.Register(_ => _.TitleTemplate);
        public DataTemplate TitleTemplate
        {
            get => (DataTemplate)this.GetValue(TitleTemplateProperty);
            set => this.SetValue(TitleTemplateProperty, value);
        }

        public static readonly DependencyProperty TitleTemplateSelectorProperty = DPUtils.Register(_ => _.TitleTemplateSelector);
        public DataTemplateSelector TitleTemplateSelector
        {
            get => (DataTemplateSelector)this.GetValue(TitleTemplateSelectorProperty);
            set => this.SetValue(TitleTemplateSelectorProperty, value);
        }

        public override void OnApplyTemplate ()
        {
            base.OnApplyTemplate();
        }
    }
}
