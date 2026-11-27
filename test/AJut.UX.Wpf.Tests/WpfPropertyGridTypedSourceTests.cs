namespace AJut.UX.Tests
{
    using System.Collections.Generic;
    using System.Linq;
    using AJut.UX.Controls;
    using AJut.UX.PropertyInteraction;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// SetSingleItemSource&lt;T&gt; and SetItemsSource&lt;T&gt; show a source the same as the object properties do. WPF
    /// objects need an STA thread, and nothing is ever shown.
    /// </summary>
    [TestClass]
    public class WpfPropertyGridTypedSourceTests
    {
        [TestMethod]
        public void WpfPropertyGrid_SetSingleItemSource_ShowsWhatSingleItemSourceShows ()
        {
            StaTestThread.Run(() =>
            {
                var sample = new Sample();
                var byObject = new PropertyGrid { SingleItemSource = sample };
                var byType = new PropertyGrid();
                byType.SetSingleItemSource(sample);

                Assert.AreSame(sample, byType.SingleItemSource);
                CollectionAssert.AreEqual(new[] { "Count", "Name" }, RowNames(byType), "The rows SetSingleItemSource shows");
                CollectionAssert.AreEqual(RowNames(byObject), RowNames(byType), "The same rows as SingleItemSource");
            });
        }

        [TestMethod]
        public void WpfPropertyGrid_SetItemsSource_ShowsWhatItemsSourceShows_AndReplacesASingleSource ()
        {
            StaTestThread.Run(() =>
            {
                var items = new List<Sample> { new Sample(), new Sample() };
                var byObject = new PropertyGrid { ItemsSource = items };
                var byType = new PropertyGrid();
                byType.SetSingleItemSource(new Sample());
                byType.SetItemsSource(items);

                Assert.AreSame(items, byType.ItemsSource);
                Assert.IsNull(byType.SingleItemSource, "An items source replaces a single source, as setting ItemsSource does");
                CollectionAssert.AreEqual(RowNames(byObject), RowNames(byType), "The same rows as ItemsSource");
            });
        }

        private static string[] RowNames (PropertyGrid grid)
        {
            return grid.PropertyTreeItems?.OfType<PropertyEditTarget>().Select(target => target.DisplayName).ToArray() ?? new string[0];
        }

        // ===========[ Subclasses/structs ]===================================
        public class Sample
        {
            public int Count { get; set; } = 3;
            public string Name { get; set; } = "Sample";
        }
    }
}
