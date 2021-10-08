namespace AJut.UX
{
    using System.Windows.Input;

    public static class StackNavCommands
    {
        public static RoutedCommand ToggleDrawerOpenStateCommand = new RoutedCommand(nameof(ToggleDrawerOpenStateCommand), typeof(StackNavCommands));
    }
}
