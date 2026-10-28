namespace AJutShowRoomUIKit.UI.Controls
{
    using System;
    using AJut.UX.Controls;
    using UIKit;

    public sealed class NumericEditorPage : ShowroomPageController
    {
        private readonly NumericEditor m_intEditor = new NumericEditor { Value = 5 };
        private readonly NumericEditor m_boundedEditor = new NumericEditor { Value = 2.5f, Minimum = 0, Maximum = 10, Nudge = 0.5, SmallNudge = 0.1, BigNudge = 2 };
        private readonly NumericEditor m_fixOnCommitEditor = new NumericEditor { Value = 50.0, Minimum = 0, Maximum = 100, OutOfBoundsResponse = eOutOfBoundsResponse.FixOnCommit, LabelText = "%" };
        private readonly NumericEditor m_enforcedEditor = new NumericEditor { EnforceNumericType = typeof(int), Value = "42" };
        private readonly NumericEditor m_readOnlyEditor = new NumericEditor { Value = 7, IsReadOnly = true };
        private readonly UISwitch m_readOnlySwitch = new UISwitch { On = true };
        private readonly UILabel m_liveValues = new UILabel { Lines = 0 };

        public NumericEditorPage () : base("NumericEditor")
        {
        }

        public override void ViewWillAppear (bool animated)
        {
            base.ViewWillAppear(animated);
            foreach (NumericEditor editor in this.Editors())
            {
                editor.ValueChanged -= this.Editor_OnValueChanged;
                editor.ValueChanged += this.Editor_OnValueChanged;
            }

            m_readOnlySwitch.ValueChanged -= this.ReadOnlySwitch_OnValueChanged;
            m_readOnlySwitch.ValueChanged += this.ReadOnlySwitch_OnValueChanged;
            this.ShowLiveValues();
        }

        public override void ViewDidDisappear (bool animated)
        {
            base.ViewDidDisappear(animated);
            foreach (NumericEditor editor in this.Editors())
            {
                editor.ValueChanged -= this.Editor_OnValueChanged;
            }

            m_readOnlySwitch.ValueChanged -= this.ReadOnlySwitch_OnValueChanged;
        }

        protected override void BuildPage ()
        {
            const float kEditorWidth = 220f;
            const float kEditorHeight = 36f;

            this.AddHeading("NumericEditor");
            this.AddText("A numeric field that keeps its value's type: an int stays an int, a float a float. The minus and plus buttons step by Nudge, and repeat while held; with a hardware keyboard, Shift steps by SmallNudge, Command by BigNudge, and the arrow keys nudge too.");

            this.AddText("An int, unbounded. Shift-nudging an int steps by 1, never 0.");
            this.AddDemo(Sized(m_intEditor));

            this.AddText("A float bounded to 0 to 10. Text past the bounds is flagged, and the value is capped.");
            this.AddDemo(Sized(m_boundedEditor));

            this.AddText("A double bounded to 0 to 100, FixOnCommit: out of bounds text snaps to the bound when the edit ends.");
            this.AddDemo(Sized(m_fixOnCommitEditor));

            this.AddText("EnforceNumericType int, given the text \"42\", the way a value read from a file or a setting arrives: it is kept as an int, and typing a fraction is refused.");
            this.AddDemo(Sized(m_enforcedEditor));

            this.AddDemo(m_liveValues);

            this.AddText("IsReadOnly shows the value without letting it be typed or nudged: the number dims, the buttons are disabled, and the arrow keys do nothing. The switch turns it off and on.");
            this.AddDemoRow(Sized(m_readOnlyEditor), m_readOnlySwitch);
            this.AddCode(@"
var editor = new NumericEditor
{
    Value = 2.5f,
    Minimum = 0,
    Maximum = 10,
    Nudge = 0.5,
};

// Kept as an int however the value arrives, text included:
var count = new NumericEditor { EnforceNumericType = typeof(int), Value = ""42"" };

// Shown, not edited:
var total = new NumericEditor { Value = 7, IsReadOnly = true };

// Bound to a model instead:
bindings.BindValue(editor, model, nameof(Model.Ratio), () => model.Ratio, value => model.Ratio = value);");

            UIView Sized (NumericEditor editor)
            {
                editor.TranslatesAutoresizingMaskIntoConstraints = false;
                editor.WidthAnchor.ConstraintEqualTo(kEditorWidth).Active = true;
                editor.HeightAnchor.ConstraintEqualTo(kEditorHeight).Active = true;
                return editor;
            }
        }

        private NumericEditor[] Editors () => new[] { m_intEditor, m_boundedEditor, m_fixOnCommitEditor, m_enforcedEditor };

        private void Editor_OnValueChanged (object sender, EventArgs e)
        {
            this.ShowLiveValues();
        }

        private void ReadOnlySwitch_OnValueChanged (object sender, EventArgs e)
        {
            m_readOnlyEditor.IsReadOnly = m_readOnlySwitch.On;
        }

        private void ShowLiveValues ()
        {
            m_liveValues.Text = $"Values: {Describe(m_intEditor)}, {Describe(m_boundedEditor)}, {Describe(m_fixOnCommitEditor)}, {Describe(m_enforcedEditor)}";

            static string Describe (NumericEditor editor) => $"{editor.Value} ({editor.Value?.GetType().Name})";
        }
    }
}
