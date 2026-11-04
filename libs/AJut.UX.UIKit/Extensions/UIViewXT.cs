namespace AJut.UX
{
    using AJut.UX.Theming;
    using CoreGraphics;
    using UIKit;

    public static class UIViewXT
    {
        /// <summary>
        /// Grows <paramref name="view"/> a little while it is pressed and springs it back when it is not, as iOS's own
        /// buttons do (<see cref="MobileLook.PressGrowth"/>). Safe to call on every state update: it only animates when
        /// the view is not already at the size the press asks for.
        /// </summary>
        internal static void ShowPressGrowth (this UIView view, bool isPressed)
        {
            CGAffineTransform target = isPressed
                ? CGAffineTransform.MakeScale(MobileLook.PressGrowth.PressedScale, MobileLook.PressGrowth.PressedScale)
                : CGAffineTransform.MakeIdentity();

            if (view.Transform == target)
            {
                return;
            }

            // The animation block runs inside AnimateNotify, which records the change and lets the block go, so the
            // capture of the view holds it no longer than this call. A transform leaves the view's layout alone, so the
            // views around it don't move. BeginFromCurrentState lets a quick tap turn the grow around partway.
            UIView.AnimateNotify(
                MobileLook.PressGrowth.Duration,
                0,
                MobileLook.PressGrowth.SpringDamping,
                0,
                UIViewAnimationOptions.BeginFromCurrentState | UIViewAnimationOptions.AllowUserInteraction,
                () => view.Transform = target,
                null
            );
        }

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
