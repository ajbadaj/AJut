namespace AJut.UX.Tests
{
    using AJut.UX.Helpers;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class ScreenPickSessionTests
    {
        // ===[ Picking ]===

        [TestMethod]
        public void SPS_LeftClick_PicksWhereTheButtonWasLetGo ()
        {
            var session = new ScreenPickSession();

            session.LeftButtonDown();
            Assert.IsFalse(session.IsDone);

            session.LeftButtonUp(1920, 640);

            Assert.AreEqual(eScreenPickState.Picked, session.State);
            Assert.AreEqual(1920, session.PickedX);
            Assert.AreEqual(640, session.PickedY);
        }

        [TestMethod]
        public void SPS_ReleasesWhosePressCameBeforeThePick_AreIgnored ()
        {
            // The press that started the pick can leave its release to the overlay
            var session = new ScreenPickSession();

            session.LeftButtonUp(10, 10);
            session.RightButtonUp();
            session.EscapeUp();

            Assert.AreEqual(eScreenPickState.Picking, session.State);

            session.LeftButtonDown();
            session.LeftButtonUp(30, 40);

            Assert.AreEqual(eScreenPickState.Picked, session.State);
            Assert.AreEqual(30, session.PickedX);
            Assert.AreEqual(40, session.PickedY);
        }

        // ===[ Canceling ]===

        [TestMethod]
        public void SPS_RightClick_Cancels ()
        {
            var session = new ScreenPickSession();

            session.RightButtonDown();
            session.RightButtonUp();

            Assert.AreEqual(eScreenPickState.Canceled, session.State);
        }

        [TestMethod]
        public void SPS_Escape_CancelsWhenLetGo ()
        {
            var session = new ScreenPickSession();

            session.EscapeDown();
            Assert.IsFalse(session.IsDone);

            session.EscapeUp();
            Assert.AreEqual(eScreenPickState.Canceled, session.State);
        }

        [TestMethod]
        public void SPS_Interrupted_Cancels ()
        {
            var session = new ScreenPickSession();
            session.LeftButtonDown();

            session.Interrupt();
            session.LeftButtonUp(5, 5);

            Assert.AreEqual(eScreenPickState.Canceled, session.State);
        }

        // ===[ After the end ]===

        [TestMethod]
        public void SPS_LosingTheOverlayAfterAPick_KeepsThePick ()
        {
            // Tearing the overlay down gives up the mouse and activation, which interrupts
            var session = new ScreenPickSession();
            session.LeftButtonDown();
            session.LeftButtonUp(7, 9);

            session.Interrupt();
            session.RightButtonDown();
            session.RightButtonUp();

            Assert.AreEqual(eScreenPickState.Picked, session.State);
            Assert.AreEqual(7, session.PickedX);
            Assert.AreEqual(9, session.PickedY);
        }

        [TestMethod]
        public void SPS_ClickAfterACancel_DoesNotPick ()
        {
            var session = new ScreenPickSession();
            session.EscapeDown();
            session.EscapeUp();

            session.LeftButtonDown();
            session.LeftButtonUp(1, 1);

            Assert.AreEqual(eScreenPickState.Canceled, session.State);
        }

        // ===[ Pressing ]===

        [TestMethod]
        public void SPS_Pressing_IsTheHoldBetweenPressAndPick ()
        {
            var session = new ScreenPickSession();
            Assert.IsFalse(session.IsPressing);

            session.LeftButtonDown();
            Assert.IsTrue(session.IsPressing);

            session.LeftButtonUp(3, 4);
            Assert.IsFalse(session.IsPressing);
        }

        [TestMethod]
        public void SPS_CancelingMidPress_EndsThePress ()
        {
            var session = new ScreenPickSession();
            session.LeftButtonDown();

            session.EscapeDown();
            session.EscapeUp();

            Assert.IsFalse(session.IsPressing);
        }

        // ===[ Reading the capture ]===

        [TestMethod]
        public void SPS_ReadingACapture_TakesBgraOrderAndRows_AndIsOpaque ()
        {
            // Two pixels by two, as GDI lays a 32 bit top down capture out: blue, green, red, then a byte that is not alpha
            byte[] pixels =
            {
                0x01, 0x02, 0x03, 0x00,   0x04, 0x05, 0x06, 0x00,
                0x07, 0x08, 0x09, 0x00,   0x10, 0x20, 0x30, 0x00,
            };

            ColorArgb color = BgraPixels.ReadOpaque(pixels, 2 * BgraPixels.kBytesPerPixel, 1, 1);

            Assert.AreEqual(new ColorArgb(255, 0x30, 0x20, 0x10), color);
        }
    }
}
