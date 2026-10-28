namespace AJut.UX.Tests
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Xml.Linq;
    using AJut.UX.Controls;
    using Android.Runtime;
    using Android.Views;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// AJut.UX.Android is trimmed in an app's Release build, and its ILLink.Descriptors.xml keeps the views a layout can
    /// name, since nothing else uses them by type. These keep that list in step with the views the package registers.
    /// </summary>
    [TestClass]
    public class TrimmingTests
    {
        [TestMethod]
        public void LayoutViews_AreAllKeptByTheTrimDescriptor ()
        {
            Assembly package = typeof(ToggleStrip).Assembly;
            using Stream? descriptor = package.GetManifestResourceStream("ILLink.Descriptors.xml");
            Assert.IsNotNull(descriptor, "The descriptor is embedded under the name the trimmer looks for");

            string[] kept = XDocument.Load(descriptor).Descendants("type")
                .Select(type => (string)type.Attribute("fullname")!)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();

            string[] registeredViews = package.GetTypes()
                .Where(type => !type.IsAbstract && typeof(View).IsAssignableFrom(type) && (type.GetCustomAttribute<RegisterAttribute>(inherit: false) != null))
                .Select(type => type.FullName!)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();

            CollectionAssert.AreEqual(registeredViews, kept, $"Registered: {string.Join(", ", registeredViews)}. Kept: {string.Join(", ", kept)}");
        }
    }
}
