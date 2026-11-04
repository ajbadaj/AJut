namespace AJut.UX.Tests
{
    using System.Windows;
    using System.Windows.Controls;
    using AJut.UX.AttachedProperties;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Edit.IsEditable is the inverse of Edit.IsReadOnly wherever IsReadOnly reaches, since AJut's themed styles enable
    /// controls from it. WPF objects need an STA thread, so each test runs on one, and nothing is ever shown.
    /// </summary>
    [TestClass]
    public class WpfEditAttachedPropertyTests
    {
        [TestMethod]
        public void Edit_IsEditable_FollowsAnInheritedIsReadOnly_EachWay ()
        {
            StaTestThread.Run(() =>
            {
                var leaf = new TextBox();
                var middle = new Border { Child = leaf };
                var root = new StackPanel();
                root.Children.Add(middle);

                Edit.SetIsReadOnly(root, true);
                Assert.IsFalse(Edit.GetIsEditable(root), "Where IsReadOnly is set");
                Assert.IsFalse(Edit.GetIsEditable(leaf), "Where IsReadOnly is inherited");

                root.ClearValue(Edit.IsReadOnlyProperty);
                Assert.IsTrue(Edit.GetIsEditable(root));
                Assert.IsTrue(Edit.GetIsEditable(leaf), "Clearing IsReadOnly makes everything under it editable again");
            });
        }

        [TestMethod]
        public void Edit_IsEditable_FollowsIsReadOnlySetPartWayDown ()
        {
            StaTestThread.Run(() =>
            {
                var leaf = new TextBox();
                var middle = new Border { Child = leaf };
                var root = new StackPanel();
                root.Children.Add(middle);

                Edit.SetIsReadOnly(root, true);
                Edit.SetIsReadOnly(middle, false);
                Assert.IsFalse(Edit.GetIsEditable(root));
                Assert.IsTrue(Edit.GetIsEditable(middle), "IsReadOnly set false under a read-only parent makes that part editable");
                Assert.IsTrue(Edit.GetIsEditable(leaf), "and everything under it");
            });
        }

        [TestMethod]
        public void Edit_IsEditable_ReachesAnElementAddedUnderAReadOnlyParent ()
        {
            StaTestThread.Run(() =>
            {
                var root = new StackPanel();
                Edit.SetIsReadOnly(root, true);

                var added = new TextBox();
                root.Children.Add(added);
                Assert.IsTrue(Edit.GetIsReadOnly(added));
                Assert.IsFalse(Edit.GetIsEditable(added), "An element added later takes both from its parent");
            });
        }
    }
}
