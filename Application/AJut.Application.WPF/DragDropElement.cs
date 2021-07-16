namespace AJut.Application
{
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Input;

    /// <summary>
    /// Unlike <see cref="System.Windows.DragDrop"/> which targets windows, these helpers target dragging around ui elements
    /// </summary>
    public static class DragDropElement
    {
        public static RoutedUICommand HorizontalDragInitiatedCommand = new RoutedUICommand(nameof(HorizontalDragInitiatedCommand), nameof(HorizontalDragInitiatedCommand), typeof(DragDrop));
        public static RoutedUICommand VerticalDragInitiatedCommand = new RoutedUICommand(nameof(VerticalDragInitiatedCommand), nameof(VerticalDragInitiatedCommand), typeof(DragDrop));
        public static RoutedUICommand DragInitiatedCommand = new RoutedUICommand(nameof(DragInitiatedCommand), nameof(DragInitiatedCommand), typeof(DragDrop));

        public static RoutedUICommand CancelDragReorderCommand = new RoutedUICommand("CancelDragReorder", nameof(CancelDragReorderCommand), typeof(DragDrop));

        public static async Task DoDragReorder (UIElement target)
        {
            await DoDragReorder(target, CancellationToken.None).ConfigureAwait(false);
        }
        public static async Task DoDragReorder(UIElement target, CancellationToken cancellationToken)
        {
            // IDEAS:
            // * Maybe a special
        }
    }
}
