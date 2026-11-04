namespace AJut.UX.Tests
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Threading;
    using AJut.UX.Controls;
    using AJut.UX.PropertyInteraction;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// The rows a WPF PropertyGrid builds itself for the Nullable, Button and List editors when it has no template of its
    /// own registered for them. The selector is made in code with no application resources around it, and nothing is
    /// ever shown.
    /// </summary>
    [TestClass]
    public class WpfPropertyGridBuiltInTemplateTests
    {
        private static readonly Size kMeasureSize = new Size(400, 400);

        [TestMethod]
        public void WpfPropertyGrid_BuiltInNullableRow_IsANullableEditor ()
        {
            StaTestThread.Run(() =>
            {
                PropertyEditTarget target = TargetFor(nameof(Source.Count));
                Assert.AreEqual(PropertyGridRowRules.kNullableEditorKey, target.Editor);

                object row = SelectTemplate(target).LoadContent();
                Assert.IsInstanceOfType(row, typeof(NullableEditor));
            });
        }

        [TestMethod]
        public void WpfPropertyGrid_BuiltInListRow_IsAListEditor ()
        {
            StaTestThread.Run(() =>
            {
                PropertyEditTarget target = TargetFor(nameof(Source.Tags));
                Assert.AreEqual(PropertyGridRowRules.kListEditorKey, target.Editor);

                object row = SelectTemplate(target).LoadContent();
                Assert.IsInstanceOfType(row, typeof(PropertyGridListEditor));
            });
        }

        [TestMethod]
        public void WpfPropertyGrid_BuiltInButtonRow_ShowsItsLabel_AndRunsItsAction ()
        {
            StaTestThread.Run(() =>
            {
                int clicks = 0;
                PropertyEditTarget target = PropertyEditTarget.CreateButton("Click me", () => ++clicks);

                // The grid sets up every row before it shows one, which is what reads the label into EditValue
                target.Setup();
                var button = (Button)SelectTemplate(target).LoadContent();
                button.DataContext = target;
                MakeHost(button);

                // A row loaded from a template attaches its data context bindings on a later dispatcher pass, the way it
                //  would in a shown grid
                PumpDispatcher();

                Assert.AreEqual("Click me", button.Content);
                Assert.IsNotNull(button.Command, "The button should run the row's command");
                Assert.IsTrue(button.Command.CanExecute(button.CommandParameter));

                button.Command.Execute(button.CommandParameter);
                Assert.AreEqual(1, clicks, "Running the button should run the row's action once");
            });
        }

        [TestMethod]
        public void WpfPropertyGrid_BuiltInRows_AreSharedBetweenSelectors ()
        {
            StaTestThread.Run(() =>
            {
                PropertyEditTarget target = TargetFor(nameof(Source.Count));
                DataTemplate first = SelectTemplate(target);
                DataTemplate second = SelectTemplate(target);

                Assert.AreSame(first, second, "Every grid should share the one built-in template");
                Assert.IsTrue(first.IsSealed, "A shared template has to be sealed");
            });
        }

        // ===========[ Helpers ]===============================================
        private static PropertyEditTarget TargetFor (string propertyName)
            => PropertyEditTarget.GenerateForPropertiesOf(new Source()).First(t => t.PropertyPathTarget == propertyName);

        private static DataTemplate SelectTemplate (PropertyEditTarget target)
        {
            DataTemplate template = new PropertyGridTemplateSelector().SelectTemplate(target, null);
            Assert.IsNotNull(template, $"No built-in template for the {target.Editor} editor");
            return template;
        }

        // Puts the row in a measured panel, so its bindings and template are applied the way a grid's would be
        private static StackPanel MakeHost (FrameworkElement row)
        {
            var host = new StackPanel();
            host.Children.Add(row);
            host.Measure(kMeasureSize);
            return host;
        }

        // Runs everything queued on this thread's dispatcher
        private static void PumpDispatcher ()
        {
            var frame = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, new DispatcherOperationCallback(EndFrame), frame);
            Dispatcher.PushFrame(frame);
        }

        private static object EndFrame (object frame)
        {
            ((DispatcherFrame)frame).Continue = false;
            return null;
        }

        // ===========[ Subclasses/structs ]====================================
        private sealed class Source
        {
            public int? Count { get; set; } = 3;

            [PGList]
            public List<string> Tags { get; set; } = new List<string> { "alpha", "beta" };
        }
    }
}
