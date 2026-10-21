namespace AJutShowRoomWinUI.UI
{
    using System;
    using AJut;
    using Microsoft.UI.Xaml.Controls;

    /// <summary>
    /// The one navigation pattern every area of the showroom uses: a NavigationView whose items carry the full type
    /// name of a Page in their Tag, and a Frame that shows the page for the selected item. Both the window and each
    /// area page use it, so a new page is a NavigationViewItem with a Tag and nothing else.
    /// </summary>
    public static class ShowroomNavigation
    {
        /// <summary>
        /// Navigates the frame to the page named by the item's Tag, when the tag names a Page type in this assembly.
        /// Returns false when it does not, so a caller can leave a non-page item (a header, say) alone.
        /// </summary>
        public static bool NavigateToTaggedPage (Frame frame, object selectedItem, object parameter = null)
        {
            if (frame == null || selectedItem is not NavigationViewItem item || item.Tag is not string typeName)
            {
                return false;
            }

            Type pageType = Type.GetType(typeName);
            if (pageType == null || !typeof(Page).IsAssignableFrom(pageType))
            {
                Logger.LogError($"Showroom navigation: '{typeName}' is not a Page type in this assembly");
                return false;
            }

            if (frame.CurrentSourcePageType == pageType)
            {
                return true;
            }

            return frame.Navigate(pageType, parameter);
        }

        /// <summary>
        /// Selects the first item in the view that carries a page tag, which navigates the frame through the view's
        /// SelectionChanged. Call it once from the owning page's Loaded.
        /// </summary>
        public static void SelectFirstPage (NavigationView view)
        {
            if (view == null || view.SelectedItem != null)
            {
                return;
            }

            foreach (object candidate in view.MenuItems)
            {
                if (candidate is NavigationViewItem item && item.Tag is string)
                {
                    view.SelectedItem = item;
                    return;
                }
            }
        }
    }
}
