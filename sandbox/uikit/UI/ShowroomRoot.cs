namespace AJutShowRoomUIKit.UI
{
    using System;
    using System.Collections.Generic;
    using UIKit;

    /// <summary>Builds the showroom's root: the theme bar over a navigation stack starting at the list of areas.</summary>
    public static class ShowroomRoot
    {
        /// <summary>
        /// The root for a window. <paramref name="path"/>, when given, names the entries to open on top of the area list,
        /// area first, for a rebuild (a theme switch) that should land back on the page it came from.
        /// </summary>
        public static UIViewController Create (IReadOnlyList<string> path = null)
        {
            var navigation = new UINavigationController(new ShowroomAreaListController());
            IReadOnlyList<ShowroomEntry> entries = ShowroomMap.Areas;
            foreach (string title in path ?? Array.Empty<string>())
            {
                ShowroomEntry entry = _Find(entries, title);
                if (entry == null)
                {
                    break;
                }

                UIViewController opened = ShowroomListController.Open(entry);
                navigation.PushViewController(opened, false);

                // Only a list has entries of its own to open next
                entries = (opened as ShowroomListController)?.Entries;
            }

            return new ShowroomShellController(navigation);

            static ShowroomEntry _Find (IReadOnlyList<ShowroomEntry> entries, string title)
            {
                if (entries == null)
                {
                    return null;
                }

                foreach (ShowroomEntry entry in entries)
                {
                    if (entry.Title == title)
                    {
                        return entry;
                    }
                }

                return null;
            }
        }

        /// <summary>The titles of the entries open in <paramref name="navigation"/>, area first, for <see cref="Create"/> to open again.</summary>
        public static IReadOnlyList<string> PathOf (UINavigationController navigation)
        {
            var path = new List<string>();
            foreach (UIViewController controller in navigation.ViewControllers)
            {
                if (controller.RestorationIdentifier != null)
                {
                    path.Add(controller.RestorationIdentifier);
                }
            }

            return path;
        }
    }
}
