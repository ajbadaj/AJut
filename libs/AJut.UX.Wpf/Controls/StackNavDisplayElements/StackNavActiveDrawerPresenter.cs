namespace AJut.UX.Controls
{
    using System.Windows;
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

        public static readonly DependencyProperty FixedDrawerDisplayProperty = DPUtils.Register(_ => _.FixedDrawerDisplay);
        public DependencyObject FixedDrawerDisplay
        {
            get => (DependencyObject)this.GetValue(FixedDrawerDisplayProperty);
            set => this.SetValue(FixedDrawerDisplayProperty, value);
        }

        public static readonly DependencyProperty NavigatorProperty = DPUtils.Register(_ => _.Navigator);
        public StackNavFlowController Navigator
        {
            get => (StackNavFlowController)this.GetValue(NavigatorProperty);
            set => this.SetValue(NavigatorProperty, value);
        }
    }
}
