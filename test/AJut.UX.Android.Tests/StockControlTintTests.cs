namespace AJut.UX.Tests
{
    using AJut.UX.Controls;
    using AJut.UX.Theming;
    using Android.Widget;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// What StockControlTint tints and what it leaves alone. The colors themselves are looks, judged by eye, so nothing
    /// here checks a value: only that each stock widget it knows takes a tint with the AJut theme loaded, none does
    /// without it, and AJut's own controls are never reached into.
    /// </summary>
    [TestClass]
    public class StockControlTintTests
    {
        [TestCleanup]
        public void Teardown ()
        {
            ThemeColors.IsThemeLoaded = false;
        }

        [TestMethod]
        public void StockControlTint_Unthemed_LeavesEveryWidgetAsItIs ()
        {
            ThemeColors.IsThemeLoaded = false;
            var check = new CheckBox(TestHost.RequiredContext);
            var toggle = new Switch(TestHost.RequiredContext);

            StockControlTint.Apply(Wrap(check, toggle));

            Assert.IsNull(check.ButtonTintList);
            Assert.IsNull(toggle.TrackTintList);
        }

        [TestMethod]
        public void StockControlTint_Themed_TintsEachStockWidget_ThroughNestedLayouts ()
        {
            ThemeColors.IsThemeLoaded = true;
            var check = new CheckBox(TestHost.RequiredContext);
            var radio = new RadioButton(TestHost.RequiredContext);
            var toggle = new Switch(TestHost.RequiredContext);
            var spinner = new Spinner(TestHost.RequiredContext);

            StockControlTint.Apply(Wrap(check, Wrap(radio, Wrap(toggle, spinner))));

            Assert.IsNotNull(check.ButtonTintList);
            Assert.IsNotNull(radio.ButtonTintList);
            Assert.IsNotNull(toggle.ThumbTintList);
            Assert.IsNotNull(toggle.TrackTintList);
            Assert.IsNotNull(spinner.BackgroundTintList);
        }

        [TestMethod]
        public void StockControlTint_Themed_TintsTheViewItIsGiven ()
        {
            ThemeColors.IsThemeLoaded = true;
            var check = new CheckBox(TestHost.RequiredContext);

            StockControlTint.Apply(check);

            Assert.IsNotNull(check.ButtonTintList);
        }

        [TestMethod]
        public void StockControlTint_Themed_LeavesAJutControlsToPaintThemselves ()
        {
            ThemeColors.IsThemeLoaded = true;
            var comboBox = new EnumComboBox(TestHost.RequiredContext) { SelectedItem = System.DayOfWeek.Friday };
            var before = comboBox.Spinner.BackgroundTintList;

            StockControlTint.Apply(Wrap(comboBox));

            Assert.AreSame(before, comboBox.Spinner.BackgroundTintList, "EnumComboBox's own Spinner keeps EnumComboBox's look");
        }

        // ===[ Helpers ]===

        private static LinearLayout Wrap (params Android.Views.View[] children)
        {
            var layout = new LinearLayout(TestHost.RequiredContext);
            foreach (Android.Views.View child in children)
            {
                layout.AddView(child);
            }

            return layout;
        }
    }
}
