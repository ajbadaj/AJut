namespace AJut.UX.Tests
{
    using System.Windows.Controls;
    using System.Windows.Data;
    using AJut.UX.Controls;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// The WPF PageStack with no theme loaded. WPF objects need an STA thread, so each test runs on one, and nothing is ever
    /// shown.
    /// </summary>
    [TestClass]
    public class WpfPageStackTests
    {
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
        private sealed class IndexSource
        {
            public int Index { get; set; }
        }
    }
}
