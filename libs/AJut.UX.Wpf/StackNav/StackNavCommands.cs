namespace AJut.UX
{
    using System.Windows.Input;

    /// <summary>
    /// Commands exclusive to the StackNav
    /// </summary>
    public static class StackNavCommands
    {
        /// <summary>
        /// Command for togglign the drawer open/close state
        /// </summary>
        public static RoutedCommand ToggleDrawerOpenStateCommand = new RoutedCommand(nameof(ToggleDrawerOpenStateCommand), typeof(StackNavCommands));
    }
}
