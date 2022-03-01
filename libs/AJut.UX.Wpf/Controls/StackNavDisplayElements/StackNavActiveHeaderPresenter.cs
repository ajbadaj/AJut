namespace AJut.UX.Controls
{
    using System.Windows;
    using System.Windows.Controls;
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

        public static readonly DependencyProperty DrawerNavButtonBaseStyleProperty = DPUtils.Register(_ => _.DrawerNavButtonBaseStyle);
        public Style DrawerNavButtonBaseStyle
        {
            get => (Style)this.GetValue(DrawerNavButtonBaseStyleProperty);
            set => this.SetValue(DrawerNavButtonBaseStyleProperty, value);
        }

        public static readonly DependencyProperty BackButtonBaseStyleProperty = DPUtils.Register(_ => _.BackButtonBaseStyle);
        public Style BackButtonBaseStyle
        {
            get => (Style)this.GetValue(BackButtonBaseStyleProperty);
            set => this.SetValue(BackButtonBaseStyleProperty, value);
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
