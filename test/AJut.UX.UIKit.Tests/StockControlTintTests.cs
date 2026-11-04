namespace AJut.UX.Tests
{
    using AJut.UX.Theming;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using UIKit;

    /// <summary>
    /// What StockControlTint sets and what it leaves alone. The colors themselves are looks, judged by eye, so nothing here
    /// checks a value: only that the switch appearance and a window's tint take the AJut colors with the theme loaded, and
    /// go back or stay put without it. UISwitch's appearance is app wide, so every test puts it back.
    /// </summary>
    [TestClass]
    public class StockControlTintTests
    {
        private UIColor m_switchOnTintAtStart;

        [TestInitialize]
        public void Setup ()
        {
            m_switchOnTintAtStart = UISwitch.Appearance.OnTintColor;
        }

        [TestCleanup]
        public void Teardown ()
        {
            ThemedColors.IsThemeLoaded = false;
            UISwitch.Appearance.OnTintColor = m_switchOnTintAtStart;
        }

        [TestMethod]
        public void StockControlTint_Themed_GivesSwitchesTheAJutOnColor ()
        {
            ThemedColors.IsThemeLoaded = true;

            StockControlTint.Apply();

            Assert.IsNotNull(UISwitch.Appearance.OnTintColor);
        }

        [TestMethod]
        public void StockControlTint_Unthemed_GivesSwitchesBackTheSystemsColor ()
        {
            ThemedColors.IsThemeLoaded = true;
            StockControlTint.Apply();

            ThemedColors.IsThemeLoaded = false;
            StockControlTint.Apply();

            Assert.IsNull(UISwitch.Appearance.OnTintColor);
        }

        [TestMethod]
        public void StockControlTint_Themed_TintsAWindowWithTheAccent ()
        {
            ThemedColors.IsThemeLoaded = true;
            var window = new UIWindow(TestAppScene);

            StockControlTint.ApplyTo(window);

            Assert.AreEqual(StockControlTint.Accent, window.TintColor);
        }

        [TestMethod]
        public void StockControlTint_Unthemed_LeavesAWindowsTintAsTheAppSetIt ()
        {
            ThemedColors.IsThemeLoaded = false;
            var window = new UIWindow(TestAppScene) { TintColor = UIColor.SystemPink };

            StockControlTint.ApplyTo(window);

            Assert.AreEqual(UIColor.SystemPink, window.TintColor);
        }

        [TestMethod]
        public void StockControlTint_Themed_GivesAPlainSystemButtonAButtonFace ()
        {
            ThemedColors.IsThemeLoaded = true;
            UIButton button = UIButton.FromType(UIButtonType.System);
            button.SetTitle("Go", UIControlState.Normal);

            StockControlTint.Apply(Wrap(button));

            Assert.IsNotNull(button.Configuration, "The face is drawn through a configuration");
            Assert.IsNotNull(button.ConfigurationUpdateHandler, "The face follows the button's state");
            Assert.AreEqual("Go", button.Configuration.Title, "The title carries over");
        }

        [TestMethod]
        public void StockControlTint_Unthemed_LeavesAPlainSystemButtonAsItIs ()
        {
            ThemedColors.IsThemeLoaded = false;
            UIButton button = UIButton.FromType(UIButtonType.System);

            StockControlTint.Apply(Wrap(button));

            Assert.IsNull(button.Configuration);
        }

        [TestMethod]
        public void StockControlTint_Themed_LeavesAButtonWithItsOwnConfigurationAlone ()
        {
            ThemedColors.IsThemeLoaded = true;
            var button = new UIButton(CoreGraphics.CGRect.Empty) { Configuration = UIButtonConfiguration.FilledButtonConfiguration };

            StockControlTint.Apply(Wrap(button));

            Assert.IsNull(button.ConfigurationUpdateHandler);
        }

        [TestMethod]
        public void StockControlTint_Themed_LeavesAJutButtonsToPaintThemselves ()
        {
            ThemedColors.IsThemeLoaded = true;
            var button = new AJut.UX.Controls.BrushButton();

            StockControlTint.Apply(Wrap(button));

            Assert.IsNull(button.ConfigurationUpdateHandler, "BrushButton is a UIButton, and AJut's own, so it keeps its own face");
        }

        [TestMethod]
        public void StockControlTint_Themed_DrawsARoundedTextFieldAsAnAJutTextBox ()
        {
            ThemedColors.IsThemeLoaded = true;
            var field = new UITextField { BorderStyle = UITextBorderStyle.RoundedRect };

            StockControlTint.Apply(Wrap(field));

            Assert.AreEqual(UITextBorderStyle.None, field.BorderStyle, "The system's own fill and outline give way to AJut's");
            Assert.IsNotNull(field.BackgroundColor);
        }

        [TestMethod]
        public void StockControlTint_Themed_AppliedTwice_TakesOverATextFieldOnce ()
        {
            ThemedColors.IsThemeLoaded = true;
            var field = new UITextField { BorderStyle = UITextBorderStyle.RoundedRect };
            UIView container = Wrap(field);

            StockControlTint.Apply(container);
            int recognizersAfterFirst = field.GestureRecognizers?.Length ?? 0;
            StockControlTint.Apply(container);

            Assert.AreEqual(recognizersAfterFirst, field.GestureRecognizers?.Length ?? 0, "A second Apply adds no second hover recognizer");
        }

        [TestMethod]
        public void StockControlTint_Unthemed_LeavesARoundedTextFieldAsItIs ()
        {
            ThemedColors.IsThemeLoaded = false;
            var field = new UITextField { BorderStyle = UITextBorderStyle.RoundedRect };

            StockControlTint.Apply(Wrap(field));

            Assert.IsNull(field.BackgroundColor);
        }

        // ===[ Helpers ]===

        private static UIView Wrap (params UIView[] children)
        {
            var container = new UIView();
            foreach (UIView child in children)
            {
                container.AddSubview(child);
            }

            return container;
        }

        private static UIWindowScene TestAppScene => TestHost.TestArea?.Window?.WindowScene ?? throw new System.InvalidOperationException("Tests only run inside the test app.");
    }
}
