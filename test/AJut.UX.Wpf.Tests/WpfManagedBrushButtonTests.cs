namespace AJut.UX.Tests
{
    using System.Windows.Media;
    using AJut.UX.Controls;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// The WPF managed brush buttons paint the brushes of whichever state CurrentState answers, so a subclass can show one
    /// state with no pointer, the way a showroom's state sampler does. WPF objects need an STA thread, so each test runs
    /// on one, and nothing is ever shown.
    /// </summary>
    [TestClass]
    public class WpfManagedBrushButtonTests
    {
        [TestMethod]
        public void WpfBrushButton_CurrentStateOverridden_PaintsThatStatesBrushesWithNoPointer ()
        {
            StaTestThread.Run(() =>
            {
                var pressedBackground = new SolidColorBrush(Colors.Red);
                var normalBackground = new SolidColorBrush(Colors.Blue);

                var button = new HeldBrushButton(eManagedButtonState.Pressed)
                {
                    NormalBackground = normalBackground,
                    PressedBackground = pressedBackground,
                };

                Assert.IsFalse(button.IsPressed, "Nothing presses the button");
                Assert.AreSame(pressedBackground, button.EffectiveBackground, "A button whose CurrentState answers Pressed paints its pressed brushes anyway");
            });
        }

        [TestMethod]
        public void WpfBrushButton_CurrentStateNotOverridden_PaintsTheNormalBrushesAtRest ()
        {
            StaTestThread.Run(() =>
            {
                var normalBackground = new SolidColorBrush(Colors.Blue);
                var button = new BrushButton
                {
                    NormalBackground = normalBackground,
                    PressedBackground = new SolidColorBrush(Colors.Red),
                };

                Assert.AreSame(normalBackground, button.EffectiveBackground, "With no pointer and nothing overridden, a button at rest paints its normal brushes");
            });
        }

        // ===========[ Subclasses/structs ]====================================
        private sealed class HeldBrushButton : BrushButton
        {
            private readonly eManagedButtonState m_heldState;

            public HeldBrushButton (eManagedButtonState heldState)
            {
                m_heldState = heldState;
            }

            protected override eManagedButtonState CurrentState => m_heldState;
        }
    }
}
