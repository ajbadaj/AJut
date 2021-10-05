namespace AJut.UX
{
    using System;
    using System.Collections;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Input;
    using System.Windows.Markup;

    public class DragDropItemsSwapEventArgs : RoutedEventArgs
    {
        public DragDropItemsSwapEventArgs (int moveFromIndex, int moveToIndex)
        {
            this.MoveFromIndex = moveFromIndex;
            this.MoveToIndex = moveToIndex;
        }

        public int MoveFromIndex { get; }
        public int MoveToIndex { get; }
    }

    public delegate void DragDropItemsSwapHandler (object sender, DragDropItemsSwapEventArgs e);
    /// <summary>
    /// Unlike <see cref="System.Windows.DragDrop"/> which targets windows, these helpers target dragging around ui elements
    /// </summary>
    public static class DragDropElement
    {
        private static readonly AEUtilsRegistrationHelper AEUtils = new AEUtilsRegistrationHelper(typeof(DragDropElement));
        private static readonly APUtilsRegistrationHelper APUtils = new APUtilsRegistrationHelper(typeof(DragDropElement));


        public static RoutedUICommand HorizontalDragInitiatedCommand = new RoutedUICommand(nameof(HorizontalDragInitiatedCommand), nameof(HorizontalDragInitiatedCommand), typeof(DragDrop));
        public static RoutedUICommand VerticalDragInitiatedCommand = new RoutedUICommand(nameof(VerticalDragInitiatedCommand), nameof(VerticalDragInitiatedCommand), typeof(DragDrop));
        public static RoutedUICommand DragInitiatedCommand = new RoutedUICommand(nameof(DragInitiatedCommand), nameof(DragInitiatedCommand), typeof(DragDrop));

        public static RoutedUICommand CancelDragReorderCommand = new RoutedUICommand("CancelDragReorder", nameof(CancelDragReorderCommand), typeof(DragDrop));

        public static RoutedEvent DragDropItemsSwapEvent = AEUtils.Register<DragDropItemsSwapHandler>(AddDragDropItemsSwapHandler, RemoveDragDropItemsSwapHandler);
        public static void AddDragDropItemsSwapHandler (DependencyObject obj, DragDropItemsSwapHandler handler)
        {
            if (obj is UIElement ui)
            {
                ui.AddHandler(DragDropItemsSwapEvent, handler);
            }
        }
        public static void RemoveDragDropItemsSwapHandler (DependencyObject obj, DragDropItemsSwapHandler handler)
        {
            if (obj is UIElement ui)
            {
                ui.RemoveHandler(DragDropItemsSwapEvent, handler);
            }
        }

        public static DependencyProperty IsDraggingProperty = APUtils.Register(GetIsDragging, SetIsDragging);
        public static bool GetIsDragging (DependencyObject obj) => (bool)obj.GetValue(IsDraggingProperty);
        public static void SetIsDragging (DependencyObject obj, bool value) => obj.SetValue(IsDraggingProperty, value);

        public static async Task DoDragReorder (UIElement owner, ActiveDragTracking activeDrag)
        {
            await DoDragReorder(owner, activeDrag, CancellationToken.None).ConfigureAwait(false);
        }

        public static async Task DoDragReorder(UIElement owner, ActiveDragTracking activeDrag, CancellationToken cancellationToken)
        {
            if (!(owner is IAddChild childAddrTarget))
            {
                activeDrag.Dispose();
                return;
            }

            UIElement rootDraggedItem = activeDrag.DragOwner.GetFirstChildAtParentLocalPoint(activeDrag.GetCurrentPoint());
            if (rootDraggedItem == null || !activeDrag.Engage())
            {
                activeDrag.Dispose();
                return;
            }


            SetIsDragging(rootDraggedItem, true);
            try
            {
                TaskCompletionSource reorderer = new TaskCompletionSource();
                cancellationToken.Register(() => reorderer.SetCanceled());

                activeDrag.SignalDragMoved += _OnActiveDragMoved;
                activeDrag.SignalDragEnd += _OnActiveDragEnded;

                await reorderer.Task;


                void _OnActiveDragMoved (object _sender, EventArgs<Point> _e)
                {
                    if (rootDraggedItem == null)
                    {
                        rootDraggedItem = activeDrag.DragOwner.GetFirstChildAtParentLocalPoint(activeDrag.GetCurrentPoint());
                    }

                    if (rootDraggedItem == null)
                    {
                        return;
                    }

                    //.GetFirstChildAtParentLocalPoint(localStartPoint)
                    foreach (UIElement child in childAddrTarget.GetChildren().Where(c => c != rootDraggedItem))
                    {
                        Point childPoint = activeDrag.DragOwner.TranslatePoint(_e.Value, child);
                        if (child.IsLocalPointInBounds(childPoint))
                        {
                            rootDraggedItem = DoSwap(owner, rootDraggedItem, child) as UIElement;
                            break;
                        }
                    }
                }

                void _OnActiveDragEnded (object _sender, EventArgs _e)
                {
                    reorderer.TrySetResult();
                }
            }
            finally
            {
                SetIsDragging(rootDraggedItem, false);
                activeDrag.Dispose();
            }

        }

        private static DependencyObject DoSwap (UIElement parent, UIElement a, UIElement b)
        {
            // ==========================================================================================
            // Special Case: ItemsControl
            // ==========================================================================================
            // Items control is a special case because the items source for this is what we want our
            //  swap target to be since that determines container order. It could be that ItemsSource
            //  is readonly, or otherwise a collection which is determined from another collection
            //  higher up the food chain - and so to make this work, we allow a DragDropElement specific
            //  override option, where users can override the DragDropItemsSwap event and handle the
            //  items swap themselves.
            if (parent is ItemsControl itemsControl)
            {
                int a_index = itemsControl.ItemContainerGenerator.IndexFromContainer(a);
                int b_index = itemsControl.ItemContainerGenerator.IndexFromContainer(b);

                var mouseEventArgs = new DragDropItemsSwapEventArgs(a_index, b_index);
                mouseEventArgs.RoutedEvent = DragDropElement.DragDropItemsSwapEvent;
                parent.RaiseEvent(mouseEventArgs);
                if (mouseEventArgs.Handled)
                {
                    // No longer dragging a, at this point a is likely been destroyed as the underlying
                    //  items source is updated, and new controls created
                    SetIsDragging(a, false);

                    // Find the new container that we're working with for dragging
                    var newContainer = itemsControl.ItemContainerGenerator.ContainerFromIndex(b_index);
                    SetIsDragging(newContainer, true);

                    // Return the new container
                    return newContainer;
                }
            }

            // If it's not the special case, handle this in a generic way
            ((IAddChild)parent).SwapChildren(a, b);
            return a;
        }


        public static bool CanDoDragReorder (UIElement owner, ActiveDragTracking parameter)
        {
            //ItemsControl i;
            //IContainItemStorage
            return parameter != null && owner is IAddChild;
            /*
             * UIElement variableContainer = source.SearchParentsOf<UIElement>().FirstOrDefault(p => GetIsDragDropReorderableContainer(p));
            if (variableContainer != null)
            {
                return new DragReorderSrc
                {
                    Source = source,
                    Representative = source,
                    ContainerBase = variableContainer
                };
            }

            ItemsControl itemsContainer = source.GetFirstParentOf<ItemsControl>(eTraversalTree.Both);
            if (itemsContainer != null)
            {
                var elementContainer = (UIElement)ItemsControl.ContainerFromElement(itemsContainer, source);
                if (elementContainer != null && itemsContainer.ItemsSource is IList list && !list.IsReadOnly)
                {
                    return new DragReorderSrc
                    {
                        Source = source,
                        Representative = elementContainer,
                        ContainerBase = itemsContainer
                    };
                }
            }

            IAddChild container = source.SearchParentsOf<IAddChild>(eTraversalTree.Both).Where(x => x is UIElement).FirstOrDefault();
            if (container != null)
            {
                return new DragReorderSrc
                {
                    Source = source,
                    ContainerBase = (UIElement)container,
                }
            }
             * */
        }
    }
}
