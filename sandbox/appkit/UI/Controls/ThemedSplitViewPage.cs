namespace AJutShowRoomAppKit.UI.Controls
{
    using AJut.UX.Controls;
    using AJut.UX.Theming;
    using AppKit;

    public sealed class ThemedSplitViewPage : ShowroomPage
    {
        private const double kDemoHeight = 180.0;

        public ThemedSplitViewPage ()
            : base("ThemedSplitView", "Drag-to-resize panes, the role GridSplitter fills on WinUI3. It builds on AppKit's own split view, adding a themed divider, a minimum pane size, and a starting split ratio.")
        {
            this.AddHeading("Side by side, starting at a third");
            ThemedSplitView sideBySide = MakeSplit(isVertical: true, ratio: 1.0 / 3.0, "Sidebar", "Contents");
            this.AddDemo(sideBySide);

            this.AddHeading("Stacked, with a thicker divider and a 60pt minimum");
            ThemedSplitView stacked = MakeSplit(isVertical: false, ratio: 0.5, "Top", "Bottom");
            stacked.Thickness = 4.0;
            stacked.MinPanelSize = 60.0;
            this.AddDemo(stacked);

            this.AddCode(
@"var split = new ThemedSplitView(isVertical: true)
{
    InitialSplitRatio = 1.0 / 3.0,
    MinPanelSize = 30.0,
};
split.AddArrangedSubview(sidebar);
split.AddArrangedSubview(contents);

// Keep the sidebar its size when the window resizes
split.SetHoldingPriority(260, 0);"
            );
        }

        private static ThemedSplitView MakeSplit (bool isVertical, double ratio, string firstLabel, string secondLabel)
        {
            var split = new ThemedSplitView(isVertical) { InitialSplitRatio = ratio };
            split.TranslatesAutoresizingMaskIntoConstraints = false;
            split.HeightAnchor.ConstraintEqualTo((nfloat)kDemoHeight).Active = true;
            split.AddArrangedSubview(MakePane(firstLabel));
            split.AddArrangedSubview(MakePane(secondLabel));
            return split;
        }

        private static NSView MakePane (string label)
        {
            var pane = new ThemedView { BackgroundColorKey = ShowroomStyle.SidebarBackground };
            NSTextField text = ShowroomStyle.Body(label);
            text.TranslatesAutoresizingMaskIntoConstraints = false;
            pane.AddSubview(text);
            NSLayoutConstraint.ActivateConstraints(new[]
            {
                text.CenterXAnchor.ConstraintEqualTo(pane.CenterXAnchor),
                text.CenterYAnchor.ConstraintEqualTo(pane.CenterYAnchor),
            });
            return pane;
        }
    }
}
