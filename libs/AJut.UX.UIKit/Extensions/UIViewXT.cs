namespace AJut.UX
{
    using UIKit;

    public static class UIViewXT
    {
        /// <summary>
        /// Finds the view controller that owns this view by walking the responder chain, which runs from a
        /// view up through its superviews to the controller whose root view it sits in. Returns null for a
        /// view that is not in a controller's hierarchy (not yet added, or already removed).
        /// </summary>
        public static UIViewController FindViewController (this UIView view)
        {
            UIResponder responder = view;
            while (responder != null)
            {
                if (responder is UIViewController controller)
                {
                    return controller;
                }

                responder = responder.NextResponder;
            }

            return null;
        }
    }
}
