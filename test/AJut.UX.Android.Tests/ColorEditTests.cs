namespace AJut.UX.Tests
{
    using System;
    using System.Threading.Tasks;
    using AJut;
    using AJut.UX;
    using AJut.UX.Controls;
    using AJut.UX.Helpers;
    using Android.Views;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using AndroidColor = Android.Graphics.Color;

    [TestClass]
    public class ColorEditTests
    {
        private static readonly TimeSpan kDismissTimeout = TimeSpan.FromSeconds(3);

        [TestMethod]
        public void HsvColorPicker_ColorRoundTrips ()
        {
            var picker = new HsvColorPicker(TestHost.RequiredContext);
            var color = AndroidColor.Argb(0x80, 0x33, 0x66, 0xCC);
            picker.Color = color;
            _AssertClose(color, picker.Color);
        }

        [TestMethod]
        public void HsvColorPicker_GrayKeepsTheHue ()
        {
            var picker = new HsvColorPicker(TestHost.RequiredContext) { Color = AndroidColor.Argb(255, 0, 0, 255) };
            Assert.AreEqual(240f, picker.Hue, 0.5f);

            picker.Color = AndroidColor.Argb(255, 128, 128, 128);
            Assert.AreEqual(240f, picker.Hue, 0.5f, "A gray has no hue, so the picker stays on the one it had");

            picker.Color = AndroidColor.Black;
            Assert.AreEqual(240f, picker.Hue, 0.5f, "Black has no hue either");
        }

        [TestMethod]
        public void HsvColorPicker_AlphaOffKeepsItOpaque ()
        {
            var picker = new HsvColorPicker(TestHost.RequiredContext) { Color = AndroidColor.Argb(0x40, 10, 200, 30) };
            picker.IsAlphaEnabled = false;
            Assert.AreEqual(255, (int)picker.Color.A);

            picker.Color = AndroidColor.Argb(0x40, 10, 200, 30);
            Assert.AreEqual(255, (int)picker.Color.A);
        }

        [TestMethod]
        public void HsvColorPicker_RaisesColorChanged ()
        {
            var picker = new HsvColorPicker(TestHost.RequiredContext);
            AndroidColor? raised = null;
            picker.ColorChanged += _Capture;
            picker.Color = AndroidColor.Argb(255, 200, 40, 40);
            picker.ColorChanged -= _Capture;

            Assert.IsNotNull(raised);
            _AssertClose(AndroidColor.Argb(255, 200, 40, 40), raised.Value);

            void _Capture (object? sender, EventArgs<AndroidColor> e) => raised = e.Value;
        }

        [TestMethod]
        public void Ingress_SettingEditColorRaisesChangeButNoUserEdit ()
        {
            var control = new ColorEditIngressControl(TestHost.RequiredContext);
            int changes = 0;
            int userEdits = 0;
            control.EditColorChanged += _OnChanged;
            control.UserEditComplete += _OnUserEdit;

            control.EditColor = new ColorArgb(255, 1, 2, 3);
            control.EditColor = new ColorArgb(255, 1, 2, 3);
            control.EditColorChanged -= _OnChanged;
            control.UserEditComplete -= _OnUserEdit;

            Assert.AreEqual(1, changes, "Setting the same color again is not a change");
            Assert.AreEqual(0, userEdits, "A change from code is not a user edit");

            void _OnChanged (object? sender, EventArgs<ColorArgb> e) => ++changes;
            void _OnUserEdit (object? sender, UserEditAppliedEventArgs e) => ++userEdits;
        }

        [TestMethod]
        public async Task Ingress_ClosingAfterAChangeCompletesOneUserEdit ()
        {
            var start = new ColorArgb(255, 10, 20, 30);
            var end = new ColorArgb(255, 40, 50, 60);
            var control = new ColorEditIngressControl(TestHost.RequiredContext) { EditColor = start };
            UserEditAppliedEventArgs? edit = null;
            int edits = 0;
            control.UserEditComplete += _OnUserEdit;

            control.OpenEditor();
            Assert.IsTrue(control.ShowEditDisplay);

            control.EditColor = new ColorArgb(255, 99, 99, 99);
            control.EditColor = end;
            control.CloseEditor();
            await _WaitUntilClosedAsync(control);
            control.UserEditComplete -= _OnUserEdit;

            Assert.AreEqual(1, edits, "Many changes in one open edit are one user edit");
            Assert.AreEqual(start, (ColorArgb)edit!.OldValue);
            Assert.AreEqual(end, (ColorArgb)edit.NewValue);

            void _OnUserEdit (object? sender, UserEditAppliedEventArgs e)
            {
                ++edits;
                edit = e;
            }
        }

        [TestMethod]
        public async Task Ingress_ClosingWithNoChangeIsNoUserEdit ()
        {
            var control = new ColorEditIngressControl(TestHost.RequiredContext) { EditColor = new ColorArgb(255, 10, 20, 30) };
            int edits = 0;
            control.UserEditComplete += _OnUserEdit;

            control.OpenEditor();
            control.CloseEditor();
            await _WaitUntilClosedAsync(control);
            control.UserEditComplete -= _OnUserEdit;

            Assert.AreEqual(0, edits);

            void _OnUserEdit (object? sender, UserEditAppliedEventArgs e) => ++edits;
        }

        [TestMethod]
        public void Ingress_ReadOnlyDoesNotOpen ()
        {
            var control = new ColorEditIngressControl(TestHost.RequiredContext) { IsReadOnly = true };
            control.PerformClick();
            Assert.IsFalse(control.ShowEditDisplay);
        }

        [TestMethod]
        public async Task Ingress_ClickOpensThePicker ()
        {
            var control = new ColorEditIngressControl(TestHost.RequiredContext);
            control.PerformClick();
            Assert.IsTrue(control.ShowEditDisplay);

            control.CloseEditor();
            await _WaitUntilClosedAsync(control);
        }

        [TestMethod]
        public void Ingress_InflatesFromLayoutWithItsAttributes ()
        {
            View root = LayoutInflater.From(TestHost.RequiredContext)!.Inflate(Resource.Layout.color_edit_test, null)!;
            var control = root.FindViewById<ColorEditIngressControl>(Resource.Id.color_edit)!;
            Assert.AreEqual(new ColorArgb(0x80, 0x12, 0x34, 0x56), control.EditColor);
            Assert.IsTrue(control.IsReadOnly);
        }

        // ===========[ Helper Methods ]===================================
        // A dialog's dismiss listener runs on a later pass of the main looper, so let the looper run until it has.
        private static async Task _WaitUntilClosedAsync (ColorEditIngressControl control)
        {
            DateTime deadline = DateTime.UtcNow + kDismissTimeout;
            while (control.ShowEditDisplay && (DateTime.UtcNow < deadline))
            {
                await Task.Delay(10);
            }

            Assert.IsFalse(control.ShowEditDisplay, "The picker did not close");
        }

        // Hue, saturation and value floats can land one step off on a channel, which is not a difference anyone sees.
        private static void _AssertClose (AndroidColor expected, AndroidColor actual)
        {
            Assert.AreEqual((int)expected.A, (int)actual.A, 1, "Alpha");
            Assert.AreEqual((int)expected.R, (int)actual.R, 1, "Red");
            Assert.AreEqual((int)expected.G, (int)actual.G, 1, "Green");
            Assert.AreEqual((int)expected.B, (int)actual.B, 1, "Blue");
        }
    }
}
