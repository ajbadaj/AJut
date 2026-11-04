namespace AJutShowRoomAndroid
{
    using AJut.UX;
    using AJut.UX.CodeBinding;
    using AJut.UX.Controls;
    using Android.Views;

    /// <summary>The Svg demo: the WPF showroom's penguin from its own file, and a small viewBox icon read from text.</summary>
    public sealed class SvgPage : ShowroomPage
    {
        // A 24 unit star on a viewBox: a polygon and a circle, with the star's paint set on its group
        private const string kStarIcon = @"<svg width=""24"" height=""24"" viewBox=""0 0 24 24"" xmlns=""http://www.w3.org/2000/svg"">
  <g fill=""#FFC107"" stroke=""#E65100"" stroke-width=""1"">
    <polygon points=""12,2 15,9 22,9 16.5,13.5 18.5,21 12,16.5 5.5,21 7.5,13.5 2,9 9,9""/>
  </g>
  <circle cx=""12"" cy=""12"" r=""2"" fill=""#E65100""/>
</svg>";

        public override string Title => "Svg";

        public override int LayoutId => Resource.Layout.demo_svg;

        public override int MarkupSourceId => Resource.Raw.demo_svg_source;

        public override void Bind (View root, MainActivity host, BindingSet binder)
        {
            SvgDocument? star = SvgParsing.ParseText(kStarIcon);
            root.FindViewById<Svg>(Resource.Id.demo_svg_icon_small)!.Source = star;
            root.FindViewById<Svg>(Resource.Id.demo_svg_icon_large)!.Source = star;
        }
    }
}
