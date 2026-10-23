namespace AJutShowRoomAppKit.UI.Controls
{
    using System;
    using System.Collections.ObjectModel;
    using AJut.UX.Controls;
    using AJut.UX.Theming;
    using AppKit;

    public sealed class ToggleStripPage : ShowroomPage
    {
        private static readonly string[] kSizes = new[] { "Small", "Medium", "Large" };
        private static readonly string[] kManyItems = new[] { "Alpha", "Bravo", "Charlie", "Delta", "Echo", "Foxtrot", "Golf", "Hotel", "India", "Juliett" };

        private readonly ToggleStrip m_single;
        private readonly NSTextField m_singleSelection;
        private readonly ObservableCollection<string> m_growingItems = new ObservableCollection<string> { "One", "Two" };
        private readonly NSButton m_addItem;

        public ToggleStripPage ()
            : base("ToggleStrip", "Mutually exclusive options, laid out as a strip of toggle buttons. Use it instead of a combo box for two to four choices. AllowMultiSelect and AllowNoSelection loosen the rules, and OverflowBehavior decides what happens when the items do not fit.")
        {
            m_single = new ToggleStrip { ItemsSource = kSizes };
            m_single.SelectionChanged += this.OnSingleSelectionChanged;
            this.AddLabeledDemo("Single select", m_single);
            m_singleSelection = ShowroomStyle.Understated(String.Empty);
            this.AddLabeledDemo("Selected", m_singleSelection);

            this.AddLabeledDemo("Multi select", new ToggleStrip { ItemsSource = kSizes, AllowMultiSelect = true });
            this.AddLabeledDemo("No selection allowed", new ToggleStrip { ItemsSource = kSizes, AllowNoSelection = true });
            this.AddLabeledDemo("Uniform size", new ToggleStrip { ItemsSource = new[] { "A", "Wide one", "B" }, EnsureUniformSize = true });

            this.AddHeading("Overflow");
            this.AddText("Drag the divider to change how much room the strips have.");
            this.AddDemo(MakeOverflowSplit());

            this.AddHeading("Vertical");
            this.AddLabeledDemo("Single select", new ToggleStrip { ItemsSource = kSizes, Orientation = NSUserInterfaceLayoutOrientation.Vertical });
            this.AddLabeledDemo("OverflowPopup, 90pt tall", MakeShortVerticalStrip());

            this.AddHeading("Following its source");
            this.AddLabeledDemo("Observable source", new ToggleStrip { ItemsSource = m_growingItems });
            m_addItem = new NSButton { Title = "Add an item" };
            m_addItem.BezelStyle = NSBezelStyle.Rounded;
            m_addItem.Activated += this.OnAddItemActivated;
            this.AddLabeledDemo(String.Empty, m_addItem);

            this.AddCode(
@"var sizes = new ToggleStrip
{
    ItemsSource = new[] { ""Small"", ""Medium"", ""Large"" },
    OverflowBehavior = eToggleStripOverflow.OverflowPopup,
    Orientation = NSUserInterfaceLayoutOrientation.Vertical, // or Horizontal, the default
};
sizes.SelectionChanged += this.OnSizeChanged;

// or, through a binding set
bindings.BindSelectedItem(sizes, viewModel, nameof(ViewModel.Size), () => viewModel.Size, v => viewModel.Size = (string)v);"
            );

            this.RefreshSingleSelection();
        }

        protected override void OnTeardown ()
        {
            m_single.SelectionChanged -= this.OnSingleSelectionChanged;
            m_addItem.Activated -= this.OnAddItemActivated;
        }

        private void OnSingleSelectionChanged (object sender, ToggleStrip.ToggleStripSelectionChangedEventArgs e)
        {
            this.RefreshSingleSelection();
        }

        private void OnAddItemActivated (object sender, EventArgs e)
        {
            m_growingItems.Insert(1, $"New {m_growingItems.Count + 1}");
        }

        private void RefreshSingleSelection ()
        {
            m_singleSelection.StringValue = m_single.SelectedItem?.ToString() ?? "(none)";
        }

        // The overflow strips fill the left pane of a split, so dragging the divider is what squeezes them. A fixed
        // width strip only ever shows one amount of overflow, and the window cannot be made narrow enough to show more.
        private static ThemedSplitView MakeOverflowSplit ()
        {
            const double kSplitHeight = 200.0;
            const double kStartRatio = 0.45;
            const double kPanePadding = 8.0;
            const double kRowSpacing = 6.0;

            var padding = new NSEdgeInsets((nfloat)kPanePadding, (nfloat)kPanePadding, (nfloat)kPanePadding, (nfloat)kPanePadding);
            var strips = new FillStack(NSUserInterfaceLayoutOrientation.Vertical, kRowSpacing) { Padding = padding };
            foreach (eToggleStripOverflow overflow in new[] { eToggleStripOverflow.Clip, eToggleStripOverflow.Scroll, eToggleStripOverflow.OverflowPopup })
            {
                strips.Add(ShowroomStyle.Understated(overflow.ToString()));
                strips.Add(new ToggleStrip { ItemsSource = kManyItems, OverflowBehavior = overflow });
            }

            var stripsPane = new ThemedView { BackgroundColorKey = ShowroomStyle.SidebarBackground };
            PinToEdges(strips, stripsPane);

            var hint = new FillStack(NSUserInterfaceLayoutOrientation.Vertical) { Padding = padding };
            hint.Add(ShowroomStyle.Understated("The divider is the left edge of this pane."));
            var restPane = new ThemedView();
            PinToEdges(hint, restPane);

            var split = new ThemedSplitView(isVertical: true) { InitialSplitRatio = kStartRatio };
            split.TranslatesAutoresizingMaskIntoConstraints = false;
            split.HeightAnchor.ConstraintEqualTo((nfloat)kSplitHeight).Active = true;
            split.AddArrangedSubview(stripsPane);
            split.AddArrangedSubview(restPane);
            return split;
        }

        private static void PinToEdges (NSView child, NSView parent)
        {
            child.TranslatesAutoresizingMaskIntoConstraints = false;
            parent.AddSubview(child);
            NSLayoutConstraint.ActivateConstraints(new[]
            {
                child.TopAnchor.ConstraintEqualTo(parent.TopAnchor),
                child.BottomAnchor.ConstraintEqualTo(parent.BottomAnchor),
                child.LeadingAnchor.ConstraintEqualTo(parent.LeadingAnchor),
                child.TrailingAnchor.ConstraintEqualTo(parent.TrailingAnchor),
            });
        }

        private static ToggleStrip MakeShortVerticalStrip ()
        {
            const double kShortHeight = 90.0;

            var strip = new ToggleStrip
            {
                ItemsSource = kManyItems,
                OverflowBehavior = eToggleStripOverflow.OverflowPopup,
                Orientation = NSUserInterfaceLayoutOrientation.Vertical,
            };
            strip.TranslatesAutoresizingMaskIntoConstraints = false;
            strip.HeightAnchor.ConstraintEqualTo((nfloat)kShortHeight).Active = true;
            return strip;
        }
    }
}
