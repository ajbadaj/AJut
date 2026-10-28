namespace AJutShowRoomAppKit.UI.Controls
{
    using System;
    using AJut.UX.Controls;
    using AJut.UX.Helpers;
    using AppKit;

    public sealed class GeometryButtonPage : ShowroomPage
    {
        private const string kNotchedShape = "M 0,0 20,20 40,0 150,0 150,100 100,90 0,100 Z";
        private const string kPlayShape = "M0,0 L10,6 L0,12 Z";
        private const string kCircleShape = "M 50,0 A 50,50 0 1 1 49.99,0 Z";
        private const string kWaveShape = "M 0,20 C 30,-10 70,50 100,20 L 100,80 C 70,110 30,50 0,80 Z";
        private const string kClippedCornerShape = "M 10,0 H 90 L 100,10 V 40 L 90,50 H 10 L 0,40 V 10 Z";

        private readonly GeometryButton m_notched;
        private readonly GeometryButton m_disabled;
        private readonly NSTextField m_clickCount;
        private int m_clicks;

        public GeometryButtonPage ()
            : base("GeometryButton", "A button whose background is a path geometry instead of a rectangle. GeometryMarkup takes path mini-language, and TraceThickness outlines it. The shape is fitted to the button by GeometryStretch.")
        {
            m_notched = MakeButton(kNotchedShape, 150, 100, "Notched, traced");
            m_notched.TraceThickness = 1.0;
            m_notched.Activated += this.OnNotchedActivated;
            this.AddLabeledDemo(m_notched.ToolTip, m_notched);

            m_clickCount = ShowroomStyle.Understated("Not clicked yet");
            this.AddLabeledDemo("Clicks", m_clickCount);

            this.AddHeading("Curves, arcs and stretch");

            GeometryButton circle = MakeButton(kCircleShape, 110, 110, "Arc, Uniform");
            circle.GeometryStretch = ePathStretch.Uniform;
            circle.TraceThickness = 2.0;
            this.AddLabeledDemo(circle.ToolTip, circle);

            GeometryButton wave = MakeButton(kWaveShape, 160, 110, "Cubic curves, Fill");
            wave.GeometryStretch = ePathStretch.Fill;
            wave.TraceThickness = 2.0;
            this.AddLabeledDemo(wave.ToolTip, wave);

            foreach (ePathStretch stretch in new[] { ePathStretch.None, ePathStretch.Fill, ePathStretch.Uniform, ePathStretch.UniformToFill })
            {
                GeometryButton sized = MakeButton(kPlayShape, 80, 40, $"Stretch {stretch}, 80x40");
                sized.GeometryStretch = stretch;
                sized.TraceThickness = 1.0;
                this.AddLabeledDemo(sized.ToolTip, sized);
            }

            m_disabled = MakeButton(kClippedCornerShape, 220, 60, "Disabled, H V and L");
            m_disabled.GeometryStretch = ePathStretch.Fill;
            m_disabled.TraceThickness = 1.0;
            m_disabled.Enabled = false;
            this.AddLabeledDemo(m_disabled.ToolTip, m_disabled);

            this.AddCode(
@"var button = new GeometryButton
{
    GeometryMarkup = ""M 0,0 20,20 40,0 150,0 150,100 100,90 0,100 Z"",
    TraceThickness = 1.0,
    GeometryStretch = ePathStretch.UniformToFill,
    ToolTip = ""Notched"", // it draws no text, so VoiceOver reads it by this
};
button.Activated += this.OnButtonActivated;

// or, through a binding set
bindings.BindCommand(button, viewModel.PlayCommand);"
            );
        }

        protected override void OnTeardown ()
        {
            m_notched.Activated -= this.OnNotchedActivated;
        }

        private void OnNotchedActivated (object sender, EventArgs e)
        {
            ++m_clicks;
            m_clickCount.StringValue = m_clicks == 1 ? "Clicked once" : $"Clicked {m_clicks} times";
        }

        // A GeometryButton draws no text, so with no AccessibilityLabel of its own, its ToolTip is what VoiceOver reads it by.
        // Each one here carries its row's label as its ToolTip, and the row is labeled from it.
        private static GeometryButton MakeButton (string markup, double width, double height, string label)
        {
            var button = new GeometryButton { GeometryMarkup = markup, ToolTip = label };
            button.TranslatesAutoresizingMaskIntoConstraints = false;
            NSLayoutConstraint.ActivateConstraints(new[]
            {
                button.WidthAnchor.ConstraintEqualTo((nfloat)width),
                button.HeightAnchor.ConstraintEqualTo((nfloat)height),
            });
            return button;
        }
    }
}
