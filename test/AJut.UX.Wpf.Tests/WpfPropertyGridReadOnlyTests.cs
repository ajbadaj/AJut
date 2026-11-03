namespace AJut.UX.Tests
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Media;
    using System.Windows.Threading;
    using AJut.UX.AttachedProperties;
    using AJut.UX.Controls;
    using AJut.UX.PropertyInteraction;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// A read-only WPF PropertyGrid is read-only through the inherited Edit.IsReadOnly, so it has to reach every row the
    /// first time it changes, and the editors have to honor it: the list editor's add button, the built-in button row and
    /// the path picker. WPF objects need an STA thread, so each test runs on one, and nothing is ever shown.
    /// </summary>
    [TestClass]
    public class WpfPropertyGridReadOnlyTests
    {
        private static readonly Size kMeasureSize = new Size(400, 400);

        [TestMethod]
        public void WpfPropertyGrid_ReadOnly_ListAddButtonIsDisabled ()
        {
            StaTestThread.Run(() =>
            {
                PropertyEditTarget listTarget = PropertyEditTarget.GenerateForPropertiesOf(new ListSource()).First(t => t.PropertyPathTarget == nameof(ListSource.Tags));
                var editor = new PropertyGridListEditor { DataContext = listTarget };
                StackPanel host = MakeHost(editor);

                var addButton = (Button)editor.Template.FindName("PART_AddButton", editor);
                Assert.IsTrue(addButton.IsEnabled, "The add button should work in a grid that isn't read-only");

                Edit.SetIsReadOnly(host, true);
                Assert.IsFalse(addButton.IsEnabled, "A read-only grid can still add list elements");
            });
        }

        [TestMethod]
        public void WpfPropertyGrid_ReadOnly_BuiltInButtonRowIsDisabled ()
        {
            StaTestThread.Run(() =>
            {
                int clicks = 0;
                PropertyEditTarget buttonTarget = PropertyEditTarget.CreateButton("Click me", () => ++clicks);
                DataTemplate template = new PropertyGridTemplateSelector().SelectTemplate(buttonTarget, null);
                var button = (Button)template.LoadContent();
                button.DataContext = buttonTarget;
                StackPanel host = MakeHost(button);

                Assert.IsTrue(button.IsEnabled, "The button row should work in a grid that isn't read-only");

                Edit.SetIsReadOnly(host, true);
                Assert.IsFalse(button.IsEnabled, "A read-only grid can still run a button row");
            });
        }

        [TestMethod]
        public void WpfPropertyGrid_IsReadOnly_TheFirstChangeEachWayReachesTheRows ()
        {
            StaTestThread.Run(() =>
            {
                var grid = new PropertyGrid { ItemTemplateSelector = new PropertyGridTemplateSelector() };
                grid.SetSingleItemSource(new ListSource());
                var host = new Border { Child = grid };
                LayOut(host);

                PropertyGridListEditor listEditor = FindInVisualTree<PropertyGridListEditor>(grid).FirstOrDefault();
                Assert.IsNotNull(listEditor, "The grid should have built the list row");
                var addButton = (Button)listEditor.Template.FindName("PART_AddButton", listEditor);
                Assert.IsTrue(addButton.IsEnabled, "The add button should work in a grid that isn't read-only");

                grid.IsReadOnly = true;
                LayOut(host);
                Assert.IsTrue(Edit.GetIsReadOnly(listEditor), "The first change to read-only should reach the rows");
                Assert.IsFalse(addButton.IsEnabled, "The first change to read-only should stop the list adding");

                grid.IsReadOnly = false;
                LayOut(host);
                Assert.IsFalse(Edit.GetIsReadOnly(listEditor), "The first change back should reach the rows too");
                Assert.IsTrue(addButton.IsEnabled, "The first change back should let the list add again");
            });
        }

        [TestMethod]
        public void WpfPathSelectionControl_ReadOnly_TakesNoTypingAndNoBrowsing ()
        {
            StaTestThread.Run(() =>
            {
                var path = new PathSelectionControl { SelectedPath = @"C:\AJutTest\ReadOnlyPath" };
                StackPanel host = MakeHost(path);
                var textBox = (TextBox)path.Template.FindName("PART_PathTextBox", path);
                Assert.IsFalse(textBox.IsReadOnly, "The path should take typing where nothing is read-only");
                Assert.IsTrue(PathSelectionControl.PromptUserForNewPathCommand.CanExecute(null, path), "Browsing should work where nothing is read-only");

                Edit.SetIsReadOnly(host, true);
                Assert.IsTrue(textBox.IsReadOnly, "A read-only path takes no typing");
                Assert.IsFalse(PathSelectionControl.PromptUserForNewPathCommand.CanExecute(null, path), "A read-only path can't be browsed for a new one");
            });
        }

        // ===========[ Helpers ]===============================================
        // The stand-in for the grid: Edit.IsReadOnly set on it reaches the editor by inheritance, as from the grid. A
        // measure applies the editor's template.
        private static StackPanel MakeHost (FrameworkElement editor)
        {
            var host = new StackPanel();
            host.Children.Add(editor);
            host.Measure(kMeasureSize);
            return host;
        }

        // Measures and arranges the host, then runs what the dispatcher has queued, so a grid builds its rows the way a
        // shown one would
        private static void LayOut (FrameworkElement host)
        {
            host.Measure(kMeasureSize);
            host.Arrange(new Rect(kMeasureSize));
            host.UpdateLayout();

            var frame = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, new DispatcherOperationCallback(EndFrame), frame);
            Dispatcher.PushFrame(frame);
        }

        private static object EndFrame (object frame)
        {
            ((DispatcherFrame)frame).Continue = false;
            return null;
        }

        private static IEnumerable<T> FindInVisualTree<T> (DependencyObject root) where T : DependencyObject
        {
            int childCount = VisualTreeHelper.GetChildrenCount(root);
            for (int index = 0; index < childCount; ++index)
            {
                DependencyObject child = VisualTreeHelper.GetChild(root, index);
                if (child is T match)
                {
                    yield return match;
                }

                foreach (T descendant in FindInVisualTree<T>(child))
                {
                    yield return descendant;
                }
            }
        }

        // ===========[ Subclasses/structs ]====================================
        private sealed class ListSource
        {
            [PGList]
            public List<string> Tags { get; set; } = new List<string> { "alpha", "beta" };
        }
    }
}
