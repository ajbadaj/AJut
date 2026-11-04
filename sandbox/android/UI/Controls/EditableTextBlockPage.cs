namespace AJutShowRoomAndroid
{
    using AJut;
    using AJut.UX.CodeBinding;
    using AJut.UX.Controls;
    using Android.Views;
    using Android.Widget;

    /// <summary>The EditableTextBlock demo: one bound to a model, with read only and enabled switches, and two other gestures.</summary>
    public sealed class EditableTextBlockPage : ShowroomPage
    {
        private readonly DemoSettings m_settings = new DemoSettings();

        public override string Title => "EditableTextBlock";

        public override int LayoutId => Resource.Layout.demo_editable_text_block;

        public override int MarkupSourceId => Resource.Raw.demo_editable_text_block_source;

        public override void Bind (View root, MainActivity host, BindingSet binder)
        {
            EditableTextBlock tap = root.FindViewById<EditableTextBlock>(Resource.Id.demo_editable_text_tap)!;
            binder.BindText(tap, m_settings, nameof(DemoSettings.Name), () => m_settings.Name, value => m_settings.Name = value);
            binder.BindLabel(root.FindViewById<TextView>(Resource.Id.demo_editable_text_value)!, m_settings, nameof(DemoSettings.Name), () => $"The model's name: {m_settings.Name}");

            binder.BindChecked(root.FindViewById<Switch>(Resource.Id.demo_editable_text_read_only)!, m_settings, nameof(DemoSettings.IsReadOnly), () => m_settings.IsReadOnly, value => m_settings.IsReadOnly = value);
            binder.Watch(m_settings, nameof(DemoSettings.IsReadOnly), () => tap.IsReadOnly = m_settings.IsReadOnly);

            Switch enabled = root.FindViewById<Switch>(Resource.Id.demo_editable_text_enabled)!;
            binder.BindChecked(enabled, m_settings, nameof(DemoSettings.IsEnabled), () => m_settings.IsEnabled, value => m_settings.IsEnabled = value);
            binder.BindEnabled(tap, m_settings, nameof(DemoSettings.IsEnabled), () => m_settings.IsEnabled);
        }

        // ===========[ Subclasses/structs ]===================================
        private sealed class DemoSettings : NotifyPropertyChanged
        {
            private string? m_name = "Tap to rename";
            private bool m_isReadOnly;
            private bool m_isEnabled = true;

            public string? Name
            {
                get => m_name;
                set => this.SetAndRaiseIfChanged(ref m_name, value);
            }

            public bool IsReadOnly
            {
                get => m_isReadOnly;
                set => this.SetAndRaiseIfChanged(ref m_isReadOnly, value);
            }

            public bool IsEnabled
            {
                get => m_isEnabled;
                set => this.SetAndRaiseIfChanged(ref m_isEnabled, value);
            }
        }
    }
}
