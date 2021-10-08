namespace AJut.UX.Controls
{
    using System.Windows;
    using System.Windows.Input;

    public interface IStackNavPresenter
    {
        StackNavFlowController Navigator { get; }
    }

    public static class StackNavPresenterXT
    {
        public static void SetupBasicNavigatorCommandBindings<T> (this T control)
            where T : UIElement, IStackNavPresenter
        {
            control.CommandBindings.Add(new CommandBinding(NavigationCommands.BrowseBack, _OnBrowseBack, _CanBrowseBack));
            control.CommandBindings.Add(new CommandBinding(StackNavCommands.ToggleDrawerOpenStateCommand, _OnToggleDrawerOpen, _CanToggleDrawerOpen));

            void _CanBrowseBack (object sender, CanExecuteRoutedEventArgs e)
            {
                if ((control.Navigator?.CanGoBack == true || control.Navigator?.StackTopDisplayAdapter.IsShowingPopover == true) && !control.Navigator.StackTopDisplayAdapter.IsBusyWaitActive)
                {
                    e.CanExecute = true;
                }
            }
            async void _OnBrowseBack (object sender, ExecutedRoutedEventArgs e)
            {
                if (control.Navigator.StackTopDisplayAdapter.IsShowingPopover)
                {
                    control.Navigator.StackTopDisplayAdapter.PopoverDisplay.Cancel();
                }
                else
                {
                    await control.Navigator.PopDisplay();
                }
            }

            void _CanToggleDrawerOpen (object sender, CanExecuteRoutedEventArgs e)
            {
                if (control.Navigator?.IsDrawerOpen == true || control.Navigator?.CanCloseDrawer == true)
                {
                    e.CanExecute = true;
                }
            }

            void _OnToggleDrawerOpen (object sender, ExecutedRoutedEventArgs e)
            {
                if (control.Navigator.IsDrawerOpen)
                {
                    control.Navigator.CloseDrawer();
                }
                else
                {
                    control.Navigator.OpenDrawer();
                }
            }
        }
    }
}
