namespace AJutShowRoomAndroid
{
    using System;
    using AJut;
    using AJut.UX;
    using AJut.UX.CodeBinding;
    using AJut.UX.Controls;
    using Android.Views;
    using Android.Widget;

    /// <summary>
    /// The DateTimeEditIngressControl demo: a date bound to a model shown under it, a time, a date and time on each clock, a
    /// formatted date and time with its last completed edit, empty treated as invalid, and read only.
    /// </summary>
    public sealed class DateTimeEditIngressControlPage : ShowroomPage
    {
        private readonly DemoSettings m_settings = new DemoSettings();
        private DateTimeEditIngressControl? m_dateAndTime;
        private TextView? m_lastEdit;

        public override string Title => "DateTimeEditIngressControl";

        public override int LayoutId => Resource.Layout.demo_date_time_edit_ingress_control;

        public override int MarkupSourceId => Resource.Raw.demo_date_time_edit_ingress_control_source;

        public override void Bind (View root, MainActivity host, BindingSet binder)
        {
            binder.BindEditDateTime(root.FindViewById<DateTimeEditIngressControl>(Resource.Id.demo_date_only)!, m_settings, nameof(DemoSettings.Due), () => m_settings.Due, value => m_settings.Due = value);
            binder.BindLabel(root.FindViewById<TextView>(Resource.Id.demo_date_only_value)!, m_settings, nameof(DemoSettings.Due), () => $"Due = {m_settings.Due?.ToString("D") ?? "(none)"}");

            root.FindViewById<DateTimeEditIngressControl>(Resource.Id.demo_date_read_only)!.EditDateTime = DateTime.Now;
            root.FindViewById<DateTimeEditIngressControl>(Resource.Id.demo_date_twenty_four_hour)!.EditDateTime = DateTime.Now;
            root.FindViewById<DateTimeEditIngressControl>(Resource.Id.demo_date_twelve_hour)!.EditDateTime = DateTime.Now;

            m_lastEdit = root.FindViewById<TextView>(Resource.Id.demo_date_and_time_last_edit)!;
            m_dateAndTime = root.FindViewById<DateTimeEditIngressControl>(Resource.Id.demo_date_and_time)!;
            m_dateAndTime.UserEditComplete += this.OnDateAndTimeUserEditComplete;
        }

        public override void Unbind ()
        {
            if (m_dateAndTime != null)
            {
                m_dateAndTime.UserEditComplete -= this.OnDateAndTimeUserEditComplete;
                m_dateAndTime = null;
            }

            m_lastEdit = null;
        }

        private void OnDateAndTimeUserEditComplete (object? sender, UserEditAppliedEventArgs e)
        {
            if (m_lastEdit != null)
            {
                m_lastEdit.Text = $"Last edit: {e.OldValue ?? "(none)"} to {e.NewValue ?? "(none)"}";
            }
        }

        // ===========[ Subclasses/structs ]===================================
        private sealed class DemoSettings : NotifyPropertyChanged
        {
            private DateTime? m_due;

            public DateTime? Due
            {
                get => m_due;
                set => this.SetAndRaiseIfChanged(ref m_due, value);
            }
        }
    }
}
