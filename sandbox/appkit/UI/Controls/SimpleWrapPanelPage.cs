namespace AJutShowRoomAppKit.UI.Controls
{
    using System;
    using AJut.UX.Controls;
    using AJut.UX.Theming;
    using AppKit;
    using CoreAnimation;
    using CoreGraphics;

    /// <summary>
    /// The SimpleWrapPanel demo, as WinUI3's: uneven chips in a fixed-width panel, flowing and wrapping, with one that
    /// starts hidden so bringing it back exercises a subview joining the flow. A second panel, across the page, holds stock
    /// controls at their own size and rewraps as the window narrows.
    /// </summary>
    public sealed class SimpleWrapPanelPage : ShowroomPage
    {
        private const double kCornerRadius = 4.0;

        private readonly SimpleWrapPanel m_chips = new SimpleWrapPanel();
        private readonly BrushButton m_toggle;
        private NSView m_epsilon;

        public SimpleWrapPanelPage ()
            : base("SimpleWrapPanel", "Flows its subviews left to right at their own size and wraps to the next line when the next one would run past the width. HorizontalSpacing and VerticalSpacing gap them, and a hidden subview takes no place.")
        {
            const double kChipPanelWidth = 300.0;
            const string kHiddenChip = "Epsilon";
            const double kStockHorizontalSpacing = 12.0;
            const double kStockVerticalSpacing = 8.0;
            const double kSwatchSize = 24.0;

            this.AddHeading("Chips in a fixed width");
            this.AddText("Epsilon starts hidden: the button brings it back into the flow between Delta and Zeta. AppKit tells no parent when a subview hides or shows, so the button sets NeedsLayout on the panel.");

            m_toggle = new BrushButton { Title = "Show or hide Epsilon" };
            m_toggle.Activated += this.OnToggleEpsilon;
            this.AddDemo(m_toggle, eFillStackCrossAlignment.Start);

            string[] labels = { "Alpha", "Beta", "Gamma", "Delta", kHiddenChip, "Zeta", "Eta", "Theta", "Iota", "Kappa" };
            for (int index = 0; index < labels.Length; ++index)
            {
                var chip = new Chip(labels[index], index);
                if (labels[index] == kHiddenChip)
                {
                    chip.Hidden = true;
                    m_epsilon = chip;
                }

                m_chips.AddSubview(chip);
            }

            this.AddDemo(MakeFrame(m_chips, kChipPanelWidth), eFillStackCrossAlignment.Start);

            this.AddHeading("Stock controls across the page");
            this.AddText("Each control at its own size, spaced by its visible edges: push buttons, a checkbox, a switch, a pop-up button, a label, and a plain view given a 24 point frame. Narrow the window and the line wraps.");
            var stock = new SimpleWrapPanel { HorizontalSpacing = kStockHorizontalSpacing, VerticalSpacing = kStockVerticalSpacing };
            stock.AddSubview(new BrushButton { Title = "Push button" });
            stock.AddSubview(new BrushButton { Title = "A longer push button" });
            var checkbox = new ThemedCheckBox { Title = "Checkbox" };
            stock.AddSubview(checkbox);
            stock.AddSubview(new NSSwitch());
            var popUp = new ThemedPopUpButton();
            popUp.AddItems(new[] { "Pop-up button", "Second choice" });
            stock.AddSubview(popUp);
            NSTextField label = NSTextField.CreateLabel("A label");
            label.TextColor = ThemedColors.Get(ShowroomStyle.BodyText);
            stock.AddSubview(label);
            stock.AddSubview(new Swatch(kSwatchSize));
            this.AddDemo(stock);

            this.AddCode(
@"var panel = new SimpleWrapPanel { HorizontalSpacing = 8, VerticalSpacing = 6 };
panel.AddSubview(chip);

// AppKit tells no parent when a subview hides or shows, so say so
chip.Hidden = true;
panel.NeedsLayout = true;"
            );
        }

        protected override void OnTeardown ()
        {
            m_toggle.Activated -= this.OnToggleEpsilon;
        }

        // ===========[ Events ]================================================
        private void OnToggleEpsilon (object sender, EventArgs e)
        {
            m_epsilon.Hidden = !m_epsilon.Hidden;
            m_chips.NeedsLayout = true;
        }

        // ===========[ Helper Methods ]========================================
        // A bordered frame holding the panel at a fixed width, as WinUI3's page has, so the chips wrap whatever the window
        private static NSView MakeFrame (SimpleWrapPanel panel, double width)
        {
            const double kFramePadding = 8.0;
            const double kBorderThickness = 1.0;

            var frame = new ThemedView { BorderColorKey = ShowroomStyle.Divider, BorderThickness = kBorderThickness, CornerRadius = kCornerRadius };
            frame.TranslatesAutoresizingMaskIntoConstraints = false;
            panel.TranslatesAutoresizingMaskIntoConstraints = false;
            frame.AddSubview(panel);
            NSLayoutConstraint.ActivateConstraints(new[]
            {
                panel.WidthAnchor.ConstraintEqualTo((nfloat)width),
                panel.LeadingAnchor.ConstraintEqualTo(frame.LeadingAnchor, (nfloat)kFramePadding),
                panel.TrailingAnchor.ConstraintEqualTo(frame.TrailingAnchor, -(nfloat)kFramePadding),
                panel.TopAnchor.ConstraintEqualTo(frame.TopAnchor, (nfloat)kFramePadding),
                panel.BottomAnchor.ConstraintEqualTo(frame.BottomAnchor, -(nfloat)kFramePadding),
            });

            return frame;
        }

        // ===========[ Subclasses/structs ]====================================
        // A content-sized chip of uneven width, a label held inside by constraints, so the panel sizes it by its fitting
        //  size. Its tint is fixed, so it is set once rather than per appearance.
        private sealed class Chip : NSView
        {
            private readonly CGColor m_tint;

            public Chip (string text, int index)
            {
                const double kInsetX = 8.0;
                const double kInsetY = 4.0;

                // The same uneven teal tints as the UIKit page's chips: red steps with the chip's place, the rest is fixed
                const int kRedBase = 70;
                const int kRedStep = 22;
                const int kRedRange = 150;
                const double kGreen = 0x90 / 255.0;
                const double kBlue = 0xC8 / 255.0;
                const double kAlpha = 0x55 / 255.0;

                double red = (kRedBase + ((index * kRedStep) % kRedRange)) / 255.0;
                m_tint = NSColor.FromSrgb((nfloat)red, (nfloat)kGreen, (nfloat)kBlue, (nfloat)kAlpha).CGColor;
                this.WantsLayer = true;

                NSTextField label = NSTextField.CreateLabel(text);
                label.TextColor = NSColor.Label;
                label.TranslatesAutoresizingMaskIntoConstraints = false;
                this.AddSubview(label);
                NSLayoutConstraint.ActivateConstraints(new[]
                {
                    label.LeadingAnchor.ConstraintEqualTo(this.LeadingAnchor, (nfloat)kInsetX),
                    label.TrailingAnchor.ConstraintEqualTo(this.TrailingAnchor, -(nfloat)kInsetX),
                    label.TopAnchor.ConstraintEqualTo(this.TopAnchor, (nfloat)kInsetY),
                    label.BottomAnchor.ConstraintEqualTo(this.BottomAnchor, -(nfloat)kInsetY),
                });

                // Starts at the size it holds, so its label's constraints never meet an empty frame before the panel places it
                this.SetFrameSize(this.FittingSize);
            }

            public override bool WantsUpdateLayer => true;

            public override void UpdateLayer ()
            {
                CALayer layer = this.Layer;
                if (layer != null)
                {
                    layer.BackgroundColor = m_tint;
                    layer.CornerRadius = (nfloat)kCornerRadius;
                }
            }
        }

        // A plain view with no size of its own but its frame, which the panel keeps
        private sealed class Swatch : NSView
        {
            public Swatch (double size) : base(new CGRect(0, 0, (nfloat)size, (nfloat)size))
            {
                this.WantsLayer = true;
            }

            public override bool WantsUpdateLayer => true;

            public override void UpdateLayer ()
            {
                CALayer layer = this.Layer;
                if (layer != null)
                {
                    layer.BackgroundColor = NSColor.SystemTeal.CGColor;
                    layer.CornerRadius = (nfloat)kCornerRadius;
                }
            }
        }
    }
}
