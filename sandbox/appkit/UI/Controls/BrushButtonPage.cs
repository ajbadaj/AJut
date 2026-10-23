namespace AJutShowRoomAppKit.UI.Controls
{
    using AJut.UX.Controls;
    using AppKit;

    public sealed class BrushButtonPage : ShowroomPage
    {
        public BrushButtonPage ()
            : base("BrushButton", "Buttons that pick their own colors per state. BrushButton takes background, foreground and border colors for normal, pointer over, pressed and disabled. AutoBrushButton derives all of them from one accent color, and keeps its text readable on it.")
        {
            this.AddHeading("BrushButton");
            this.AddLabeledDemo("Defaults", new BrushButton { Title = "Themed defaults" });
            this.AddLabeledDemo("Restyled", new BrushButton
            {
                Title = "Warning colors",
                NormalBackground = NSColor.SystemRed,
                NormalForeground = NSColor.White,
                PointerOverBackground = NSColor.SystemOrange,
                PointerOverForeground = NSColor.White,
                PressedBackground = NSColor.SystemBrown,
                PressedForeground = NSColor.White,
            });
            this.AddLabeledDemo("No border", new BrushButton { Title = "Borderless", NormalBorder = null, PointerOverBorder = null, PressedBorder = null });
            this.AddLabeledDemo("Disabled", new BrushButton { Title = "Disabled", Enabled = false });

            this.AddHeading("AutoBrushButton");
            this.AddLabeledDemo("System accent", new AutoBrushButton { Title = "Follows System Settings" });
            this.AddLabeledDemo("Green accent", new AutoBrushButton { Title = "Green", PropagatedAccentColor = NSColor.SystemGreen });
            this.AddLabeledDemo("Yellow accent", new AutoBrushButton { Title = "Dark text on light", PropagatedAccentColor = NSColor.SystemYellow });
            this.AddLabeledDemo("Disabled", new AutoBrushButton { Title = "Disabled", PropagatedAccentColor = NSColor.SystemPurple, Enabled = false });

            this.AddCode(
@"var save = new AutoBrushButton
{
    Title = ""Save"",
    PropagatedAccentColor = NSColor.SystemGreen,
};

var warning = new BrushButton
{
    Title = ""Delete"",
    NormalBackground = NSColor.SystemRed,
    NormalForeground = NSColor.White,
};"
            );
        }
    }
}
