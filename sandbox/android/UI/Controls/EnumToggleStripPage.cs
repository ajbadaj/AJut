namespace AJutShowRoomAndroid
{
    using System;
    using AJut;
    using AJut.UX.CodeBinding;
    using AJut.UX.Controls;
    using Android.Views;
    using Android.Widget;

    /// <summary>The EnumToggleStrip demo: a single-select strip with an excluded value, and a [Flags] multi-select strip.</summary>
    public sealed class EnumToggleStripPage : ShowroomPage
    {
        private readonly DemoSettings m_settings = new DemoSettings();

        public override string Title => "EnumToggleStrip";

        public override int LayoutId => Resource.Layout.demo_enum_toggle_strip;

        public override int MarkupSourceId => Resource.Raw.demo_enum_toggle_strip_source;

        public override void Bind (View root, MainActivity host, BindingSet binder)
        {
            binder.BindSelectedValue(root.FindViewById<EnumToggleStrip>(Resource.Id.demo_enum_strip)!, m_settings, nameof(DemoSettings.Weather), () => m_settings.Weather, value => m_settings.Weather = value);
            binder.BindLabel(root.FindViewById<TextView>(Resource.Id.demo_enum_value)!, m_settings, nameof(DemoSettings.Weather), () => $"Weather = {m_settings.Weather}");
            binder.BindSelectedValue(root.FindViewById<EnumToggleStrip>(Resource.Id.demo_enum_strip_vertical)!, m_settings, nameof(DemoSettings.Weather), () => m_settings.Weather, value => m_settings.Weather = value);

            binder.BindSelectedValue(root.FindViewById<EnumToggleStrip>(Resource.Id.demo_flags_strip)!, m_settings, nameof(DemoSettings.Days), () => m_settings.Days, value => m_settings.Days = value);
            binder.BindLabel(root.FindViewById<TextView>(Resource.Id.demo_flags_value)!, m_settings, nameof(DemoSettings.Days), () => $"Days = {m_settings.Days}");
        }

        // ===========[ Subclasses/structs ]===================================
        public enum eWeather
        {
            Sunny,
            Cloudy,
            Rainy,
            [ExcludeFromSelection]
            Unknown,
        }

        [Flags]
        public enum eWeekend
        {
            None = 0,
            Friday = 1,
            Saturday = 2,
            Sunday = 4,
        }

        private sealed class DemoSettings : NotifyPropertyChanged
        {
            private eWeather m_weather = eWeather.Cloudy;
            private eWeekend m_days = eWeekend.Saturday | eWeekend.Sunday;

            public eWeather Weather
            {
                get => m_weather;
                set => this.SetAndRaiseIfChanged(ref m_weather, value);
            }

            public eWeekend Days
            {
                get => m_days;
                set => this.SetAndRaiseIfChanged(ref m_days, value);
            }
        }
    }
}
