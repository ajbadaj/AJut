namespace AJutShowRoomUIKit.UI.Controls
{
    using System;
    using AJut;
    using AJut.UX.Controls;
    using UIKit;

    /// <summary>EnumToggleStrip and EnumComboBox, which both take their options from the enum type of their SelectedItem.</summary>
    public sealed class EnumPickersPage : ShowroomPageController
    {
        private readonly EnumToggleStrip m_shapeStrip = new EnumToggleStrip { SelectedItem = eDemoShape.Square };
        private readonly EnumToggleStrip m_toppingsStrip = new EnumToggleStrip { AllowMultiSelect = true, AllowNoSelection = true };
        private readonly EnumComboBox m_shapeCombo = new EnumComboBox { SelectedItem = eDemoShape.Circle };
        private readonly EnumToggleStrip m_verticalShapeStrip = new EnumToggleStrip { Orientation = UILayoutConstraintAxis.Vertical, SelectedItem = eDemoShape.Triangle };
        private readonly UILabel m_report = new UILabel { Lines = 0 };

        public EnumPickersPage () : base("EnumToggleStrip & EnumComboBox")
        {
            m_toppingsStrip.SelectedItem = eDemoToppings.Cheese | eDemoToppings.Olives;
        }

        public override void ViewWillAppear (bool animated)
        {
            base.ViewWillAppear(animated);
            foreach (UIControl control in this.Pickers())
            {
                control.ValueChanged -= this.Picker_OnValueChanged;
                control.ValueChanged += this.Picker_OnValueChanged;
            }

            this.ShowValues();
        }

        public override void ViewDidDisappear (bool animated)
        {
            base.ViewDidDisappear(animated);
            foreach (UIControl control in this.Pickers())
            {
                control.ValueChanged -= this.Picker_OnValueChanged;
            }
        }

        protected override void BuildPage ()
        {
            this.AddHeading("EnumToggleStrip");
            this.AddText("A ToggleStrip of an enum's values. Hidden is marked [ExcludeFromSelection], so it is left out.");
            this.AddDemo(m_shapeStrip);

            this.AddText("Orientation is passed on to the strip inside, so it can run top to bottom too.");
            this.AddDemo(m_verticalShapeStrip);

            this.AddHeading("A [Flags] enum, multi select");
            this.AddText("SelectedItem is every selected flag OR'd together. Five flags are wider than a phone, so the strip is set to scroll: Strip.OverflowBehavior = Scroll, and drag it sideways.");
            m_toppingsStrip.Strip.OverflowBehavior = eToggleStripOverflow.Scroll;
            m_toppingsStrip.TranslatesAutoresizingMaskIntoConstraints = false;
            this.AddDemo(m_toppingsStrip);

            // Held to the page's width, so on a phone the strip scrolls rather than running off the edge
            m_toppingsStrip.WidthAnchor.ConstraintLessThanOrEqualTo(m_toppingsStrip.Superview.WidthAnchor).Active = true;

            this.AddHeading("EnumComboBox");
            this.AddText("A button with a menu of the same options, UIKit's equivalent of a combo box.");
            this.AddDemo(m_shapeCombo);

            this.AddDemo(m_report);
            this.AddCode(@"
var strip = new EnumToggleStrip { SelectedItem = eDemoShape.Square };
var toppings = new EnumToggleStrip { AllowMultiSelect = true, SelectedItem = eDemoToppings.Cheese | eDemoToppings.Olives };
toppings.Strip.OverflowBehavior = eToggleStripOverflow.Scroll;
var combo = new EnumComboBox { SelectedItem = eDemoShape.Circle };");
        }

        private UIControl[] Pickers () => new UIControl[] { m_shapeStrip, m_toppingsStrip, m_shapeCombo };

        private void Picker_OnValueChanged (object sender, EventArgs e)
        {
            this.ShowValues();
        }

        private void ShowValues ()
        {
            m_report.Text = $"Strip: {m_shapeStrip.SelectedItem}   Toppings: {m_toppingsStrip.SelectedItem}   Combo: {m_shapeCombo.SelectedItem}";
        }

        // ===========[ Subclasses/structs ]===================================
        public enum eDemoShape
        {
            Circle,
            Square,
            Triangle,

            [ExcludeFromSelection]
            Hidden,
        }

        [Flags]
        public enum eDemoToppings
        {
            None = 0,
            Cheese = 1,
            Olives = 2,
            Peppers = 4,
            Onions = 8,
        }
    }
}
