namespace AJutShowRoomAppKit.UI.Controls
{
    using System;
    using System.Collections.Generic;
    using AJut.UX.Controls;
    using AppKit;

    public sealed class PopupMenuButtonPage : ShowroomPage
    {
        private readonly List<PopupMenuItem> m_followedItems = new List<PopupMenuItem>();
        private readonly PopupMenuButton m_options;
        private readonly NSButton m_openFromCode;
        private readonly NSTextField m_picked;
        private readonly NSTextField m_menuState;

        public PopupMenuButtonPage ()
            : base("PopupMenuButton", "A button that drops a menu of its items when pressed, and shows itself checked while the menu is open. The menu is the Mac's own: separators divide it into groups, an item with items of its own opens a submenu, and a checkable item flips its check when picked. Only the button takes the AJut look.")
        {
            m_options = new PopupMenuButton { ButtonContent = "Options" };
            var recent = new PopupMenuItem("Recent");
            var older = new PopupMenuItem("Older");
            older.Items.Add(this.Follow(new PopupMenuItem("draft.txt")));
            recent.Items.Add(this.Follow(new PopupMenuItem("notes.txt")));
            recent.Items.Add(this.Follow(new PopupMenuItem("plan.md")));
            recent.Items.Add(older);
            m_options.MenuItems.Add(this.Follow(new PopupMenuItem("Open")));
            m_options.MenuItems.Add(this.Follow(new PopupMenuItem("Save")));
            m_options.MenuItems.Add(recent);
            m_options.MenuItems.Add(PopupMenuItem.CreateSeparator());
            m_options.MenuItems.Add(this.Follow(new PopupMenuItem("Word wrap") { IsCheckable = true, IsChecked = true }));
            m_options.MenuItems.Add(this.Follow(new PopupMenuItem("Line numbers") { IsCheckable = true }));
            m_options.MenuItems.Add(PopupMenuItem.CreateSeparator());
            m_options.MenuItems.Add(this.Follow(new PopupMenuItem("Delete") { IsEnabled = false }));
            m_options.FlyoutOpened += this.OnOptionsFlyoutOpened;
            m_options.FlyoutClosed += this.OnOptionsFlyoutClosed;
            this.AddLabeledDemo("Menu", m_options);

            m_picked = ShowroomStyle.Understated("Nothing picked yet");
            this.AddLabeledDemo("Picked", m_picked);
            m_menuState = ShowroomStyle.Understated("Closed");
            this.AddLabeledDemo("The menu is", m_menuState);

            m_openFromCode = new NSButton { Title = "Open it from code" };
            m_openFromCode.BezelStyle = NSBezelStyle.Rounded;
            m_openFromCode.Activated += this.OnOpenFromCodeActivated;
            this.AddLabeledDemo("IsOpen = true", m_openFromCode);

            var disabled = new PopupMenuButton { ButtonContent = "Unavailable", Enabled = false };
            disabled.MenuItems.Add(new PopupMenuItem("Never shown"));
            this.AddLabeledDemo("Disabled", disabled);

            this.AddCode(
@"var options = new PopupMenuButton { ButtonContent = ""Options"" };
options.MenuItems.Add(new PopupMenuItem(""Open"", openCommand));
options.MenuItems.Add(PopupMenuItem.CreateSeparator());
options.MenuItems.Add(new PopupMenuItem(""Word wrap"") { IsCheckable = true });

// The same items, as any other AppKit menu
NSMenu menu = PopupMenuButton.CreateMenu(options.MenuItems);"
            );
        }

        protected override void OnTeardown ()
        {
            m_options.FlyoutOpened -= this.OnOptionsFlyoutOpened;
            m_options.FlyoutClosed -= this.OnOptionsFlyoutClosed;
            m_openFromCode.Activated -= this.OnOpenFromCodeActivated;
            foreach (PopupMenuItem item in m_followedItems)
            {
                item.Click -= this.OnItemClick;
            }

            m_followedItems.Clear();
        }

        // ===========[ Events ]================================================
        private void OnItemClick (object sender, EventArgs e)
        {
            if (sender is PopupMenuItem item)
            {
                m_picked.StringValue = item.IsCheckable ? $"{item.Text} is {(item.IsChecked ? "on" : "off")}" : $"Picked {item.Text}";
            }
        }

        private void OnOptionsFlyoutOpened (object sender, EventArgs e) => m_menuState.StringValue = "Open";
        private void OnOptionsFlyoutClosed (object sender, EventArgs e) => m_menuState.StringValue = "Closed";
        private void OnOpenFromCodeActivated (object sender, EventArgs e) => m_options.IsOpen = true;

        // ===========[ Helper Methods ]========================================
        private PopupMenuItem Follow (PopupMenuItem item)
        {
            item.Click += this.OnItemClick;
            m_followedItems.Add(item);
            return item;
        }
    }
}
