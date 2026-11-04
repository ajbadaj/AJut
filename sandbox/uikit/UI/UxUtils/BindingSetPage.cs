namespace AJutShowRoomUIKit.UI.UxUtils
{
    using System;
    using System.Windows.Input;
    using AJut;
    using AJut.UX;
    using AJut.UX.CodeBinding;
    using UIKit;

    /// <summary>
    /// BindingSet on UIKit: a plain C# model bound to stock UIKit controls. The set is made when the page appears and
    /// disposed when it goes, which is the whole teardown story.
    /// </summary>
    public sealed class BindingSetPage : ShowroomPageController
    {
        private readonly DemoModel m_model = new DemoModel();
        private readonly UITextField m_nameField = new UITextField { BorderStyle = UITextBorderStyle.RoundedRect, Placeholder = "Name" };
        private readonly UILabel m_greeting = new UILabel { Lines = 0 };
        private readonly UISwitch m_loudSwitch = new UISwitch();
        private readonly UISlider m_volumeSlider = new UISlider { MinValue = 0f, MaxValue = 1f };
        private readonly UILabel m_volumeLabel = new UILabel();
        private readonly UIButton m_resetButton = UIButton.FromType(UIButtonType.System);
        private BindingSet m_bindings;

        public BindingSetPage () : base("BindingSet")
        {
            m_resetButton.SetTitle("Reset (enabled once there is a name)", UIControlState.Normal);
        }

        public override void ViewWillAppear (bool animated)
        {
            base.ViewWillAppear(animated);

            m_bindings?.Dispose();
            m_bindings = new BindingSet();
            m_bindings.BindText(m_nameField, m_model, nameof(DemoModel.Name), () => m_model.Name, value => m_model.Name = value);
            m_bindings.BindLabel(m_greeting, m_model, nameof(DemoModel.Greeting), () => m_model.Greeting);
            m_bindings.BindOn(m_loudSwitch, m_model, nameof(DemoModel.IsLoud), () => m_model.IsLoud, value => m_model.IsLoud = value);
            m_bindings.BindValue(m_volumeSlider, m_model, nameof(DemoModel.Volume), () => m_model.Volume, value => m_model.Volume = value);
            m_bindings.BindLabel(m_volumeLabel, m_model, nameof(DemoModel.Volume), () => $"Volume: {m_model.Volume:P0}");
            m_bindings.BindCommand(m_resetButton, m_model.ResetCommand);
        }

        public override void ViewDidDisappear (bool animated)
        {
            base.ViewDidDisappear(animated);
            m_bindings?.Dispose();
            m_bindings = null;
        }

        protected override void BuildPage ()
        {
            this.AddHeading("A model bound to UIKit controls");
            this.AddText("The text field and switch edit the model two ways; the greeting is computed by the model and shown one way; Reset is an ICommand whose CanExecute drives the button's Enabled.");
            m_nameField.TranslatesAutoresizingMaskIntoConstraints = false;
            m_nameField.WidthAnchor.ConstraintEqualTo(240f).Active = true;
            this.AddDemo(m_nameField);
            this.AddDemoRow(new UILabel { Text = "Loud" }, m_loudSwitch);
            this.AddDemo(m_greeting);
            m_volumeSlider.TranslatesAutoresizingMaskIntoConstraints = false;
            m_volumeSlider.WidthAnchor.ConstraintEqualTo(240f).Active = true;
            this.AddDemoRow(m_volumeSlider, m_volumeLabel);
            this.AddDemo(m_resetButton);
            this.AddCode(@"
// ViewWillAppear
m_bindings = new BindingSet();
m_bindings.BindText(m_nameField, m_model, nameof(DemoModel.Name), () => m_model.Name, value => m_model.Name = value);
m_bindings.BindLabel(m_greeting, m_model, nameof(DemoModel.Greeting), () => m_model.Greeting);
m_bindings.BindOn(m_loudSwitch, m_model, nameof(DemoModel.IsLoud), () => m_model.IsLoud, value => m_model.IsLoud = value);
m_bindings.BindCommand(m_resetButton, m_model.ResetCommand);

// ViewDidDisappear: undoes every hookup above
m_bindings.Dispose();");
        }

        // ===========[ Subclasses/structs ]===================================
        private sealed class DemoModel : NotifyPropertyChanged
        {
            private string m_name = string.Empty;
            private bool m_isLoud;
            private float m_volume = 0.5f;
            private readonly ResetModelCommand m_resetCommand;

            public DemoModel ()
            {
                m_resetCommand = new ResetModelCommand(this);
            }

            public ICommand ResetCommand => m_resetCommand;

            public string Name
            {
                get => m_name;
                set
                {
                    if (this.SetAndRaiseIfChanged(ref m_name, value ?? string.Empty))
                    {
                        this.RaisePropertyChanged(nameof(Greeting));
                        m_resetCommand.RaiseCanExecuteChanged();
                    }
                }
            }

            public bool IsLoud
            {
                get => m_isLoud;
                set
                {
                    if (this.SetAndRaiseIfChanged(ref m_isLoud, value))
                    {
                        this.RaisePropertyChanged(nameof(Greeting));
                    }
                }
            }

            public float Volume
            {
                get => m_volume;
                set => this.SetAndRaiseIfChanged(ref m_volume, value);
            }

            public string Greeting
            {
                get
                {
                    string greeting = m_name.Length == 0 ? "Type a name" : $"Hello, {m_name}";
                    return m_isLoud ? greeting.ToUpperInvariant() + "!" : greeting;
                }
            }

            public void Reset ()
            {
                this.Name = string.Empty;
                this.IsLoud = false;
                this.Volume = 0.5f;
            }
        }

        private sealed class ResetModelCommand : ICommand
        {
            private readonly DemoModel m_model;

            public ResetModelCommand (DemoModel model)
            {
                m_model = model;
            }

            public event EventHandler CanExecuteChanged;

            public bool CanExecute (object parameter) => m_model.Name.Length > 0;

            public void Execute (object parameter) => m_model.Reset();

            public void RaiseCanExecuteChanged () => this.CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
