namespace AJut.Application
{
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
    }
}
