namespace AJutShowRoomAndroid
{
    using AJut.UX.CodeBinding;
    using AJut.UX.Controls;
    using Android.Graphics;
    using Android.Graphics.Drawables;
    using Android.Views;
    using Android.Widget;

    /// <summary>
    /// The SimpleWrapPanel demo, as WinUI3's: uneven chips flowing and wrapping, with one that starts gone so bringing it
    /// back exercises a child joining the flow.
    /// </summary>
    public sealed class SimpleWrapPanelPage : ShowroomPage
    {
        private View? m_epsilon;

        public override string Title => "SimpleWrapPanel";

        public override int LayoutId => Resource.Layout.demo_simple_wrap_panel;

        public override int MarkupSourceId => Resource.Raw.demo_simple_wrap_panel_source;

        public override void Bind (View root, MainActivity host, BindingSet binder)
        {
            var panel = root.FindViewById<SimpleWrapPanel>(Resource.Id.demo_wrap_panel)!;
            float density = host.Resources!.DisplayMetrics!.Density;
            string[] labels = { "Alpha", "Beta", "Gamma", "Delta", "Epsilon", "Zeta", "Eta", "Theta", "Iota", "Kappa" };
            for (int index = 0; index < labels.Length; ++index)
            {
                TextView chip = _Chip(host, labels[index], index, density);
                if (index == 4)
                {
                    chip.Visibility = ViewStates.Gone;
                    m_epsilon = chip;
                }

                panel.AddView(chip);
            }

            binder.OnClick(root.FindViewById(Resource.Id.demo_wrap_toggle)!, this.ToggleEpsilon);
        }

        public override void Unbind ()
        {
            m_epsilon = null;
        }

        private void ToggleEpsilon ()
        {
            if (m_epsilon != null)
            {
                m_epsilon.Visibility = m_epsilon.Visibility == ViewStates.Gone ? ViewStates.Visible : ViewStates.Gone;
            }
        }

        // Content-sized chips of uneven width, so the flow packs several on a line
        private static TextView _Chip (MainActivity host, string text, int index, float density)
        {
            int tint = 70 + ((index * 22) % 150);
            var background = new GradientDrawable();
            background.SetColor(Color.Argb(0x55, tint, 0x90, 0xC8));
            background.SetCornerRadius(4 * density);

            var chip = new TextView(host) { Text = text, Background = background };
            chip.SetPadding((int)(8 * density), (int)(4 * density), (int)(8 * density), (int)(4 * density));
            return chip;
        }
    }
}
