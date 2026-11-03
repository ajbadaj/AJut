namespace AJutShowRoomUIKit.UI.Controls
{
    using System;
    using AJut;
    using AJut.UX.CodeBinding;
    using AJut.UX.Controls;
    using UIKit;

    /// <summary>
    /// EditableTextBlock on UIKit: one bound to a model, with read only and enabled switches, and two other gestures. The
    /// bindings are made when the page appears and disposed when it goes.
    /// </summary>
    public sealed class EditableTextBlockPage : ShowroomPageController
    {
        private readonly DemoSettings m_settings = new DemoSettings();
        private readonly EditableTextBlock m_tap = new EditableTextBlock();
        private readonly UILabel m_value = new UILabel { Lines = 0 };
        private readonly UISwitch m_readOnlySwitch = new UISwitch();
        private readonly UISwitch m_enabledSwitch = new UISwitch();
        private readonly EditableTextBlock m_doubleTap = new EditableTextBlock { EditTextInstigatorAction = eEditTextTouchInstigator.DoubleTap, NoTextSetMessage = "Double tap to name it" };
        private readonly EditableTextBlock m_longPress = new EditableTextBlock
        {
            Text = "Long press to rename",
            EditTextInstigatorAction = eEditTextTouchInstigator.LongPress,
            EditTextInitializationBehavior = eEditTextInitializationBehavior.CursorAtEnd,
            LostFocusEditClose = eEditTextCloseAction.Cancel,
        };

        private BindingSet m_bindings;

        // ===========[ Setup/Construction/Teardown ]===================================
        public EditableTextBlockPage () : base("EditableTextBlock")
        {
        }

        public override void ViewWillAppear (bool animated)
        {
            base.ViewWillAppear(animated);

            m_bindings?.Dispose();
            m_bindings = new BindingSet();
            m_bindings.BindText(m_tap, m_settings, nameof(DemoSettings.Name), () => m_settings.Name, value => m_settings.Name = value);
            m_bindings.BindLabel(m_value, m_settings, nameof(DemoSettings.Name), () => $"The model's name: {m_settings.Name}");
            m_bindings.BindOn(m_readOnlySwitch, m_settings, nameof(DemoSettings.IsReadOnly), () => m_settings.IsReadOnly, value => m_settings.IsReadOnly = value);
            m_bindings.Watch(m_settings, nameof(DemoSettings.IsReadOnly), () => m_tap.IsReadOnly = m_settings.IsReadOnly);
            m_bindings.BindOn(m_enabledSwitch, m_settings, nameof(DemoSettings.IsEnabled), () => m_settings.IsEnabled, value => m_settings.IsEnabled = value);
            m_bindings.BindEnabled(m_tap, m_settings, nameof(DemoSettings.IsEnabled), () => m_settings.IsEnabled);
        }

        public override void ViewDidDisappear (bool animated)
        {
            base.ViewDidDisappear(animated);
            m_bindings?.Dispose();
            m_bindings = null;
        }

        protected override void BuildPage ()
        {
            this.AddHeading("EditableTextBlock");
            this.AddText("Text that reads as a label until it is tapped, then edits in place. Done keeps the edit; the edit follows into the model as it is typed. Giving up focus ends the edit the LostFocusEditClose way.");
            this.AddWide(m_tap);
            this.AddDemo(m_value);
            this.AddDemoRow(new UILabel { Text = "Read only" }, m_readOnlySwitch, new UILabel { Text = "Enabled" }, m_enabledSwitch);
            this.AddCode(@"
var name = new EditableTextBlock { NoTextSetMessage = ""Name it"" };
bindings.BindText(name, model, nameof(Model.Name), () => model.Name, value => model.Name = value);");

            this.AddHeading("Other gestures, and canceling");
            this.AddText("A double tap, starting with no text: the empty message shows faded and italic, and as the placeholder while editing.");
            this.AddWide(m_doubleTap);
            this.AddText("A long press, with the caret at the end, and an edit that ends any way but Done is canceled. Edit it, then tap the first one on the page, which takes the focus and cancels this edit; long press this one again, and the canceled edit comes back.");
            this.AddWide(m_longPress);
        }

        // ===========[ Subclasses/structs ]===================================
        private sealed class DemoSettings : NotifyPropertyChanged
        {
            private string m_name = "Tap to rename";
            private bool m_isReadOnly;
            private bool m_isEnabled = true;

            public string Name
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
