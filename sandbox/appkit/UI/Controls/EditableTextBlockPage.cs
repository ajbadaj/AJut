namespace AJutShowRoomAppKit.UI.Controls
{
    using System;
    using AJut;
    using AJut.UX.CodeBinding;
    using AJut.UX.Controls;
    using AppKit;

    // One EditableTextBlock bound to a model, with read only and enabled switches, then the other ways an edit starts
    public sealed class EditableTextBlockPage : ShowroomPage
    {
        private const double kDemoWidth = 320.0;

        private readonly DemoSettings m_settings = new DemoSettings();
        private readonly BindingSet m_bindings = new BindingSet();
        private readonly EditableTextBlock m_bound = new EditableTextBlock();
        private readonly EditableTextBlock m_manual = new EditableTextBlock { Text = "Only code starts this one", EditTextInstigatorAction = eEditTextInstigator.Manually };
        private readonly BrushButton m_editFromCode;

        public EditableTextBlockPage ()
            : base("EditableTextBlock", "Text that reads as a label until it is double-clicked, then edits in place. Return keeps the edit, Escape puts back what was there, and the edit follows into the model as it is typed. Giving up focus, with Tab or a click into something else, ends the edit the LostFocusEditClose way.")
        {
            // 1. Bound to a model, with the model's switches
            m_bindings.BindText(m_bound, m_settings, nameof(DemoSettings.Name), this.ReadName, this.WriteName);
            m_bindings.BindEnabled(m_bound, m_settings, nameof(DemoSettings.IsEnabled), this.ReadIsEnabled);
            m_bindings.Watch(m_settings, nameof(DemoSettings.IsReadOnly), this.PushIsReadOnly);
            this.AddLabeledDemo("Double-click to rename", Sized(m_bound));

            NSTextField readout = ShowroomStyle.Understated(String.Empty);
            m_bindings.BindLabel(readout, m_settings, nameof(DemoSettings.Name), this.ReadNameText);
            this.AddLabeledDemo("Model says", readout);

            var readOnlyCheck = new ThemedCheckBox { Title = "Read only (the text can still be selected)" };
            m_bindings.BindChecked(readOnlyCheck, m_settings, nameof(DemoSettings.IsReadOnly), this.ReadIsReadOnly, this.WriteIsReadOnly);
            this.AddDemo(readOnlyCheck, eFillStackCrossAlignment.Start);

            var enabledCheck = new ThemedCheckBox { Title = "Enabled" };
            m_bindings.BindChecked(enabledCheck, m_settings, nameof(DemoSettings.IsEnabled), this.ReadIsEnabled, this.WriteIsEnabled);
            this.AddDemo(enabledCheck, eFillStackCrossAlignment.Start);

            this.AddCode(
@"var name = new EditableTextBlock { NoTextSetMessage = ""Name it"" };
bindings.BindText(
    name, model, nameof(Model.Name),
    () => model.Name, value => model.Name = value
);"
            );

            // 2. The other ways in
            this.AddHeading("Other ways to start, and canceling");
            this.AddText("The pointer coming over it starts an edit without taking the focus; leaving ends it, unless the field was clicked into. This one starts empty, so the message shows faded and italic, and as the field's placeholder.");
            this.AddLabeledDemo("Mouse over", Sized(new EditableTextBlock { EditTextInstigatorAction = eEditTextInstigator.MouseOver, NoTextSetMessage = "Point here to name it" }));

            this.AddText("The caret at the end, and an edit that ends any way but Return is canceled. Edit it, then click into the first one on the page, which takes the focus and cancels this edit; double-click this one again, and the canceled edit comes back.");
            this.AddLabeledDemo("Cancels on lost focus", Sized(new EditableTextBlock
            {
                Text = "Double-click, then click away",
                EditTextInitializationBehavior = eEditTextInitializationBehavior.CursorAtEnd,
                LostFocusEditClose = eEditTextCloseAction.Cancel,
            }));

            this.AddText("Long text, wrapped onto more lines with IsTextWrapping rather than cut short.");
            this.AddLabeledDemo("Wrapping", Sized(new EditableTextBlock { Text = "A longer name that wraps onto a second line when the control is narrower than the text", IsTextWrapping = true }));

            this.AddLabeledDemo("Manually", Sized(m_manual));
            m_editFromCode = new BrushButton { Title = "IsEditing = true" };
            m_editFromCode.Activated += this.OnEditFromCodeActivated;
            this.AddLabeledDemo("Start it from code", m_editFromCode);
        }

        protected override void OnTeardown ()
        {
            m_editFromCode.Activated -= this.OnEditFromCodeActivated;
            m_bindings.Dispose();
        }

        // ===========[ Events ]================================================
        private void OnEditFromCodeActivated (object sender, EventArgs e) => m_manual.IsEditing = true;

        // ===========[ Helper Methods ]========================================
        private static EditableTextBlock Sized (EditableTextBlock textBlock)
        {
            textBlock.TranslatesAutoresizingMaskIntoConstraints = false;
            textBlock.WidthAnchor.ConstraintEqualTo((nfloat)kDemoWidth).Active = true;
            return textBlock;
        }

        private string ReadName () => m_settings.Name;
        private void WriteName (string value) => m_settings.Name = value;
        private string ReadNameText () => $"\"{m_settings.Name}\"";
        private bool ReadIsReadOnly () => m_settings.IsReadOnly;
        private void WriteIsReadOnly (bool value) => m_settings.IsReadOnly = value;
        private bool ReadIsEnabled () => m_settings.IsEnabled;
        private void WriteIsEnabled (bool value) => m_settings.IsEnabled = value;
        private void PushIsReadOnly () => m_bound.IsReadOnly = m_settings.IsReadOnly;

        // ===========[ Subclasses/structs ]====================================
        private sealed class DemoSettings : NotifyPropertyChanged
        {
            private string m_name = "Double-click to rename";
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
