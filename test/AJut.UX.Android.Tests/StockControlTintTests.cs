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

        [TestMethod]
        public void StockControlTint_Themed_GivesAPlainButtonAButtonFaceForEachState ()
        {
            ThemeColors.IsThemeLoaded = true;
            var button = new Button(TestHost.RequiredContext);

            StockControlTint.Apply(Wrap(button));

            Assert.IsInstanceOfType<Android.Graphics.Drawables.StateListDrawable>(button.Background);
            Assert.IsNull(button.StateListAnimator, "A press shows with color, as BrushButton's does, not the stock lift");
        }

        [TestMethod]
        public void StockControlTint_Unthemed_LeavesAPlainButtonsFaceAsItIs ()
        {
            ThemeColors.IsThemeLoaded = false;
            var button = new Button(TestHost.RequiredContext);
            Android.Graphics.Drawables.Drawable? before = button.Background;

            StockControlTint.Apply(Wrap(button));

            Assert.AreSame(before, button.Background);
        }

        [TestMethod]
        public void StockControlTint_Themed_LeavesAToggleButtonsFaceAsItIs ()
        {
            ThemeColors.IsThemeLoaded = true;
            var toggle = new ToggleButton(TestHost.RequiredContext);
            Android.Graphics.Drawables.Drawable? before = toggle.Background;

            StockControlTint.Apply(Wrap(toggle));

            Assert.AreSame(before, toggle.Background, "A check-style button is not a plain button face");
        }

        [TestMethod]
        public void StockControlTint_Themed_LeavesABrushButtonToPaintItself ()
        {
            ThemeColors.IsThemeLoaded = true;
            var button = new BrushButton(TestHost.RequiredContext);
            Android.Graphics.Drawables.Drawable? before = button.Background;

            StockControlTint.Apply(Wrap(button));

            Assert.AreSame(before, button.Background, "BrushButton is a Button, and AJut's own, so it keeps its own face");
        }

        [TestMethod]
        public void StockControlTint_Themed_DrawsAnEditTextAsAnAJutTextBox ()
        {
            ThemeColors.IsThemeLoaded = true;
            var field = new EditText(TestHost.RequiredContext);

            StockControlTint.Apply(Wrap(field));

            Assert.IsInstanceOfType<Android.Graphics.Drawables.StateListDrawable>(field.Background);
        }

        [TestMethod]
        public void StockControlTint_Unthemed_LeavesAnEditTextAsItIs ()
        {
            ThemeColors.IsThemeLoaded = false;
            var field = new EditText(TestHost.RequiredContext);
            Android.Graphics.Drawables.Drawable? before = field.Background;

            StockControlTint.Apply(Wrap(field));

            Assert.AreSame(before, field.Background);
        }

        [TestMethod]
        public void StockControlTint_Themed_LeavesANumericEditorsFieldToItsEditor ()
        {
            ThemeColors.IsThemeLoaded = true;
            var editor = new NumericEditor(TestHost.RequiredContext);
            Android.Graphics.Drawables.Drawable? before = editor.Field.Background;

            StockControlTint.Apply(Wrap(editor));

            Assert.AreSame(before, editor.Field.Background, "NumericEditor's own EditText keeps NumericEditor's look");
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
