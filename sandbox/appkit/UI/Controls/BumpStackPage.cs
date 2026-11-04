namespace AJutShowRoomAppKit.UI.Controls
{
    using System;
    using AJut.UX.Controls;
    using AJut.UX.Theming;
    using AppKit;
    using CoreGraphics;

    // The BumpStack demos, the WinUI3 page's three: horizontal, vertical, and clip only. The chips are stocked from code
    // because they are demo data, not layout, in translucent tints of their own like the WinUI3 page's. Along a vertical
    // stack a chip has a height of its own and is stretched across it.
    public sealed class BumpStackPage : ShowroomPage
    {
        private const double kChipMargin = 3.0;

        private readonly BumpStack m_horizontal;
        private readonly BrushButton m_bringEightIntoView;

        public BumpStackPage ()
            : base("BumpStack", "A scroll container with a chevron bump button at each end instead of a scroll bar. The buttons appear only when the content overflows, disable at each end, and repeat while held, and the content is inset so scrolling all the way parks a button over empty space. A mouse wheel scrolls a horizontal stack.")
        {
            string[] words = { "One", "Two", "Three", "Four", "Five", "Six", "Seven", "Eight", "Nine", "Ten" };

            // 1. Horizontal, narrower than its chips
            m_horizontal = MakeStack(NSUserInterfaceLayoutOrientation.Horizontal, 320.0, 44.0);
            BumpStack clip = MakeStack(NSUserInterfaceLayoutOrientation.Horizontal, 320.0, 44.0);
            clip.ScrollingEnabled = false;
            for (int index = 0; index < words.Length; ++index)
            {
                m_horizontal.Children.Add(MakeChip(words[index], index, isVertical: false));
                clip.Children.Add(MakeChip(words[index], index, isVertical: false));
            }

            this.AddLabeledDemo("Horizontal (resize narrower to see more)", m_horizontal);

            m_bringEightIntoView = new BrushButton { Title = "Bring Eight into view" };
            m_bringEightIntoView.Activated += this.OnBringEightIntoViewActivated;
            this.AddLabeledDemo("ScrollFirstElementIntoView", m_bringEightIntoView);

            // 2. Vertical
            BumpStack vertical = MakeStack(NSUserInterfaceLayoutOrientation.Vertical, 160.0, 130.0);
            for (int index = 0; index < 8; ++index)
            {
                vertical.Children.Add(MakeChip($"Row {index + 1}", index, isVertical: true));
            }

            this.AddLabeledDemo("Vertical", vertical);

            // 3. Clip only: no buttons, the content just clips
            this.AddLabeledDemo("ScrollingEnabled = false (clip only)", clip);

            this.AddCode(
@"var stack = new BumpStack
{
    BorderColorKey = ThemedColorKeys.BumpStack_ButtonBorder,
    BorderThickness = 1.0,
};
stack.Children.Add(chip); // each child needs a width of its own

var vertical = new BumpStack { Orientation = NSUserInterfaceLayoutOrientation.Vertical };
var clip = new BumpStack { ScrollingEnabled = false };"
            );
        }

        protected override void OnTeardown ()
        {
            m_bringEightIntoView.Activated -= this.OnBringEightIntoViewActivated;
        }

        private void OnBringEightIntoViewActivated (object sender, EventArgs e)
        {
            m_horizontal.ScrollFirstElementIntoView(IsEight);
        }

        private static bool IsEight (NSView chip) => chip.Identifier == "Eight";

        private static BumpStack MakeStack (NSUserInterfaceLayoutOrientation orientation, double width, double height)
        {
            var stack = new BumpStack
            {
                Orientation = orientation,
                BorderColorKey = ThemedColorKeys.BumpStack_ButtonBorder,
                BorderThickness = 1.0,
                TranslatesAutoresizingMaskIntoConstraints = false,
            };

            stack.WidthAnchor.ConstraintEqualTo((nfloat)width).Active = true;
            stack.HeightAnchor.ConstraintEqualTo((nfloat)height).Active = true;
            return stack;
        }

        // A chip: a rounded tint with its name in the middle, inset by a margin, at a fixed length along the stack
        private static NSView MakeChip (string text, int index, bool isVertical)
        {
            const byte kTintAlpha = 0x55;
            const byte kTintGreen = 0x90;
            const byte kTintBlue = 0xC8;

            const double kChipLength = 70.0;
            const double kRowHeight = 24.0;
            byte tint = (byte)(70 + ((index * 22) % 150));

            var chip = new NSView { Identifier = text, TranslatesAutoresizingMaskIntoConstraints = false };
            var fill = new NSView { WantsLayer = true, TranslatesAutoresizingMaskIntoConstraints = false };
            fill.Layer.BackgroundColor = new CGColor((nfloat)(tint / 255.0), (nfloat)(kTintGreen / 255.0), (nfloat)(kTintBlue / 255.0), (nfloat)(kTintAlpha / 255.0));
            fill.Layer.CornerRadius = 4;

            NSTextField label = ShowroomStyle.Body(text);
            label.TranslatesAutoresizingMaskIntoConstraints = false;
            fill.AddSubview(label);
            chip.AddSubview(fill);

            NSLayoutConstraint.ActivateConstraints(new[]
            {
                fill.TopAnchor.ConstraintEqualTo(chip.TopAnchor, (nfloat)kChipMargin),
                fill.BottomAnchor.ConstraintEqualTo(chip.BottomAnchor, (nfloat)(-kChipMargin)),
                fill.LeadingAnchor.ConstraintEqualTo(chip.LeadingAnchor, (nfloat)kChipMargin),
                fill.TrailingAnchor.ConstraintEqualTo(chip.TrailingAnchor, (nfloat)(-kChipMargin)),
                label.CenterXAnchor.ConstraintEqualTo(fill.CenterXAnchor),
                label.CenterYAnchor.ConstraintEqualTo(fill.CenterYAnchor),
                isVertical
                    ? chip.HeightAnchor.ConstraintEqualTo((nfloat)(kRowHeight + (2.0 * kChipMargin)))
                    : chip.WidthAnchor.ConstraintEqualTo((nfloat)(kChipLength + (2.0 * kChipMargin))),
            });

            return chip;
        }
    }
}
