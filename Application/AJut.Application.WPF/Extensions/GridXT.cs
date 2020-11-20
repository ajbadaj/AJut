namespace AJut.Application
{
    using System.Collections.Generic;

#if WINDOWS_UWP
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Controls;
#else
    using System.Windows;
    using System.Windows.Controls;
#endif

    public static class GridXT
    {
        /// <summary>
        /// Returns all elements that have the indicated grid coordinates
        /// </summary>
        /// <typeparam name="T">The type of item to find</typeparam>
        /// <param name="rowNumber">The row to find it in</param>
        /// <param name="columnNumber">The column to find it in</param>
        /// <param name="collection">The collection of UIElement objects to look through</param>
        /// <returns>The first item found of the specified type, or null for none</returns>
        public static IEnumerable<T> FindChildrenWithGridCoords<T>(this Grid grid, int rowNumber, int columnNumber)
#if WINDOWS_UWP
            where T : FrameworkElement
#else
            where T : UIElement
#endif
        {
            foreach (UIElement element in grid.Children)
            {
                T casted = element as T;
                if (Grid.GetColumn(casted) == columnNumber && Grid.GetRow(casted) == rowNumber && casted != null)
                {
                    yield return casted;
                }
            }
        }

#if !WINDOWS_UWP
        public static void GetPotentialCoordinates(Grid grid, Point testPoint, out int rowNumber, out int columnNumber)
        {
            GetPotentialCoordinates(grid, testPoint.X, testPoint.Y, out rowNumber, out columnNumber);
        }
#endif

        /// <summary>
        /// Retrieves the possible row and column coordinates of a point's location
        /// </summary>
        /// <param name="grid">The grid to look through</param>
        /// <param name="testPoint">The point to find on the grid</param>
        /// <param name="rowNumber">The row that was found to contain the point</param>
        /// <param name="columnNumber">The column that was found to contain the point</param>
        public static void GetPotentialCoordinates(Grid grid, double testPointX, double testPointY, out int rowNumber, out int columnNumber)
        {
            double dWidth = 0.0;
            columnNumber = 0;
            foreach (ColumnDefinition def in grid.ColumnDefinitions)
            {
                dWidth += def.ActualWidth;
                if (dWidth > testPointX)
                {
                    break;
                }

                ++columnNumber;
            }

            double dHeight = 0.0;
            rowNumber = 0;
            foreach (RowDefinition def in grid.RowDefinitions)
            {
                dHeight += def.ActualHeight;
                if (dHeight > testPointY)
                {
                    break;
                }

                ++rowNumber;
            }
        }
    }
}
