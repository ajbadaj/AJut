namespace AJut.UX.Tests
{
    using System;
    using System.Collections.Generic;
    using AJut.UX.Controls;
    using Android.Views;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class PopupMenuButtonTests
    {
        [TestMethod]
        public void Tap_OpensTheMenuFromTheItems_InOrderWithGroupsAndChecks ()
        {
            var button = new PopupMenuButton(TestHost.RequiredContext) { ButtonContent = "Options" };
            button.MenuItems.Add(new PopupMenuItem("Open"));
            button.MenuItems.Add(new PopupMenuItem("Word wrap") { IsCheckable = true, IsChecked = true });
            button.MenuItems.Add(PopupMenuItem.CreateSeparator());
            button.MenuItems.Add(new PopupMenuItem("Delete") { IsEnabled = false });
            using (TestHost.Attach(button))
            {
                button.PerformClick();
                try
                {
                    Assert.IsTrue(button.IsOpen);
                    Assert.IsTrue(button.Activated, "The button shows itself checked while the menu is open");
                    IMenu menu = button.ShownMenu!.Menu!;
                    Assert.AreEqual(3, menu.Size(), "The separator is a divider between groups, not an item");
                    Assert.AreEqual("Open", menu.GetItem(0)!.TitleFormatted!.ToString());
                    Assert.IsTrue(menu.GetItem(1)!.IsCheckable);
                    Assert.IsTrue(menu.GetItem(1)!.IsChecked);
                    Assert.IsFalse(menu.GetItem(0)!.IsCheckable);
                    Assert.IsFalse(menu.GetItem(2)!.IsEnabled);
                    Assert.AreEqual(menu.GetItem(0)!.GroupId, menu.GetItem(1)!.GroupId);
                    Assert.AreNotEqual(menu.GetItem(1)!.GroupId, menu.GetItem(2)!.GroupId, "Items after a separator are a group of their own");
                }
                finally
                {
                    button.IsOpen = false;
                }
            }
        }

        [TestMethod]
        public void Tap_ItemWithItemsOfItsOwn_IsASubmenu ()
        {
            var recent = new PopupMenuItem("Recent");
            recent.Items.Add(new PopupMenuItem("first.txt"));
            recent.Items.Add(new PopupMenuItem("second.txt"));
            var button = new PopupMenuButton(TestHost.RequiredContext);
            button.MenuItems.Add(recent);
            using (TestHost.Attach(button))
            {
                button.PerformClick();
                try
                {
                    IMenuItem entry = button.ShownMenu!.Menu!.GetItem(0)!;
                    Assert.IsTrue(entry.HasSubMenu);
                    Assert.AreEqual(2, entry.SubMenu!.Size());
                    Assert.AreEqual("second.txt", entry.SubMenu.GetItem(1)!.TitleFormatted!.ToString());
                }
                finally
                {
                    button.IsOpen = false;
                }
            }
        }

        [TestMethod]
        public void PickingAnItem_InvokesIt ()
        {
            var picked = new List<string>();
            var wrap = new PopupMenuItem("Word wrap") { IsCheckable = true };
            wrap.Click += _OnClick;
            var button = new PopupMenuButton(TestHost.RequiredContext);
            button.MenuItems.Add(new PopupMenuItem("Open"));
            button.MenuItems.Add(wrap);
            using (TestHost.Attach(button))
            {
                button.PerformClick();
                try
                {
                    IMenu menu = button.ShownMenu!.Menu!;
                    menu.PerformIdentifierAction(menu.GetItem(1)!.ItemId, 0);
                    CollectionAssert.AreEqual(new[] { "Word wrap" }, picked);
                    Assert.IsTrue(wrap.IsChecked, "A checkable item flips when picked");
                }
                finally
                {
                    button.IsOpen = false;
                    wrap.Click -= _OnClick;
                }
            }

            void _OnClick (object? sender, EventArgs e) => picked.Add(((PopupMenuItem)sender!).Text);
        }

        [TestMethod]
        public void IsOpen_SetFromCode_OpensAndClosesWithItsEvents ()
        {
            int opened = 0;
            int closed = 0;
            var button = new PopupMenuButton(TestHost.RequiredContext);
            button.MenuItems.Add(new PopupMenuItem("Open"));
            button.FlyoutOpened += _OnOpened;
            button.FlyoutClosed += _OnClosed;
            using (TestHost.Attach(button))
            {
                button.IsOpen = true;
                Assert.IsTrue(button.IsOpen);
                Assert.AreEqual(1, opened);

                button.IsOpen = false;
                Assert.IsFalse(button.IsOpen);
                Assert.IsFalse(button.Activated);
                Assert.AreEqual(1, closed);
                Assert.IsNull(button.ShownMenu);
            }

            button.FlyoutOpened -= _OnOpened;
            button.FlyoutClosed -= _OnClosed;

            void _OnOpened (object? sender, EventArgs e) => ++opened;
            void _OnClosed (object? sender, EventArgs e) => ++closed;
        }

        [TestMethod]
        public void IsOpen_OutOfAWindow_StaysClosed ()
        {
            var button = new PopupMenuButton(TestHost.RequiredContext);
            button.MenuItems.Add(new PopupMenuItem("Open"));
            button.IsOpen = true;
            Assert.IsFalse(button.IsOpen, "A menu needs its button in a window");
        }

        [TestMethod]
        public void LeavingTheWindow_ClosesAnOpenMenu ()
        {
            var button = new PopupMenuButton(TestHost.RequiredContext);
            button.MenuItems.Add(new PopupMenuItem("Open"));
            IDisposable attached = TestHost.Attach(button);
            button.IsOpen = true;
            attached.Dispose();
            Assert.IsFalse(button.IsOpen);
            Assert.IsNull(button.ShownMenu);
        }
    }
}
