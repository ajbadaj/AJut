namespace AJutShowRoomAndroid
{
    using System;
    using AJut;
    using AJut.UX;
    using AJut.UX.CodeBinding;
    using AJut.UX.Controls;
    using AJut.UX.Helpers;
    using Android.Views;
    using Android.Widget;
    using AndroidColor = Android.Graphics.Color;

    /// <summary>The ColorEditIngressControl demo: a swatch, a button that follows it live, and the last completed edit.</summary>
    public sealed class ColorEditIngressControlPage : ShowroomPage
    {
        private ColorEditIngressControl? m_swatch;
        private AutoBrushButton? m_follower;
        private TextView? m_lastEdit;

        public override string Title => "ColorEditIngressControl";

        public override int LayoutId => Resource.Layout.demo_color_edit_ingress_control;

        public override int MarkupSourceId => Resource.Raw.demo_color_edit_ingress_control_source;

        public override void Bind (View root, MainActivity host, BindingSet binder)
        {
            m_swatch = root.FindViewById<ColorEditIngressControl>(Resource.Id.demo_color_edit)!;
            m_follower = root.FindViewById<AutoBrushButton>(Resource.Id.demo_color_follower)!;
            m_lastEdit = root.FindViewById<TextView>(Resource.Id.demo_color_last_edit)!;

            m_follower.PropagatedAccentColor = m_swatch.EditColor.ToAndroidColor();
            m_swatch.EditColorChanged += this.OnSwatchColorChanged;
            m_swatch.UserEditComplete += this.OnSwatchUserEditComplete;
        }

        public override void Unbind ()
        {
            if (m_swatch != null)
            {
                m_swatch.EditColorChanged -= this.OnSwatchColorChanged;
                m_swatch.UserEditComplete -= this.OnSwatchUserEditComplete;
                m_swatch = null;
            }

            m_follower = null;
            m_lastEdit = null;
        }

        private void OnSwatchColorChanged (object? sender, EventArgs<ColorArgb> e)
        {
            if (m_follower != null)
            {
                m_follower.PropagatedAccentColor = e.Value.ToAndroidColor();
            }
        }

        private void OnSwatchUserEditComplete (object? sender, UserEditAppliedEventArgs e)
        {
            if (m_lastEdit != null)
            {
                m_lastEdit.Text = $"Last edit: {_Hex((ColorArgb)e.OldValue)} to {_Hex((ColorArgb)e.NewValue)}";
            }
        }

        private static string _Hex (ColorArgb color) => $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";
    }
}
