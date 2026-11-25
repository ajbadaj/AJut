namespace AJut.UX.Tests
{
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Data;
    using AJut.UX.Controls;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// The WPF PageStack in its default template, with no theme loaded. WPF objects need an STA thread, so each test runs on
    /// one, and nothing is ever shown.
    /// </summary>
    [TestClass]
    public class WpfPageStackTests
    {
        private static readonly Size kMeasureSize = new Size(600, 400);

        [TestMethod]
        public void WpfPageStack_WithDots_AppliesItsTemplateAndPages ()
        {
            StaTestThread.Run(() =>
            {
                const int kPageCount = 20;
                var stack = new PageStack { ShowDots = true, DotReach = 3 };
                for (int index = 0; index < kPageCount; ++index)
                {
                    stack.Children.Add(new TextBlock { Text = $"Page {index + 1}" });
                }

                // The dot strip slides under a transform on every refresh, from the template's first apply onward
                var host = new StackPanel();
                host.Children.Add(stack);
                LayOut(host);

                stack.SelectedIndex = 10;
                LayOut(host);

                Assert.AreEqual(10, stack.SelectedIndex);
            });
        }

        [TestMethod]
        public void WpfPageStack_BoundSelectedIndex_KeepsItsBindingAndHearsTheStacksOwnMove ()
        {
            StaTestThread.Run(() =>
            {
                var source = new IndexSource { Index = 2 };
                var stack = new PageStack();
                for (int index = 0; index < 5; ++index)
                {
                    stack.Children.Add(new TextBlock { Text = $"Page {index + 1}" });
                }

                // No mode given, as a consumer binds a selector's index
                BindingOperations.SetBinding(stack, PageStack.SelectedIndexProperty, new Binding(nameof(IndexSource.Index)) { Source = source });

                // A page added before the shown one moves the shown page's index along with it
                stack.Children.Insert(0, new TextBlock { Text = "New first page" });

                Assert.IsNotNull(BindingOperations.GetBindingExpression(stack, PageStack.SelectedIndexProperty), "The stack's own move replaced the binding on SelectedIndex");
                Assert.AreEqual(3, source.Index, "The bound index did not hear the stack's own move");
            });
        }

        // ===========[ Helpers ]===================================
        private static void LayOut (FrameworkElement host)
        {
            host.Measure(kMeasureSize);
            host.Arrange(new Rect(kMeasureSize));
            host.UpdateLayout();
        }

        private sealed class IndexSource
        {
            public int Index { get; set; }
        }
    }
}
