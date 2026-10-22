namespace AJut.UX.Tests
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Windows;
    using System.Windows.Controls;
    using AJut.UX.AttachedProperties;
    using AJut.UX.Controls;
    using AJut.UX.PropertyInteraction;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// A read-only WPF PropertyGrid is read-only through the inherited Edit.IsReadOnly, so the editors it builds itself
    /// have to honor it: the list editor's add button and the built-in button row. WPF objects need an STA thread, so
    /// each test runs on one, and nothing is ever shown.
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

        // ===========[ Subclasses/structs ]====================================
        private sealed class ListSource
        {
            [PGList]
            public List<string> Tags { get; set; } = new List<string> { "alpha", "beta" };
        }
    }
}
