namespace AJut.UX.Controls
{
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Input;
    using System.Windows.Media;
    using AJut.UX.AttachedProperties;
    using AJut.UX;
    using DPUtils = AJut.UX.DPUtils<StackNavActiveContentPresenter>;
    using System;

    public class StackNavActiveContentPresenter : Control
    {
        static StackNavActiveContentPresenter ()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(StackNavActiveContentPresenter), new FrameworkPropertyMetadata(typeof(StackNavActiveContentPresenter)));
        }

        public StackNavActiveContentPresenter ()
        {
            this.InputBindings.Add(new InputBinding(NavigationCommands.BrowseBack, new KeyGesture(Key.Back)));
            this.CommandBindings.Add(new CommandBinding(NavigationCommands.BrowseBack, _OnBrowseBack, _CanBrowseBack));

            void _CanBrowseBack (object sender, CanExecuteRoutedEventArgs e)
            {
                if (this.Navigator?.CanGoBack == true)
                {
                    e.CanExecute = true;
                }
            }
            async void _OnBrowseBack (object sender, ExecutedRoutedEventArgs e)
            {
                await this.Navigator.PopDisplay();
            }
        }

        public static readonly DependencyProperty NavigatorProperty = DPUtils.Register(_ => _.Navigator);
        public StackNavFlowController Navigator
        {
            get => (StackNavFlowController)this.GetValue(NavigatorProperty);
            set => this.SetValue(NavigatorProperty, value);
        }

        public static readonly DependencyProperty CoverBackgroundBrushProperty = DPUtils.Register(_ => _.CoverBackgroundBrush);
        public Brush CoverBackgroundBrush
        {
            get => (Brush)this.GetValue(CoverBackgroundBrushProperty);
            set => this.SetValue(CoverBackgroundBrushProperty, value);
        }
        public override void OnApplyTemplate ()
        {
            base.OnApplyTemplate();
        }
    }
}
