namespace AJut.UX.Tests
{
    using System;
    using System.ComponentModel;
    using System.Linq;
    using System.Threading.Tasks;
    using AJut.Storage;
    using AJut.UX;
    using AJut.UX.Controls;
    using AJut.UX.PropertyInteraction;
    using Foundation;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using UIKit;

    [TestClass]
    public class PropertyGridTests
    {
        // The rows, in the order Settings declares its properties
        private const int kNameRow = 0;
        private const int kIsOnRow = 1;
        private const int kCountRow = 2;
        private const int kLimitRow = 3;

        [TestMethod]
        public void Grid_InAWindow_ShowsARowPerProperty ()
        {
            PropertyGrid grid = MakeGrid(new Settings());
            using (TestHost.Attach(grid))
            {
                Assert.AreEqual(4, (int)TableOf(grid).NumberOfRowsInSection(0));
            }
        }

        [TestMethod]
        public async Task Grid_StringRow_WritesTheSourceWhenEditingEnds ()
        {
            var settings = new Settings();
            PropertyGrid grid = MakeGrid(settings);
            using (TestHost.Attach(grid))
            {
                await TestHost.LayOutAsync(grid);
                UITextField field = TestHost.Descendants<UITextField>(CellAt(grid, kNameRow)).First();
                Assert.AreEqual("Grid", field.Text);

                field.Text = "Edited";
                field.SendActionForControlEvents(UIControlEvent.EditingChanged);
                Assert.AreEqual("Grid", settings.Name, "Nothing is written while still editing");
                field.SendActionForControlEvents(UIControlEvent.EditingDidEnd);
                Assert.AreEqual("Edited", settings.Name);
            }
        }

        [TestMethod]
        public async Task Grid_BooleanRow_EditsThroughASwitch ()
        {
            var settings = new Settings();
            PropertyGrid grid = MakeGrid(settings);
            using (TestHost.Attach(grid))
            {
                await TestHost.LayOutAsync(grid);
                UISwitch toggle = TestHost.Descendants<UISwitch>(CellAt(grid, kIsOnRow)).First();

                toggle.On = true;
                toggle.SendActionForControlEvents(UIControlEvent.ValueChanged);
                Assert.IsTrue(settings.IsOn);
            }
        }

        [TestMethod]
        public async Task Grid_SourceChangedElsewhere_ShowsTheNewValue ()
        {
            var settings = new Settings();
            PropertyGrid grid = MakeGrid(settings);
            using (TestHost.Attach(grid))
            {
                await TestHost.LayOutAsync(grid);
                settings.Name = "From code";
                UITextField field = TestHost.Descendants<UITextField>(CellAt(grid, kNameRow)).First();
                Assert.AreEqual("From code", field.Text);
            }
        }

        [TestMethod]
        public async Task Grid_ReadOnly_TakesNoEdits ()
        {
            PropertyGrid grid = MakeGrid(new Settings());
            grid.IsReadOnly = true;
            using (TestHost.Attach(grid))
            {
                await TestHost.LayOutAsync(grid);
                Assert.IsFalse(TestHost.Descendants<UITextField>(CellAt(grid, kNameRow)).First().Enabled, "The text field");
                Assert.IsFalse(TestHost.Descendants<UISwitch>(CellAt(grid, kIsOnRow)).First().Enabled, "The switch");
            }
        }

        [TestMethod]
        public async Task Grid_Disabled_DisablesEveryEditor ()
        {
            PropertyGrid grid = MakeGrid(new Settings());
            grid.Enabled = false;
            using (TestHost.Attach(grid))
            {
                await TestHost.LayOutAsync(grid);
                Assert.IsFalse(TableOf(grid).UserInteractionEnabled, "No row takes a touch");
                Assert.IsFalse(TestHost.Descendants<UITextField>(CellAt(grid, kNameRow)).First().Enabled, "The text field");
                Assert.IsFalse(TestHost.Descendants<UISwitch>(CellAt(grid, kIsOnRow)).First().Enabled, "The switch");
                Assert.IsTrue(TestHost.Descendants<NumericEditor>(CellAt(grid, kCountRow)).First().IsReadOnly, "The number");
                UIButton[] setAndUnset = TestHost.Descendants<UIButton>(CellAt(grid, kLimitRow)).Where(b => (b.Configuration?.Title == "Set") || (b.Configuration?.Title == "Unset")).ToArray();
                Assert.AreEqual(2, setAndUnset.Length, "The nullable row has its set and unset buttons");
                Assert.IsTrue(setAndUnset.All(b => b.Hidden || !b.Enabled), "Neither can be pressed");
            }
        }

        [TestMethod]
        public async Task Grid_EnabledAgain_TakesEditsAgain ()
        {
            PropertyGrid grid = MakeGrid(new Settings());
            grid.Enabled = false;
            using (TestHost.Attach(grid))
            {
                grid.Enabled = true;
                await TestHost.LayOutAsync(grid);
                Assert.IsTrue(TableOf(grid).UserInteractionEnabled);
                Assert.IsTrue(TestHost.Descendants<UITextField>(CellAt(grid, kNameRow)).First().Enabled);
            }
        }

        [TestMethod]
        public async Task Grid_NullableRowWithNoValue_SaysNotSet ()
        {
            PropertyGrid grid = MakeGrid(new Settings());
            using (TestHost.Attach(grid))
            {
                await TestHost.LayOutAsync(grid);
                UILabel notSet = TestHost.Descendants<UILabel>(CellAt(grid, kLimitRow)).FirstOrDefault(l => l.Text == "Not set");
                Assert.IsNotNull(notSet);
                Assert.IsFalse(notSet.Hidden);
            }
        }

        [TestMethod]
        public void Grid_LeavingTheWindow_LetsGoOfItsSource ()
        {
            var settings = new Settings();
            PropertyGrid grid = MakeGrid(settings);
            IDisposable attached = TestHost.Attach(grid);
            Assert.IsTrue(settings.SubscriberCount > 0, "The grid follows its source while in a window");

            attached.Dispose();
            Assert.AreEqual(0, settings.SubscriberCount, "Nothing is left listening once the grid is out of the window");
        }

        [TestMethod]
        public void Grid_BackInAWindow_ShowsItsRowsWithTheSelectionKept ()
        {
            PropertyGrid grid = MakeGrid(new Settings());
            IDisposable attached = TestHost.Attach(grid);
            var isOn = (PropertyEditTarget)((IObservableTreeNode)grid.Manager.RootNode).Children[kIsOnRow];
            grid.SelectedTarget = isOn;

            attached.Dispose();
            Assert.AreEqual(0, (int)TableOf(grid).NumberOfRowsInSection(0), "No rows out of a window");

            using (TestHost.Attach(grid))
            {
                Assert.AreEqual(4, (int)TableOf(grid).NumberOfRowsInSection(0));
                Assert.AreSame(isOn, grid.SelectedTarget);
            }
        }

        // ===========[ Helper Methods ]===================================
        private static PropertyGrid MakeGrid (Settings settings)
        {
            var grid = new PropertyGrid();
            grid.Editors.RegisterStandardEditors();
            grid.SetSingleItemSource(settings);
            return grid;
        }

        private static UITableView TableOf (UIView view) => TestHost.Descendants<UITableView>(view).First();

        private static UITableViewCell CellAt (PropertyGrid grid, int row)
        {
            return TableOf(grid).CellAt(NSIndexPath.FromRowSection(row, 0)) ?? throw new AssertFailedException($"Row {row} is not shown");
        }

        // ===========[ Subclasses/structs ]===================================
        // A source that can say who listens to it, so a test can tell the grid let go. Its count is a read-only property
        // with no setter, which the grid shows no row for (a private setter would still count as one).
        private sealed class Settings : INotifyPropertyChanged
        {
            private string m_name = "Grid";
            private bool m_isOn;
            private int m_count = 3;
            private int? m_limit;

            public event PropertyChangedEventHandler PropertyChanged;

            public int SubscriberCount => this.PropertyChanged?.GetInvocationList().Length ?? 0;

            public string Name
            {
                get => m_name;
                set => this.Set(ref m_name, value, nameof(Name));
            }

            public bool IsOn
            {
                get => m_isOn;
                set => this.Set(ref m_isOn, value, nameof(IsOn));
            }

            public int Count
            {
                get => m_count;
                set => this.Set(ref m_count, value, nameof(Count));
            }

            public int? Limit
            {
                get => m_limit;
                set => this.Set(ref m_limit, value, nameof(Limit));
            }

            private void Set<T> (ref T field, T value, string propertyName)
            {
                field = value;
                this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }
}
