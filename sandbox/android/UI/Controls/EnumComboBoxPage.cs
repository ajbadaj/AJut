namespace AJutShowRoomAndroid
{
    using System;
    using AJut;
    using AJut.UX.CodeBinding;
    using AJut.UX.Controls;
    using Android.Views;
    using Android.Widget;

    /// <summary>The EnumComboBox demo: a day of the week, and a line that follows the pick.</summary>
    public sealed class EnumComboBoxPage : ShowroomPage
    {
        private readonly DemoSettings m_settings = new DemoSettings();

        public override string Title => "EnumComboBox";

        public override int LayoutId => Resource.Layout.demo_enum_combo_box;

        public override int MarkupSourceId => Resource.Raw.demo_enum_combo_box_source;

        public override void Bind (View root, MainActivity host, BindingSet binder)
        {
            binder.BindSelectedValue(root.FindViewById<EnumComboBox>(Resource.Id.demo_enum_combo)!, m_settings, nameof(DemoSettings.Day), () => m_settings.Day, value => m_settings.Day = value);
            binder.BindLabel(root.FindViewById<TextView>(Resource.Id.demo_enum_combo_value)!, m_settings, nameof(DemoSettings.Day), () => _Describe(m_settings.Day));
        }

        private static string _Describe (DayOfWeek day) => day switch
        {
            DayOfWeek.Saturday or DayOfWeek.Sunday => $"{day} is a weekend day.",
            DayOfWeek.Friday => "Friday is a fine choice.",
            _ => $"{day} is a weekday.",
        };

        // ===========[ Subclasses/structs ]===================================
        private sealed class DemoSettings : NotifyPropertyChanged
        {
            private DayOfWeek m_day = DayOfWeek.Friday;

            public DayOfWeek Day
            {
                get => m_day;
                set => this.SetAndRaiseIfChanged(ref m_day, value);
            }
        }
    }
}
