namespace AJutShowRoomUIKit.UI.Controls
{
    using AJut.UX.Controls;
    using UIKit;

    public sealed class BrushButtonPage : ShowroomPageController
    {
        public BrushButtonPage () : base("Brush Buttons")
        {
        }

        protected override void BuildPage ()
        {
            this.AddHeading("AutoBrushButton");
            this.AddText("One accent color, and the rest is derived: a readable foreground, a visible border, and the pressed, pointer over and disabled variants. With no accent set it follows the view's tint color.");
            this.AddDemoRow(
                MakeAutoButton("Tint", null),
                MakeAutoButton("Kick", UIColor.FromRGB(0x18, 0x6E, 0x11)),
                MakeAutoButton("Delete", UIColor.SystemRed),
                MakeAutoButton("Pale", UIColor.FromRGB(0xF4, 0xE1, 0x9C))
            );

            AutoBrushButton disabled = MakeAutoButton("Disabled", UIColor.SystemIndigo);
            disabled.Enabled = false;
            this.AddDemo(disabled);
            this.AddCode(@"
var button = new AutoBrushButton
{
    PropagatedAccentColor = UIColor.SystemRed,
};
button.SetTitle(""Delete"", UIControlState.Normal);");

            this.AddHeading("BrushButton");
            this.AddText("Every state's background, foreground and border is its own property. An untouched BrushButton looks like a bordered system button; override only the slots you care about.");

            var plain = new BrushButton();
            plain.SetTitle("Default", UIControlState.Normal);

            var red = new BrushButton
            {
                NormalBackground = UIColor.FromRGB(0xC0, 0x39, 0x2B),
                PointerOverBackground = UIColor.FromRGB(0xE0, 0x4A, 0x3A),
                PressedBackground = UIColor.FromRGB(0x96, 0x28, 0x1B),
                NormalForeground = UIColor.White,
                PointerOverForeground = UIColor.White,
                PressedForeground = UIColor.White,
            };
            red.SetTitle("Stays red", UIControlState.Normal);

            this.AddDemoRow(plain, red);
            this.AddCode(@"
var red = new BrushButton
{
    NormalBackground = UIColor.FromRGB(0xC0, 0x39, 0x2B),
    PointerOverBackground = UIColor.FromRGB(0xE0, 0x4A, 0x3A),
    PressedBackground = UIColor.FromRGB(0x96, 0x28, 0x1B),
    NormalForeground = UIColor.White,
    PointerOverForeground = UIColor.White,
    PressedForeground = UIColor.White,
};");
        }

        private static AutoBrushButton MakeAutoButton (string title, UIColor accent)
        {
            var button = new AutoBrushButton { PropagatedAccentColor = accent };
            button.SetTitle(title, UIControlState.Normal);
            return button;
        }
    }
}
