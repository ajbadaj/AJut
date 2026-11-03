namespace AJutShowRoomUIKit.UI.Controls
{
    using System;
    using AJut.UX.Controls;
    using UIKit;

    public sealed class PopupMenuButtonPage : ShowroomPageController
    {
        private readonly UILabel m_picked = new UILabel { Text = "Nothing picked yet.", Lines = 0 };

        public PopupMenuButtonPage () : base("PopupMenuButton")
        {
        }

        protected override void BuildPage ()
        {
            this.AddHeading("PopupMenuButton");
            this.AddText("A button that opens a menu of its items when tapped, and shows itself checked while the menu is open. Separators divide the menu into groups, an item with items of its own opens a submenu, and a checkable item flips its check when picked.");

            var button = new PopupMenuButton { ButtonContent = "Options" };
            var recent = new PopupMenuItem("Recent");
            recent.Items.Add(this.Leaf(new PopupMenuItem("notes.txt")));
            recent.Items.Add(this.Leaf(new PopupMenuItem("plan.md")));
            button.MenuItems.Add(this.Leaf(new PopupMenuItem("Open")));
            button.MenuItems.Add(this.Leaf(new PopupMenuItem("Save")));
            button.MenuItems.Add(recent);
            button.MenuItems.Add(PopupMenuItem.CreateSeparator());
            button.MenuItems.Add(this.Leaf(new PopupMenuItem("Word wrap") { IsCheckable = true, IsChecked = true }));
            button.MenuItems.Add(this.Leaf(new PopupMenuItem("Line numbers") { IsCheckable = true }));
            button.MenuItems.Add(PopupMenuItem.CreateSeparator());
            button.MenuItems.Add(this.Leaf(new PopupMenuItem("Delete") { IsEnabled = false }));
            this.AddDemo(button);
            this.AddWide(m_picked);
            this.AddCode(@"
var button = new PopupMenuButton { ButtonContent = ""Options"" };
button.MenuItems.Add(new PopupMenuItem(""Open"", openCommand));
button.MenuItems.Add(PopupMenuItem.CreateSeparator());
button.MenuItems.Add(new PopupMenuItem(""Word wrap"") { IsCheckable = true });");

            this.AddText("Disabled, it shows its disabled look and opens nothing.");
            var disabled = new PopupMenuButton { ButtonContent = "Unavailable", Enabled = false };
            disabled.MenuItems.Add(new PopupMenuItem("Never shown"));
            this.AddDemo(disabled);
        }

        private PopupMenuItem Leaf (PopupMenuItem item)
        {
            // The page and its items go together, so the handler needs no unhooking: nothing native holds either
            item.Click += this.OnItemClick;
            return item;
        }

        private void OnItemClick (object sender, EventArgs e)
        {
            if (sender is PopupMenuItem item)
            {
                m_picked.Text = item.IsCheckable ? $"{item.Text} is {(item.IsChecked ? "on" : "off")}." : $"Picked {item.Text}.";
            }
        }
    }
}
