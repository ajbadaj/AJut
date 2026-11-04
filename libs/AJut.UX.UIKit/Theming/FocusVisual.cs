namespace AJut.UX.Theming
{
    using System;
    using System.Linq;
    using CoreAnimation;
    using CoreGraphics;
    using Foundation;
    using UIKit;

    /// <summary>
    /// The keyboard focus look of the controls AJut draws itself on iOS and iPadOS, by MobileLook.FocusVisual: a thin
    /// dotted rectangle in the bright primary just inside the control, the same as AJut's WPF focus visual and the Android
    /// controls'. It is a shape layer over the control's own drawing while the control holds focus, which on iPad is
    /// keyboard focus. The control turns the system's own focus effect off, so the two never stack.
    /// </summary>
    public static class FocusVisual
    {
        private const string kLayerName = "AJutFocusVisual";

        // Over everything the control draws itself
        private const float kLayerZPosition = 1000f;

        /// <summary>
        /// Turns the system's focus effect off on <paramref name="view"/>, which then shows <see cref="Update"/>'s rectangle
        /// in its place. Call it once, from the control's setup.
        /// </summary>
        public static void TakeOver (UIView view)
        {
            view.FocusEffect = null;
        }

        /// <summary>
        /// Shows the rectangle while <paramref name="view"/> is focused, fitted to its bounds now, and takes it away when it
        /// isn't. Call it from DidUpdateFocus, and from LayoutSubviews so it follows a resize.
        /// </summary>
        public static void Update (UIView view)
        {
            CAShapeLayer existing = view.Layer.Sublayers?.OfType<CAShapeLayer>().FirstOrDefault(layer => layer.Name == kLayerName);
            if (!view.Focused)
            {
                existing?.RemoveFromSuperLayer();
                return;
            }

            CAShapeLayer rectangle = existing ?? new CAShapeLayer
            {
                Name = kLayerName,
                FillColor = UIColor.Clear.CGColor,
                ZPosition = kLayerZPosition,
            };

            if (existing == null)
            {
                view.Layer.AddSublayer(rectangle);
            }

            // The line is centered on the path, so the path runs half a line further in for its outer edge to sit at the inset
            nfloat inset = MobileLook.FocusVisual.Inset + (MobileLook.FocusVisual.StrokeThickness / 2f);
            UIColor stroke = ThemedColors.Get(MobileLook.FocusVisual.Stroke) ?? view.TintColor;
            rectangle.Frame = view.Bounds;
            rectangle.Path = UIBezierPath.FromRect(view.Bounds.Inset(inset, inset)).CGPath;
            rectangle.LineWidth = MobileLook.FocusVisual.StrokeThickness;
            rectangle.LineDashPattern = new[] { new NSNumber(MobileLook.FocusVisual.DashLength), new NSNumber(MobileLook.FocusVisual.DashGap) };
            rectangle.StrokeColor = stroke.GetResolvedColor(view.TraitCollection).CGColor;
        }
    }
}
