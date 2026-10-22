namespace AJutShowRoomUIKit.UI
{
    using UIKit;

    /// <summary>Builds the showroom's root: a navigation stack starting at the list of areas.</summary>
    public static class ShowroomRoot
    {
        /// <summary>
        /// The navigation stack for a window. <paramref name="startOn"/>, when given, is pushed on top of the area list,
        /// for a rebuild (a theme switch) that should land back on the page it came from.
        /// </summary>
        public static UINavigationController Create (UIViewController startOn = null)
        {
            var navigation = new UINavigationController(new ShowroomAreaListController());
            if (startOn != null)
            {
                navigation.PushViewController(startOn, false);
            }

            return navigation;
        }
    }
}
