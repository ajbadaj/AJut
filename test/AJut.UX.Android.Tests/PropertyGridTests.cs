namespace AJut.UX.Tests
{
    using System;
    using System.ComponentModel;
    using System.Linq;
    using System.Threading.Tasks;
    using AJut.Storage;
    using AJut.UX.Controls;
    using AJut.UX.PropertyInteraction;
    using Android.Views;
    using Android.Views.InputMethods;
    using Android.Widget;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class PropertyGridTests
    {
        // The rows, in the order Settings declares its properties
        private const int kNameRow = 0;
        private const int kIsOnRow = 1;
        private const int kCountRow = 2;
        private const int kLimitRow = 3;
        private const int kDetailRow = 4;
        private const int kRowCount = 5;

        private const int kGridHeightDp = 600;

        // What is left of the grid once a keyboard takes the bottom of the screen
        private const int kGridHeightWithKeyboardDp = 200;

        [TestMethod]
        public void Grid_InAWindow_ShowsARowPerProperty ()
        {
            PropertyGrid grid = MakeGrid(new Settings());
            using (TestViews.AttachSized(grid, kGridHeightDp))
            {
                Assert.AreEqual(kRowCount, ListOf(grid).Adapter!.Count);
            }
        }

        [TestMethod]
        public async Task Grid_StringRow_WritesTheSourceOnDone ()
        {
            var settings = new Settings();
            PropertyGrid grid = MakeGrid(settings);
            using (TestViews.AttachSized(grid, kGridHeightDp))
            {
                await TestViews.LayOutAsync(grid);
                EditText field = NameField(grid);
                Assert.AreEqual("Grid", field.Text);

                field.Text = "Edited";
                Assert.AreEqual("Grid", settings.Name, "Nothing is written while still editing");
                field.OnEditorAction(ImeAction.Done);
                Assert.AreEqual("Edited", settings.Name);
            }
        }

        [TestMethod]
        public async Task Grid_StringRow_WritesTheSourceOnLeavingTheField ()
        {
            var settings = new Settings();
            PropertyGrid grid = MakeGrid(settings);
            using (TestViews.AttachSized(grid, kGridHeightDp))
            {
                await TestViews.LayOutAsync(grid);
                EditText field = NameField(grid);
                Assert.IsTrue(field.RequestFocus(), "The field takes focus");

                field.Text = "Edited";
                field.ClearFocus();
                Assert.AreEqual("Edited", settings.Name);
            }
        }

        // What the keyboard opening does to a window that resizes for it: the grid is laid out again, shorter. The focused
        // field's row is the same view after, and the field keeps its focus, which is what lets the keyboard stay up.
        [TestMethod]
        public async Task Grid_FocusedTextField_KeepsFocusWhenTheGridIsLaidOutShorter ()
        {
            PropertyGrid grid = MakeGrid(new Settings());
            using (TestViews.AttachSized(grid, kGridHeightDp))
            {
                await TestViews.LayOutAsync(grid);
                EditText field = NameField(grid);
                Assert.IsTrue(field.RequestFocus(), "The field takes focus");
                Assert.IsTrue(field.HasFocus);

                grid.LayoutParameters!.Height = TestViews.ToPixels(kGridHeightWithKeyboardDp);
                grid.RequestLayout();
                await TestViews.LayOutAsync(grid);

                Assert.AreSame(field, NameField(grid), "The row was not built again");
                Assert.IsTrue(field.HasFocus, "The field kept its focus through the layout");
            }
        }

        [TestMethod]
        public async Task Grid_AnotherRowExpanding_LeavesTheFocusedTextFieldAsItWas ()
        {
            PropertyGrid grid = MakeGrid(new Settings());
            using (TestViews.AttachSized(grid, kGridHeightDp))
            {
                await TestViews.LayOutAsync(grid);
                EditText field = NameField(grid);
                Assert.IsTrue(field.RequestFocus(), "The field takes focus");

                var detail = (PropertyEditTarget)((IObservableTreeNode)grid.Manager.RootNode).Children[kDetailRow];
                FlatTreeListView tree = TestViews.Descendants<FlatTreeListView>(grid).First();
                tree.FindItemForSource(detail)!.IsExpanded = true;
                await TestViews.LayOutAsync(grid);

                Assert.AreEqual(kRowCount + 1, ListOf(grid).Adapter!.Count, "The sub-object's property shows under it");
                Assert.AreSame(field, NameField(grid), "The name row was not built again");
                Assert.IsTrue(field.HasFocus, "The field has its focus back");
            }
        }

        [TestMethod]
        public async Task Grid_MadeReadOnlyWhileTyping_KeepsWhatWasTyped ()
        {
            var settings = new Settings();
            PropertyGrid grid = MakeGrid(settings);
            using (TestViews.AttachSized(grid, kGridHeightDp))
            {
                await TestViews.LayOutAsync(grid);
                EditText field = NameField(grid);
                Assert.IsTrue(field.RequestFocus(), "The field takes focus");
                field.Text = "Typed";

                grid.IsReadOnly = true;
                await TestViews.LayOutAsync(grid);

                Assert.AreEqual("Typed", settings.Name, "The edit was written as the field went");
                Assert.IsFalse(NameField(grid).Enabled, "The rows were built again, read-only");
            }
        }

        [TestMethod]
        public async Task Grid_TapOnAnUnsetNullablesDimmedEditor_SelectsTheRow ()
        {
            PropertyGrid grid = MakeGrid(new Settings());
            using (TestViews.AttachSized(grid, kGridHeightDp))
            {
                await TestViews.LayOutAsync(grid);
                ListView list = ListOf(grid);
                NumericEditor dimmed = TestViews.Descendants<NumericEditor>(RowAt(grid, kLimitRow)).First();
                Android.Graphics.PointF tap = TestViews.CenterIn(dimmed, list);
                long down = Android.OS.SystemClock.UptimeMillis();

                Dispatch(list, down, MotionEventActions.Down, tap.X, tap.Y);
                Dispatch(list, down, MotionEventActions.Up, tap.X, tap.Y);
                await TestViews.LayOutAsync(grid);

                var limit = (PropertyEditTarget)((IObservableTreeNode)grid.Manager.RootNode).Children[kLimitRow];
                Assert.AreSame(limit, grid.SelectedTarget);
            }
        }

        [TestMethod]
        public async Task Grid_BooleanRow_EditsThroughASwitch ()
        {
            var settings = new Settings();
            PropertyGrid grid = MakeGrid(settings);
            using (TestViews.AttachSized(grid, kGridHeightDp))
            {
                await TestViews.LayOutAsync(grid);
                Switch toggle = TestViews.Descendants<Switch>(RowAt(grid, kIsOnRow)).First();

                toggle.Checked = true;
                Assert.IsTrue(settings.IsOn);
            }
        }

        [TestMethod]
        public async Task Grid_SourceChangedElsewhere_ShowsTheNewValue ()
        {
            var settings = new Settings();
            PropertyGrid grid = MakeGrid(settings);
            using (TestViews.AttachSized(grid, kGridHeightDp))
            {
                await TestViews.LayOutAsync(grid);
                settings.Name = "From code";
                Assert.AreEqual("From code", NameField(grid).Text);
            }
        }

        [TestMethod]
        public async Task Grid_ReadOnly_TakesNoEdits ()
        {
            PropertyGrid grid = MakeGrid(new Settings());
            grid.IsReadOnly = true;
            using (TestViews.AttachSized(grid, kGridHeightDp))
            {
                await TestViews.LayOutAsync(grid);
                Assert.IsFalse(NameField(grid).Enabled, "The text field");
                Assert.IsFalse(TestViews.Descendants<Switch>(RowAt(grid, kIsOnRow)).First().Enabled, "The switch");
            }
        }

        [TestMethod]
        public async Task Grid_Disabled_DisablesEveryEditor ()
        {
            PropertyGrid grid = MakeGrid(new Settings());
            grid.Enabled = false;
            using (TestViews.AttachSized(grid, kGridHeightDp))
            {
                await TestViews.LayOutAsync(grid);
                Assert.IsFalse(ListOf(grid).Enabled, "No row takes a touch, and the rows don't scroll");
                Assert.IsFalse(NameField(grid).Enabled, "The text field");
                Assert.IsFalse(TestViews.Descendants<Switch>(RowAt(grid, kIsOnRow)).First().Enabled, "The switch");
                Assert.IsTrue(TestViews.Descendants<NumericEditor>(RowAt(grid, kCountRow)).First().IsReadOnly, "The number");
                Button[] setAndUnset = TestViews.Descendants<Button>(RowAt(grid, kLimitRow)).Where(b => (b.Text == "Set") || (b.Text == "Unset")).ToArray();
                Assert.AreEqual(2, setAndUnset.Length, "The nullable row has its set and unset buttons");
                Assert.IsTrue(setAndUnset.All(b => b.Visibility != ViewStates.Visible), "Neither can be pressed");
            }
        }

        [TestMethod]
        public async Task Grid_EnabledAgain_TakesEditsAgain ()
        {
            PropertyGrid grid = MakeGrid(new Settings());
            grid.Enabled = false;
            using (TestViews.AttachSized(grid, kGridHeightDp))
            {
                grid.Enabled = true;
                await TestViews.LayOutAsync(grid);
                Assert.IsTrue(ListOf(grid).Enabled);
                Assert.IsTrue(NameField(grid).Enabled);
            }
        }

        [TestMethod]
        public async Task Grid_NullableRowWithNoValue_SaysNotSet ()
        {
            PropertyGrid grid = MakeGrid(new Settings());
            using (TestViews.AttachSized(grid, kGridHeightDp))
            {
                await TestViews.LayOutAsync(grid);
                TextView? notSet = TestViews.Descendants<TextView>(RowAt(grid, kLimitRow)).FirstOrDefault(t => t.Text == "Not set");
                Assert.IsNotNull(notSet);
                Assert.AreEqual(ViewStates.Visible, notSet.Visibility);
            }
        }

        [TestMethod]
        public async Task Grid_NullableRowSet_TakesAValueAndShowsUnset ()
        {
            var settings = new Settings();
            PropertyGrid grid = MakeGrid(settings);
            using (TestViews.AttachSized(grid, kGridHeightDp))
            {
                await TestViews.LayOutAsync(grid);
                Button set = TestViews.Descendants<Button>(RowAt(grid, kLimitRow)).First(b => b.Text == "Set");
                set.PerformClick();

                Assert.AreEqual(0, settings.Limit, "Set gives the inner type's default");
                Button unset = TestViews.Descendants<Button>(RowAt(grid, kLimitRow)).First(b => b.Text == "Unset");
                Assert.AreEqual(ViewStates.Visible, unset.Visibility);
                Assert.AreNotEqual(ViewStates.Visible, set.Visibility);
            }
        }

        [TestMethod]
        public void Grid_LeavingTheWindow_LetsGoOfItsSource ()
        {
            var settings = new Settings();
            PropertyGrid grid = MakeGrid(settings);
            IDisposable attached = TestViews.AttachSized(grid, kGridHeightDp);
            Assert.IsTrue(settings.SubscriberCount > 0, "The grid follows its source while in a window");

            attached.Dispose();
            Assert.AreEqual(0, settings.SubscriberCount, "Nothing is left listening once the grid is out of the window");
        }

        [TestMethod]
        public void Grid_BackInAWindow_ShowsItsRowsWithTheSelectionKept ()
        {
            PropertyGrid grid = MakeGrid(new Settings());
            IDisposable attached = TestViews.AttachSized(grid, kGridHeightDp);
            var isOn = (PropertyEditTarget)((IObservableTreeNode)grid.Manager.RootNode).Children[kIsOnRow];
            grid.SelectedTarget = isOn;

            attached.Dispose();
            Assert.AreEqual(0, ListOf(grid).Adapter!.Count, "No rows out of a window");

            using (TestViews.AttachSized(grid, kGridHeightDp))
            {
                Assert.AreEqual(kRowCount, ListOf(grid).Adapter!.Count);
                Assert.AreSame(isOn, grid.SelectedTarget);
            }
        }

        // ===========[ Helper Methods ]===================================
        private static PropertyGrid MakeGrid (Settings settings)
        {
            var grid = new PropertyGrid(TestHost.RequiredContext);
            grid.Editors.RegisterStandardEditors();
            grid.SetSingleItemSource(settings);
            return grid;
        }

        private static ListView ListOf (View view) => TestViews.Descendants<ListView>(view).First();

        private static View RowAt (PropertyGrid grid, int row)
        {
            ListView list = ListOf(grid);
            int shown = row - list.FirstVisiblePosition;
            return ((shown >= 0) && (shown < list.ChildCount) ? list.GetChildAt(shown) : null) ?? throw new AssertFailedException($"Row {row} is not shown");
        }

        private static EditText NameField (PropertyGrid grid) => TestViews.Descendants<EditText>(RowAt(grid, kNameRow)).First();

        private static void Dispatch (View view, long downTime, MotionEventActions action, float x, float y)
        {
            MotionEvent e = MotionEvent.Obtain(downTime, Android.OS.SystemClock.UptimeMillis(), action, x, y, MetaKeyStates.None)!;
            view.DispatchTouchEvent(e);
            e.Recycle();
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

            public event PropertyChangedEventHandler? PropertyChanged;

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

            public Detail Detail { get; set; } = new Detail();

            private void Set<T> (ref T field, T value, string propertyName)
            {
                field = value;
                this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        // A sub-object, an expandable row whose child is its one property
        private sealed class Detail
        {
            public string Note { get; set; } = "Inside";
        }
    }
}
