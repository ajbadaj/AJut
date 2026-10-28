namespace AJut.UX
{
    using UIKit;

    public static class UIColorXT
    {
        /// <summary>
        /// Builds a dynamic color that resolves to <paramref name="light"/> or <paramref name="dark"/> by the
        /// appearance of whatever view draws it, the same way the UIKit system colors do. Anything that takes
        /// a UIColor (a button configuration, a view's background, a label's text color) follows a light/dark
        /// switch on its own, with no trait change handling at the use site.
        /// </summary>
        /// <remarks>
        /// The one place that does NOT follow along is a CALayer property (BorderColor, ShadowColor, and so
        /// on), which takes a CGColor: converting to CGColor freezes whatever the color resolves to at that
        /// moment. Resolve with <c>GetResolvedColor(this.TraitCollection)</c> inside the view's
        /// LayoutSubviews, which UIKit calls again on an appearance change.
        /// </remarks>
        public static UIColor FromLightDark (UIColor light, UIColor dark)
        {
            if (light.Equals(dark))
            {
                return light;
            }

            return new UIColor(new LightDarkPair(light, dark).Resolve);
        }

        // UIColor keeps the provider delegate alive for as long as the color lives. Holding the pair in a
        // small object with a named method keeps the capture explicit: two colors, never a view.
        private sealed class LightDarkPair
        {
            private readonly UIColor m_light;
            private readonly UIColor m_dark;

            public LightDarkPair (UIColor light, UIColor dark)
            {
                m_light = light;
                m_dark = dark;
            }

            public UIColor Resolve (UITraitCollection traits) => traits.UserInterfaceStyle == UIUserInterfaceStyle.Dark ? m_dark : m_light;
        }
    }
}
