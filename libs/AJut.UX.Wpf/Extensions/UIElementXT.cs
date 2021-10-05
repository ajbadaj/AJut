namespace AJut.UX
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Markup;

    public static class UIElementXT
    {
        public static bool IsLocalPointInBounds (this UIElement element, Point point)
        {
            return new Rect(new Point(), element.RenderSize).Contains(point);
        }

        public static UIElement GetFirstChildAtParentLocalPoint (this UIElement parent, Point parentLocalPoint)
        {
            if (parent is IAddChild parentCasted)
            {
                foreach (UIElement child in parentCasted.GetChildren())
                {
                    Point childPoint = parent.TranslatePoint(parentLocalPoint, child);
                    if (child.IsLocalPointInBounds(childPoint))
                    {
                        return child;
                    }
                }
            }

            return null;
        }

        public static IEnumerable<UIElement> GetChildren (this IAddChild parent)
        {
            if (parent is Panel panel)
            {
                foreach (UIElement child in panel.Children)
                {
                    yield return child;
                }
            }

            if (parent is ItemsControl itemsControl)
            {
                for (int index = 0; index < itemsControl.Items.Count; ++index)
                {
                    if (itemsControl.ItemContainerGenerator.ContainerFromIndex(index) is UIElement castedChild)
                    {
                        yield return castedChild;
                    }
                }
            }
        }

        public static void SwapChildren (this IAddChild parent, UIElement a, UIElement b)
        {
            if (parent is Panel panel)
            {
                int a_index = panel.Children.IndexOf(a);
                int b_index = panel.Children.IndexOf(b);
                panel.Children.Swap(a_index, b_index);
            }
            else if (parent is ItemsControl itemsControl && itemsControl.ItemsSource is IList sourceItems)
            {
                int a_index = itemsControl.ItemContainerGenerator.IndexFromContainer(a);
                int b_index = itemsControl.ItemContainerGenerator.IndexFromContainer(b);
                sourceItems.Swap(a_index, b_index);
            }

            // Do the normal panel stuff above first to make sure z-order is right essentially.
            //  Then do Grid and Canvas after to make sure coordinates are correct
            if (parent is Grid)
            {
                int a_column = Grid.GetColumn(a);
                int a_row = Grid.GetRow(a);

                int b_column = Grid.GetColumn(b);
                int b_row = Grid.GetRow(b);

                Grid.SetColumn(a, b_column);
                Grid.SetRow(a, b_row);

                Grid.SetColumn(b, a_column);
                Grid.SetRow(b, a_row);
            }
            else if (parent is Canvas)
            {
                double ax = Canvas.GetLeft(a);
                double ay = Canvas.GetTop(a);

                double bx = Canvas.GetLeft(b);
                double by = Canvas.GetTop(b);

                Canvas.SetLeft(a, bx);
                Canvas.SetTop(a, by);

                Canvas.SetLeft(b, ax);
                Canvas.SetTop(b, ay);
            }
        }


        public static bool TryRemoveFromLogicalAndVisualParents (this UIElement target)
        {
            try
            {
                // If it's visual, we should be able to remove it from just the visual tree
                DependencyObject parent = target.GetVisualParent();
                if (parent == null)
                {
                    // No visual parent, no removal needed, success!
                    return true;
                }

                if (parent is Panel panelParent)
                {
                    int previousCount = panelParent.Children.Count;
                    panelParent.Children.Remove(target);
                    return previousCount != panelParent.Children.Count;
                }

                if (parent is Decorator decoratorParent)
                {
                    decoratorParent.Child = null;
                    return true;
                }

                if (parent is ItemsControl ic)
                {
                    // This probably means you can't do it, but you can try to remove from the items source if:
                    //  * It has one
                    //  * and that's an IList
                    //  * and that IList is not readonly
                    //
                    // Even this could be problematic so... not sure if this methodology will stay
                    if (ic.ItemsSource is IList itemsSource && !itemsSource.IsReadOnly)
                    {
                        int previousCount = itemsSource.Count;
                        itemsSource.Remove(ic.ItemContainerGenerator.ItemFromContainer(target));
                        return previousCount != itemsSource.Count;
                    }

                    // You need to remove items for an items control, and that does not always work out
                    return false;
                }
            }
            catch (Exception exc)
            {
                Logger.LogError("There was an issue trying to remove element from visual tree", exc);
            }

            return false;
        }
    }
}
