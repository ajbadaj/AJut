namespace AJutShowRoomAppKit.UI.Controls
{
    using System;
    using AJut.UX.Controls;
    using AJut.UX.Theming;
    using AppKit;
    using CoreGraphics;

    // The PageStack demos, the WinUI3 page's three: horizontal with dots, vertical, and a few pages with no dots. The pages
    // are stocked from code because they are demo data, not layout, in translucent tints of their own like the WinUI3
    // page's.
    public sealed class PageStackPage : ShowroomPage
    {
        private readonly PageStack m_horizontal;
        private readonly NSTextField m_selectedIndexLabel;

        public PageStackPage ()
            : base("PageStack", "BumpStack's sibling for paging: one page shows at a time, and the neighbor pages peek in, faded, past the page buttons. A click on a page button or a dot runs an animated transition. With the stack focused, the arrow keys, Home and End page too. The dot strip shows DotReach dots each side of the current page, plus a smaller, faded edge dot while there are more pages past them; clicking an edge dot goes just past the window.")
        {
            // 1. Horizontal, 20 pages, with dots
            m_horizontal = MakeStack(NSUserInterfaceLayoutOrientation.Horizontal, 420.0, 180.0);
            m_horizontal.ShowDots = true;
            m_horizontal.DotReach = 3;
            for (int index = 0; index < 20; ++index)
            {
                m_horizontal.Children.Add(MakePage($"Page {index + 1}", index));
            }

            m_horizontal.SelectedIndexChanged += this.OnHorizontalSelectedIndexChanged;
            this.AddLabeledDemo("Horizontal, 20 pages, ShowDots with DotReach = 3", m_horizontal);
            m_selectedIndexLabel = ShowroomStyle.Body(String.Empty);
            this.UpdateSelectedIndexLabel();
            this.AddLabeledDemo("SelectedIndexChanged", m_selectedIndexLabel);

            // 2. Vertical, with a slower transition
            PageStack vertical = MakeStack(NSUserInterfaceLayoutOrientation.Vertical, 260.0, 240.0);
            vertical.ShowDots = true;
            vertical.TransitionDuration = TimeSpan.FromMilliseconds(600);
            for (int index = 0; index < 6; ++index)
            {
                vertical.Children.Add(MakePage($"Card {index + 1}", index));
            }

            this.AddLabeledDemo("Vertical, 6 pages, ShowDots, a slower TransitionDuration", vertical);

            // 3. A few pages, no dots, starting on the last: the index is set before the pages are added
            PageStack few = MakeStack(NSUserInterfaceLayoutOrientation.Horizontal, 420.0, 120.0);
            few.SelectedIndex = 2;
            string[] words = { "First", "Second", "Third" };
            for (int index = 0; index < words.Length; ++index)
            {
                few.Children.Add(MakePage(words[index], index));
            }

            this.AddLabeledDemo("Three pages, no dots, starting on the last", few);

            this.AddCode(
@"var stack = new PageStack { ShowDots = true, DotReach = 3 };
stack.Children.Add(page); // each page is pinned to the page area
stack.SelectedIndex = 4;  // runs a transition to it
stack.SelectedIndexChanged += this.OnPageChanged;"
            );
        }

        protected override void OnTeardown ()
        {
            m_horizontal.SelectedIndexChanged -= this.OnHorizontalSelectedIndexChanged;
        }

        private void OnHorizontalSelectedIndexChanged (object sender, EventArgs e) => this.UpdateSelectedIndexLabel();

        private void UpdateSelectedIndexLabel ()
        {
            m_selectedIndexLabel.StringValue = $"SelectedIndex: {m_horizontal.SelectedIndex}";
        }

        private static PageStack MakeStack (NSUserInterfaceLayoutOrientation orientation, double width, double height)
        {
            var stack = new PageStack
            {
                Orientation = orientation,
                BorderColorKey = ThemedColorKeys.PageStack_ButtonBorder,
                BorderThickness = 1.0,
                TranslatesAutoresizingMaskIntoConstraints = false,
            };

            stack.WidthAnchor.ConstraintEqualTo((nfloat)width).Active = true;
            stack.HeightAnchor.ConstraintEqualTo((nfloat)height).Active = true;
            return stack;
        }

        // A page: a rounded tint with its name in the middle
        private static NSView MakePage (string text, int index)
        {
            const byte kTintAlpha = 0x88;
            const byte kTintGreen = 0x90;
            const byte kTintBlue = 0xC8;
            byte tint = (byte)(70 + ((index * 37) % 150));

            var page = new NSView { WantsLayer = true, Identifier = text };
            page.Layer.BackgroundColor = new CGColor((nfloat)(tint / 255.0), (nfloat)(kTintGreen / 255.0), (nfloat)(kTintBlue / 255.0), (nfloat)(kTintAlpha / 255.0));
            page.Layer.CornerRadius = 6;

            NSTextField label = ShowroomStyle.Body(text);
            label.TranslatesAutoresizingMaskIntoConstraints = false;
            page.AddSubview(label);
            NSLayoutConstraint.ActivateConstraints(new[]
            {
                label.CenterXAnchor.ConstraintEqualTo(page.CenterXAnchor),
                label.CenterYAnchor.ConstraintEqualTo(page.CenterYAnchor),
            });

            return page;
        }
    }
}
