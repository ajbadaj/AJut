namespace AJutShowRoomAndroid
{
    using AJut;
    using AJut.UX.CodeBinding;
    using AJut.UX.Controls;
    using Android.Views;
    using Android.Widget;

    /// <summary>The BusyWait demo: three docks that start and stop together, and an overlay over a button it blocks while busy.</summary>
    public sealed class BusyWaitPage : ShowroomPage
    {
        private readonly DemoSettings m_settings = new DemoSettings();

        public override string Title => "BusyWait";

        public override int LayoutId => Resource.Layout.demo_busy_wait;

        public override int MarkupSourceId => Resource.Raw.demo_busy_wait_source;

        public override void Bind (View root, MainActivity host, BindingSet binder)
        {
            BusyWait right = root.FindViewById<BusyWait>(Resource.Id.demo_busy_wait_right)!;
            BusyWait left = root.FindViewById<BusyWait>(Resource.Id.demo_busy_wait_left)!;
            BusyWait top = root.FindViewById<BusyWait>(Resource.Id.demo_busy_wait_top)!;
            BusyWait overlay = root.FindViewById<BusyWait>(Resource.Id.demo_busy_wait_overlay)!;

            binder.BindChecked(root.FindViewById<Switch>(Resource.Id.demo_busy_wait_spinning)!, m_settings, nameof(DemoSettings.IsSpinning), () => m_settings.IsSpinning, value => m_settings.IsSpinning = value);
            binder.Watch(m_settings, nameof(DemoSettings.IsSpinning), () =>
            {
                right.IsSpinning = m_settings.IsSpinning;
                left.IsSpinning = m_settings.IsSpinning;
                top.IsSpinning = m_settings.IsSpinning;
            });

            binder.BindChecked(root.FindViewById<Switch>(Resource.Id.demo_busy_wait_overlay_busy)!, m_settings, nameof(DemoSettings.IsBusy), () => m_settings.IsBusy, value => m_settings.IsBusy = value);
            binder.Watch(m_settings, nameof(DemoSettings.IsBusy), () => overlay.IsSpinning = m_settings.IsBusy);

            binder.OnClick(root.FindViewById(Resource.Id.demo_busy_wait_overlay_button)!, () => ++m_settings.Presses);
            binder.BindLabel(root.FindViewById<TextView>(Resource.Id.demo_busy_wait_overlay_count)!, m_settings, nameof(DemoSettings.Presses), () => $"Pressed {m_settings.Presses} times");
        }

        // ===========[ Subclasses/structs ]===================================
        private sealed class DemoSettings : NotifyPropertyChanged
        {
            private bool m_isSpinning = true;
            private bool m_isBusy;
            private int m_presses;

            public bool IsSpinning
            {
                get => m_isSpinning;
                set => this.SetAndRaiseIfChanged(ref m_isSpinning, value);
            }

            public bool IsBusy
            {
                get => m_isBusy;
                set => this.SetAndRaiseIfChanged(ref m_isBusy, value);
            }

            public int Presses
            {
                get => m_presses;
                set => this.SetAndRaiseIfChanged(ref m_presses, value);
            }
        }
    }
}
