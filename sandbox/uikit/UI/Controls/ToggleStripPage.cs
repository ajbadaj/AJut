namespace AJutShowRoomUIKit.UI.Controls
{
    using System;
    using System.Collections.ObjectModel;
    using System.Linq;
    using AJut.UX.Controls;
    using UIKit;

    public sealed class ToggleStripPage : ShowroomPageController
    {
        private readonly ObservableCollection<string> m_sizes = new ObservableCollection<string> { "Small", "Medium", "Large" };
        private readonly ToggleStrip m_single = new ToggleStrip();
        private readonly ToggleStrip m_multi = new ToggleStrip { AllowMultiSelect = true, AllowNoSelection = true };
        private readonly ToggleStrip m_overflow = new ToggleStrip { OverflowBehavior = eToggleStripOverflow.OverflowPopup };
        private readonly ToggleStrip m_scrolling = new ToggleStrip { OverflowBehavior = eToggleStripOverflow.Scroll };
        private readonly ToggleStrip m_vertical = new ToggleStrip { Orientation = UILayoutConstraintAxis.Vertical };
        private readonly ToggleStrip m_verticalOverflow = new ToggleStrip { Orientation = UILayoutConstraintAxis.Vertical, OverflowBehavior = eToggleStripOverflow.OverflowPopup };
        private readonly UILabel m_report = new UILabel { Lines = 0, Text = "No change yet" };
        private readonly UIButton m_addSize = UIButton.FromType(UIButtonType.System);

        public ToggleStripPage () : base("ToggleStrip")
        {
            m_single.ItemsSource = m_sizes;
            m_multi.ItemsSource = new[] { "Bold", "Italic", "Underline", "Strikethrough" };
            m_overflow.ItemsSource = new[] { "January", "February", "March", "April", "May", "June", "July", "August" };
            m_scrolling.ItemsSource = new[] { "January", "February", "March", "April", "May", "June", "July", "August" };
            m_vertical.ItemsSource = new[] { "Top", "Middle", "Bottom" };
            m_verticalOverflow.ItemsSource = new[] { "Mercury", "Venus", "Earth", "Mars", "Jupiter", "Saturn", "Uranus", "Neptune" };
            m_addSize.SetTitle("Add a size to the source", UIControlState.Normal);
        }

        public override void ViewWillAppear (bool animated)
        {
            base.ViewWillAppear(animated);
            m_single.SelectionChanged -= this.Strip_OnSelectionChanged;
            m_single.SelectionChanged += this.Strip_OnSelectionChanged;
            m_multi.SelectionChanged -= this.Strip_OnSelectionChanged;
            m_multi.SelectionChanged += this.Strip_OnSelectionChanged;
            m_addSize.TouchUpInside -= this.AddSize_OnTouchUpInside;
            m_addSize.TouchUpInside += this.AddSize_OnTouchUpInside;
        }

        public override void ViewDidDisappear (bool animated)
        {
            base.ViewDidDisappear(animated);
            m_single.SelectionChanged -= this.Strip_OnSelectionChanged;
            m_multi.SelectionChanged -= this.Strip_OnSelectionChanged;
            m_addSize.TouchUpInside -= this.AddSize_OnTouchUpInside;
        }

        protected override void BuildPage ()
        {
            const float kOverflowDemoWidth = 260f;
            const float kVerticalDemoWidth = 140f;
            const float kVerticalOverflowDemoHeight = 170f;

            this.AddHeading("ToggleStrip");
            this.AddText("A row of joined toggle buttons. Single select by default; the selection rules (one selected, the swap reported once) are AJut.UX's, shared with every stack.");
            this.AddDemo(m_single);
            this.AddDemo(m_addSize);
            this.AddCode(@"
var strip = new ToggleStrip { ItemsSource = sizes };   // an ObservableCollection is followed as it changes
strip.SelectionChanged += this.Strip_OnSelectionChanged;");

            this.AddHeading("Multi select");
            this.AddText("AllowMultiSelect, with AllowNoSelection so the last one can be turned off too.");
            this.AddDemo(m_multi);

            this.AddHeading("Overflow");
            this.AddText("OverflowPopup: what does not fit moves into a menu on the trailing button, and a selected item is kept in the strip.");
            m_overflow.TranslatesAutoresizingMaskIntoConstraints = false;
            m_overflow.WidthAnchor.ConstraintEqualTo(kOverflowDemoWidth).Active = true;
            this.AddDemo(m_overflow);

            this.AddText("Scroll: the strip keeps every item and drags sideways.");
            m_scrolling.TranslatesAutoresizingMaskIntoConstraints = false;
            m_scrolling.WidthAnchor.ConstraintEqualTo(kOverflowDemoWidth).Active = true;
            this.AddDemo(m_scrolling);

            this.AddHeading("Vertical");
            this.AddText("Orientation = Vertical runs the strip top to bottom and stretches its buttons across its width. With OverflowPopup, what does not fit moves into the menu on the button at its foot.");
            m_vertical.TranslatesAutoresizingMaskIntoConstraints = false;
            m_vertical.WidthAnchor.ConstraintEqualTo(kVerticalDemoWidth).Active = true;
            m_verticalOverflow.TranslatesAutoresizingMaskIntoConstraints = false;
            m_verticalOverflow.WidthAnchor.ConstraintEqualTo(kVerticalDemoWidth).Active = true;
            m_verticalOverflow.HeightAnchor.ConstraintEqualTo(kVerticalOverflowDemoHeight).Active = true;
            this.AddDemoRow(m_vertical, m_verticalOverflow);
            this.AddCode(@"
var strip = new ToggleStrip
{
    Orientation = UILayoutConstraintAxis.Vertical,
    OverflowBehavior = eToggleStripOverflow.OverflowPopup,
    ItemsSource = planets,
};");

            this.AddDemo(m_report);
        }

        private void Strip_OnSelectionChanged (object sender, ToggleStrip.ToggleStripSelectionChangedEventArgs e)
        {
            m_report.Text = $"Previous: [{string.Join(", ", e.PreviousSelection.Cast<object>())}]  Current: [{string.Join(", ", e.CurrentSelection.Cast<object>())}]";
        }

        private void AddSize_OnTouchUpInside (object sender, EventArgs e)
        {
            m_sizes.Insert(1, $"Size {m_sizes.Count + 1}");
        }
    }
}
