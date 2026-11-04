namespace AJut.UX.Tests
{
    using AJut.UX.Controls;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// The WPF NumericEditor's EnforceNumericType keeps Value in one type however it arrives, whichever of the type and the
    /// value is set first, and clearing it goes back to detecting the type. WPF objects need an STA thread, so each test
    /// runs on one, and nothing is ever shown.
    /// </summary>
    [TestClass]
    public class WpfNumericEditorEnforceTypeTests
    {
        [TestMethod]
        public void WpfNumericEditor_EnforcedTypeSetBeforeTheValue_KeepsTheValueInIt ()
        {
            StaTestThread.Run(() =>
            {
                var editor = new NumericEditor();
                editor.EnforceNumericType = typeof(int);

                editor.Value = "42";

                Assert.AreEqual(42, editor.Value);
            });
        }

        // XAML sets attributes in the order they are written, so Value="42" can arrive before EnforceNumericType
        [TestMethod]
        public void WpfNumericEditor_EnforcedTypeSetAfterTheValue_ConvertsIt ()
        {
            StaTestThread.Run(() =>
            {
                var editor = new NumericEditor();
                editor.Value = "42";

                editor.EnforceNumericType = typeof(int);

                Assert.AreEqual(42, editor.Value, "The value already there takes the enforced type");
                Assert.AreEqual("42", editor.DisplayValue.Text);
            });
        }

        [TestMethod]
        public void WpfNumericEditor_EnforcedTypeCleared_DetectsTheTypeAgain ()
        {
            StaTestThread.Run(() =>
            {
                var editor = new NumericEditor();
                editor.EnforceNumericType = typeof(int);
                editor.Value = 5;

                editor.EnforceNumericType = null;
                editor.DisplayValue.Text = "7";

                Assert.AreEqual(7, editor.Value, "The type is detected from the value again, which is an int");
            });
        }
    }
}
