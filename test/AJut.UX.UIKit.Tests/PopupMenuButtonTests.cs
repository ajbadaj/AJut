namespace AJut.UX.Tests
{
    using System.Linq;
    using AJut.UX.Controls;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using UIKit;

    [TestClass]
    public class PopupMenuButtonTests
    {
        [TestMethod]
        public void CreateMenuElements_OneGroup_IsTheItemsInOrder ()
        {
            UIMenuElement[] elements = PopupMenuButton.CreateMenuElements(new[] { new PopupMenuItem("Open"), new PopupMenuItem("Save") });
            CollectionAssert.AreEqual(new[] { "Open", "Save" }, elements.Select(e => e.Title).ToArray());
            Assert.IsTrue(elements.All(e => e is UIAction));
        }

        [TestMethod]
        public void CreateMenuElements_Separators_SplitTheItemsIntoInlineGroups ()
        {
            UIMenuElement[] elements = PopupMenuButton.CreateMenuElements(new[]
            {
                new PopupMenuItem("Open"), new PopupMenuItem("Save"), PopupMenuItem.CreateSeparator(), new PopupMenuItem("Delete"),
            });

            Assert.AreEqual(2, elements.Length);
            var first = (UIMenu)elements[0];
            Assert.IsTrue(first.Options.HasFlag(UIMenuOptions.DisplayInline), "A group shows inline, with a divider, not as a submenu");
            CollectionAssert.AreEqual(new[] { "Open", "Save" }, first.Children.Select(e => e.Title).ToArray());
            CollectionAssert.AreEqual(new[] { "Delete" }, ((UIMenu)elements[1]).Children.Select(e => e.Title).ToArray());
        }

        [TestMethod]
        public void CreateMenuElements_ChecksDisabledAndSubmenus_ShowAsUIKitDoes ()
        {
            var recent = new PopupMenuItem("Recent");
            recent.Items.Add(new PopupMenuItem("first.txt"));
            var closedRecent = new PopupMenuItem("Closed") { IsEnabled = false };
            closedRecent.Items.Add(new PopupMenuItem("never shown"));

            UIMenuElement[] elements = PopupMenuButton.CreateMenuElements(new[]
            {
                new PopupMenuItem("Word wrap") { IsCheckable = true, IsChecked = true },
                new PopupMenuItem("Line numbers") { IsCheckable = true },
                new PopupMenuItem("Delete") { IsEnabled = false },
                recent,
                closedRecent,
            });

            Assert.AreEqual(UIMenuElementState.On, ((UIAction)elements[0]).State);
            Assert.AreEqual(UIMenuElementState.Off, ((UIAction)elements[1]).State);
            Assert.IsTrue(((UIAction)elements[2]).Attributes.HasFlag(UIMenuElementAttributes.Disabled));
            Assert.AreEqual("first.txt", ((UIMenu)elements[3]).Children.Single().Title, "An item with items of its own is a submenu");
            Assert.IsInstanceOfType(elements[4], typeof(UIAction), "A disabled submenu shows as a disabled action, since a UIMenu has no disabled state");
            Assert.IsTrue(((UIAction)elements[4]).Attributes.HasFlag(UIMenuElementAttributes.Disabled));
        }

        [TestMethod]
        public void Button_ShowsItsMenuOnATapAndItsContentAsTheTitle ()
        {
            var button = new PopupMenuButton { ButtonContent = "Options" };
            Assert.IsTrue(button.ShowsMenuAsPrimaryAction);
            Assert.IsNotNull(button.Menu);
            Assert.AreEqual("Options", button.ButtonContent);
            Assert.IsFalse(button.IsOpen);
        }
    }
}
