namespace AJut.UX.Tests
{
    using System;
    using System.Linq;
    using System.Windows.Input;
    using AJut.UX.Controls;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>The menu entry the code-built toolkits' PopupMenuButton shows: <see cref="PopupMenuItem"/></summary>
    [TestClass]
    public class PopupMenuItemTests
    {
        [TestMethod]
        public void Invoke_PlainItem_RaisesClickThenRunsItsCommand ()
        {
            string order = string.Empty;
            var item = new PopupMenuItem("Open", new TestCommand(() => order += "command"));
            item.Click += _OnClick;

            item.Invoke();
            Assert.AreEqual("click,command", order);
            item.Click -= _OnClick;

            void _OnClick (object sender, EventArgs e) => order += "click,";
        }

        [TestMethod]
        public void Invoke_CheckableItem_FlipsIsCheckedBeforeClick ()
        {
            var item = new PopupMenuItem("Word wrap") { IsCheckable = true };
            bool? checkedWhenClicked = null;
            item.Click += _OnClick;

            item.Invoke();
            Assert.IsTrue(item.IsChecked);
            Assert.AreEqual(true, checkedWhenClicked, "Click sees the new state, as ToggleMenuFlyoutItem's does");

            item.Invoke();
            Assert.IsFalse(item.IsChecked);
            item.Click -= _OnClick;

            void _OnClick (object sender, EventArgs e) => checkedWhenClicked = item.IsChecked;
        }

        [TestMethod]
        public void Invoke_PlainItemNotCheckable_LeavesIsCheckedAlone ()
        {
            var item = new PopupMenuItem("Open") { IsChecked = true };
            item.Invoke();
            Assert.IsTrue(item.IsChecked);
        }

        [TestMethod]
        public void Invoke_ItemThatCannotBePicked_DoesNothing ()
        {
            int ran = 0;
            var disabled = new PopupMenuItem("Off", new TestCommand(() => ++ran)) { IsEnabled = false, IsCheckable = true };
            disabled.Invoke();
            Assert.IsFalse(disabled.CanInvoke);
            Assert.IsFalse(disabled.IsChecked);

            var cannotRun = new PopupMenuItem("Blocked", new TestCommand(() => ++ran, () => false));
            Assert.IsFalse(cannotRun.CanInvoke, "An item whose command cannot run is shown disabled");
            cannotRun.Invoke();

            PopupMenuItem separator = PopupMenuItem.CreateSeparator();
            Assert.IsTrue(separator.IsSeparator);
            Assert.IsFalse(separator.CanInvoke);
            separator.Invoke();

            Assert.AreEqual(0, ran);
        }

        [TestMethod]
        public void Invoke_ItemWithASubmenu_OpensRatherThanRuns ()
        {
            int ran = 0;
            var parent = new PopupMenuItem("Recent", new TestCommand(() => ++ran));
            parent.Items.Add(new PopupMenuItem("first.txt"));

            Assert.IsTrue(parent.HasSubmenu);
            parent.Invoke();
            Assert.AreEqual(0, ran);
        }

        [TestMethod]
        public void GroupBySeparators_SplitsAtEachSeparatorAndDropsEmptyGroups ()
        {
            PopupMenuItem a = new PopupMenuItem("A"), b = new PopupMenuItem("B"), c = new PopupMenuItem("C");
            var groups = PopupMenuItem.GroupBySeparators(new[]
            {
                PopupMenuItem.CreateSeparator(), a, b, PopupMenuItem.CreateSeparator(), PopupMenuItem.CreateSeparator(), c, PopupMenuItem.CreateSeparator(),
            });

            Assert.AreEqual(2, groups.Count);
            CollectionAssert.AreEqual(new[] { a, b }, groups[0].ToArray());
            CollectionAssert.AreEqual(new[] { c }, groups[1].ToArray());
            Assert.AreEqual(0, PopupMenuItem.GroupBySeparators(Array.Empty<PopupMenuItem>()).Count);
        }

        // ===========[ Subclasses/structs ]===================================
        private sealed class TestCommand : ICommand
        {
            private readonly Action m_execute;
            private readonly Func<bool> m_canExecute;

            public TestCommand (Action execute, Func<bool> canExecute = null)
            {
                m_execute = execute;
                m_canExecute = canExecute ?? (() => true);
            }

            public event EventHandler CanExecuteChanged { add { } remove { } }
            public bool CanExecute (object parameter) => m_canExecute();
            public void Execute (object parameter) => m_execute();
        }
    }
}