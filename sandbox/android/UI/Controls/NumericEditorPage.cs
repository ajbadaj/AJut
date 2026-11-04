namespace AJutShowRoomAndroid
{
    using AJut;
    using AJut.UX.CodeBinding;
    using AJut.UX.Controls;
    using Android.Views;
    using Android.Widget;

    /// <summary>The NumericEditor demo: an int and a double, each bound to a model property shown under it.</summary>
    public sealed class NumericEditorPage : ShowroomPage
    {
        private readonly DemoSettings m_settings = new DemoSettings();

        public override string Title => "NumericEditor";

        public override int LayoutId => Resource.Layout.demo_numeric_editor;

        public override int MarkupSourceId => Resource.Raw.demo_numeric_editor_source;

        public override void Bind (View root, MainActivity host, BindingSet binder)
        {
            binder.BindValue(root.FindViewById<NumericEditor>(Resource.Id.demo_numeric_int)!, m_settings, nameof(DemoSettings.Count), () => m_settings.Count, value => m_settings.Count = value);
            binder.BindLabel(root.FindViewById<TextView>(Resource.Id.demo_numeric_int_value)!, m_settings, nameof(DemoSettings.Count), () => $"Count = {m_settings.Count} (int)");

            binder.BindValue(root.FindViewById<NumericEditor>(Resource.Id.demo_numeric_double)!, m_settings, nameof(DemoSettings.Scale), () => m_settings.Scale, value => m_settings.Scale = value);
            binder.BindLabel(root.FindViewById<TextView>(Resource.Id.demo_numeric_double_value)!, m_settings, nameof(DemoSettings.Scale), () => $"Scale = {m_settings.Scale} (double)");
        }

        // ===========[ Subclasses/structs ]===================================
        private sealed class DemoSettings : NotifyPropertyChanged
        {
            private int m_count = 5;
            private double m_scale = 1.0;

            public int Count
            {
                get => m_count;
                set => this.SetAndRaiseIfChanged(ref m_count, value);
            }

            public double Scale
            {
                get => m_scale;
                set => this.SetAndRaiseIfChanged(ref m_scale, value);
            }
        }
    }
}
