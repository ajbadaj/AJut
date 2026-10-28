namespace AJutShowRoomUIKit.UI.Controls
{
    using System;
    using AJut.UX;
    using AJut.UX.Controls;
    using AJut.UX.Helpers;
    using UIKit;

    public sealed class ColorEditIngressControlPage : ShowroomPageController
    {
        private readonly ColorEditIngressControl m_editor = new ColorEditIngressControl { EditColor = new ColorArgb(0xC0, 0x33, 0x99, 0xCC) };
        private readonly UILabel m_liveValue = new UILabel { Lines = 0 };
        private readonly UILabel m_lastEdit = new UILabel { Lines = 0, Text = "No edit completed yet" };

        public ColorEditIngressControlPage () : base("ColorEditIngressControl")
        {
        }

        public override void ViewWillAppear (bool animated)
        {
            base.ViewWillAppear(animated);
            m_editor.ValueChanged -= this.Editor_OnValueChanged;
            m_editor.ValueChanged += this.Editor_OnValueChanged;
            m_editor.UserEditComplete -= this.Editor_OnUserEditComplete;
            m_editor.UserEditComplete += this.Editor_OnUserEditComplete;
        }

        public override void ViewDidDisappear (bool animated)
        {
            base.ViewDidDisappear(animated);
            m_editor.ValueChanged -= this.Editor_OnValueChanged;
            m_editor.UserEditComplete -= this.Editor_OnUserEditComplete;
        }

        protected override void BuildPage ()
        {
            this.AddHeading("ColorEditIngressControl");
            this.AddText("A swatch that opens the system color picker: a popover on iPad, a sheet on iPhone. The color updates live while the picker is open (ValueChanged), and UserEditComplete fires once when it closes, if the color changed.");
            this.AddDemo(m_editor);
            this.AddDemo(m_liveValue);
            this.AddDemo(m_lastEdit);
            this.AddCode(@"
var editor = new ColorEditIngressControl { EditColor = new ColorArgb(0xC0, 0x33, 0x99, 0xCC) };  // ColorArgb on every stack
editor.ValueChanged += this.Editor_OnValueChanged;         // every live change
editor.UserEditComplete += this.Editor_OnUserEditComplete; // once, when the picker closes");

            this.ShowLiveValue();
        }

        private void Editor_OnValueChanged (object sender, EventArgs e)
        {
            this.ShowLiveValue();
        }

        private void Editor_OnUserEditComplete (object sender, UserEditAppliedEventArgs e)
        {
            m_lastEdit.Text = $"Last edit: {ToHex((ColorArgb)e.OldValue)} -> {ToHex((ColorArgb)e.NewValue)}";
        }

        private void ShowLiveValue ()
        {
            m_liveValue.Text = $"EditColor: {ToHex(m_editor.EditColor)}";
        }

        private static string ToHex (ColorArgb color) => AJutColorHelper.GetSmallestHexString(color.A, color.R, color.G, color.B);
    }
}
