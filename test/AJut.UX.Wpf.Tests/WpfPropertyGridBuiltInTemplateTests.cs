namespace AJut.UX.Tests
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Windows;
    using AJut.UX.Controls;
    using AJut.UX.PropertyInteraction;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// The rows a WPF PropertyGrid builds itself for the Nullable and List editors when it has no template of its own
    /// registered for them. The selector is made in code with no application resources around it, and nothing is ever
    /// shown.
    /// </summary>
    [TestClass]
    public class WpfPropertyGridBuiltInTemplateTests
    {
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

        // ===========[ Helpers ]===============================================
        private static PropertyEditTarget TargetFor (string propertyName)
            => PropertyEditTarget.GenerateForPropertiesOf(new Source()).First(t => t.PropertyPathTarget == propertyName);

        private static DataTemplate SelectTemplate (PropertyEditTarget target)
        {
            DataTemplate template = new PropertyGridTemplateSelector().SelectTemplate(target, null);
            Assert.IsNotNull(template, $"No built-in template for the {target.Editor} editor");
            return template;
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
