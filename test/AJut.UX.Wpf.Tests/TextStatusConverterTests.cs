namespace AJut.UX.Tests
{
    using System.Globalization;
    using System.Windows;
    using AJut.UX.Converters;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>WPF's IsTextNullOrEmptyToVisibilityConverter gives WhenHasText for text and WhenNullOrEmpty for none</summary>
    [TestClass]
    public class TextStatusConverterTests
    {
        [TestMethod]
        public void WpfTextVisibility_Defaults_TextIsVisible_NoTextIsCollapsed ()
        {
            var converter = new IsTextNullOrEmptyToVisibilityConverter();

            Assert.AreEqual(Visibility.Visible, Convert(converter, "Ctrl+S"), "Text gives WhenNullOrEmpty");
            Assert.AreEqual(Visibility.Collapsed, Convert(converter, string.Empty), "Empty text gives WhenHasText");
            Assert.AreEqual(Visibility.Collapsed, Convert(converter, null), "Null text gives WhenHasText");
        }

        [TestMethod]
        public void WpfTextVisibility_GivesTheValueNamedForTheText ()
        {
            var converter = new IsTextNullOrEmptyToVisibilityConverter { WhenHasText = Visibility.Hidden, WhenNullOrEmpty = Visibility.Visible };

            Assert.AreEqual(Visibility.Hidden, Convert(converter, "text"));
            Assert.AreEqual(Visibility.Visible, Convert(converter, string.Empty));
        }

        private static object Convert (IsTextNullOrEmptyToVisibilityConverter converter, string text)
            => converter.Convert(text, typeof(Visibility), null, CultureInfo.InvariantCulture);
    }
}
