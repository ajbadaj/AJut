namespace AJutShowRoomAppKit.UI.ContentFlows
{
    using System;
    using System.Globalization;
    using AJut;
    using AJut.TypeManagement;
    using AJut.UX.CodeBinding;
    using AJut.UX.Controls;
    using AJut.UX.Docking;
    using AJut.UX.Theming;
    using AppKit;

    // ===========[ ShowRoomPanelState ]=========================================
    /// <summary>
    /// What a <see cref="ShowRoomPanel"/> keeps in a saved layout. The type ids match the WinUI3 showroom's, so a layout
    /// saved by either showroom loads in the other.
    /// </summary>
    [TypeId("ShowRoomPanelState")]
    public class ShowRoomPanelState
    {
        public string Label { get; set; }
        public double Value { get; set; }
    }

    // ===========[ ShowRoomPanel ]==============================================
    /// <summary>
    /// The docking demo's panel: a label, a value saved with the layout, and a "Block close" switch that vetoes closing
    /// it, so the close veto can be tried by hand
    /// </summary>
    [TypeId("ShowRoomPanel")]
    public class ShowRoomPanel : ThemedView, IDockableDisplayElement
    {
        // The panel and its model hold each other only through these bindings, and both go together, so the
        // bindings need no teardown of their own
        private readonly PanelModel m_model = new PanelModel();
        private readonly BindingSet m_bindings = new BindingSet();

        public ShowRoomPanel ()
        {
            const double kValueFieldWidth = 140.0;

            this.BackgroundColorKey = ShowroomStyle.SidebarBackground;

            NSTextField label = ShowroomStyle.Heading(String.Empty);
            m_bindings.BindLabel(label, m_model, nameof(PanelModel.Label), this.ReadLabel);

            var value = new NSTextField { PlaceholderString = "Value (double)" };
            m_bindings.BindText(value, m_model, nameof(PanelModel.ValueText), this.ReadValueText, this.WriteValueText, commitOnly: true);

            var blockClose = new NSButton { Title = "Block close" };
            blockClose.SetButtonType(NSButtonType.Switch);
            m_bindings.BindChecked(blockClose, m_model, nameof(PanelModel.IsBlockingClose), this.ReadIsBlockingClose, this.WriteIsBlockingClose);

            var stack = new FillStack(NSUserInterfaceLayoutOrientation.Vertical, spacing: 6.0);
            stack.TranslatesAutoresizingMaskIntoConstraints = false;
            stack.Add(label, alignment: eFillStackCrossAlignment.Center);
            stack.Add(value, alignment: eFillStackCrossAlignment.Center);
            stack.Add(blockClose, alignment: eFillStackCrossAlignment.Center);
            this.AddSubview(stack);
            NSLayoutConstraint.ActivateConstraints(new[]
            {
                stack.CenterXAnchor.ConstraintEqualTo(this.CenterXAnchor),
                stack.CenterYAnchor.ConstraintEqualTo(this.CenterYAnchor),
                value.WidthAnchor.ConstraintEqualTo((nfloat)kValueFieldWidth),
            });
        }

        /// <summary>The panel's name, shown on it and used as its title</summary>
        public string Label
        {
            get => m_model.Label;
            set
            {
                m_model.Label = value;
                if (this.DockingAdapter != null)
                {
                    this.DockingAdapter.TitleContent = value;
                }
            }
        }

        /// <summary>A value saved and loaded with the layout, which "Set All Values" changes on every panel</summary>
        public double Value
        {
            get => m_model.Value;
            set => m_model.Value = value;
        }

        public DockingContentAdapterModel DockingAdapter { get; private set; }

        // ===========[ IDockableDisplayElement Implementations ]===============
        // The adapter is built for this panel and dropped with it, so the CanClose subscription lives
        // exactly as long as the panel does
        public virtual void Setup (DockingContentAdapterModel adapter)
        {
            this.DockingAdapter = adapter;
            adapter.CanClose -= this.OnCanClose;
            adapter.CanClose += this.OnCanClose;
            adapter.TitleContent = m_model.Label;
        }

        public object GenerateState () => new ShowRoomPanelState { Label = m_model.Label, Value = m_model.Value };

        public void ApplyState (object state)
        {
            if (state is ShowRoomPanelState typed)
            {
                this.Label = typed.Label;
                this.Value = typed.Value;
            }
        }

        private void OnCanClose (object sender, IsReadyToCloseEventArgs e)
        {
            if (m_model.IsBlockingClose)
            {
                e.IsReadyToClose = false;
            }
        }

        private string ReadLabel () => m_model.Label;
        private string ReadValueText () => m_model.ValueText;
        private void WriteValueText (string text) => m_model.ValueText = text;
        private bool ReadIsBlockingClose () => m_model.IsBlockingClose;
        private void WriteIsBlockingClose (bool isBlocking) => m_model.IsBlockingClose = isBlocking;

        // ===========[ Subclasses/structs ]====================================
        // What the panel shows, for its bindings. Text that doesn't parse as a number puts the last good value back.
        private sealed class PanelModel : NotifyPropertyChanged
        {
            private string m_label = "Panel";
            private double m_value;
            private bool m_isBlockingClose;

            public string Label
            {
                get => m_label;
                set => this.SetAndRaiseIfChanged(ref m_label, value);
            }

            public double Value
            {
                get => m_value;
                set => this.SetAndRaiseIfChanged(ref m_value, value, nameof(this.Value), nameof(this.ValueText));
            }

            public string ValueText
            {
                get => m_value.ToString("G", CultureInfo.InvariantCulture);
                set
                {
                    if (Double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed))
                    {
                        this.Value = parsed;
                    }
                    else
                    {
                        this.RaisePropertyChanged(nameof(this.ValueText));
                    }
                }
            }

            public bool IsBlockingClose
            {
                get => m_isBlockingClose;
                set => this.SetAndRaiseIfChanged(ref m_isBlockingClose, value);
            }
        }
    }

    /// <summary>The single instance panel: its toolbar button and menu item show and hide it, and closing it only hides it</summary>
    [TypeId("ShowRoomPanel-single-inst")]
    public class SingleInstanceShowRoomPanel : ShowRoomPanel
    {
        public override void Setup (DockingContentAdapterModel adapter)
        {
            base.Setup(adapter);
            adapter.HideDontClose = true;
        }
    }

    /// <summary>A single instance panel always in the layout and kept out of the toolbar and menu, the way an app's infrastructure panel would be</summary>
    [TypeId("ShowRoomPanel-hidden-infra")]
    public class HiddenInfraShowRoomPanel : ShowRoomPanel
    {
        public override void Setup (DockingContentAdapterModel adapter)
        {
            base.Setup(adapter);
            this.Label = "Hidden Infra";
        }
    }
}
