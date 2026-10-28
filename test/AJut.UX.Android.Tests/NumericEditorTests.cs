namespace AJut.UX.Tests
{
    using System;
    using System.ComponentModel;
    using System.Threading.Tasks;
    using AJut;
    using AJut.UX.CodeBinding;
    using AJut.UX.Controls;
    using Android.OS;
    using Android.Views;
    using Android.Views.InputMethods;
    using Android.Widget;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// The Android NumericEditor on real views. The editing rules are AJut.UX's NumericEditorController, tested headless
    /// there; these cover the Android side: the step buttons and holding them, the text field, commit, and binding.
    /// </summary>
    [TestClass]
    public class NumericEditorTests
    {
        [TestMethod]
        public void Value_ShowsInTheField ()
        {
            var editor = new NumericEditor(TestHost.RequiredContext) { Value = 12 };
            Assert.AreEqual("12", editor.Field.Text);
        }

        [TestMethod]
        public void StepButtons_KeepTheValuesType ()
        {
            var editor = new NumericEditor(TestHost.RequiredContext) { Value = 5 };
            using IDisposable attached = TestHost.Attach(editor);

            _Tap(_StepButton(editor, increase: true));
            Assert.AreEqual(6, editor.Value, "An int stays an int");

            _Tap(_StepButton(editor, increase: false));
            _Tap(_StepButton(editor, increase: false));
            Assert.AreEqual(4, editor.Value);
        }

        [TestMethod]
        public void Steps_AreCappedToTheBounds ()
        {
            var editor = new NumericEditor(TestHost.RequiredContext) { Maximum = 10, Nudge = 5, Value = 9.0 };
            using IDisposable attached = TestHost.Attach(editor);

            _Tap(_StepButton(editor, increase: true));
            Assert.AreEqual(10.0, editor.Value);
        }

        [TestMethod]
        public async Task HoldingAStepButton_RepeatsUntilLifted ()
        {
            var editor = new NumericEditor(TestHost.RequiredContext) { Value = 0 };
            using IDisposable attached = TestHost.Attach(editor);
            TextView plus = _StepButton(editor, increase: true);

            _Touch(plus, MotionEventActions.Down);
            await Task.Delay(800);
            _Touch(plus, MotionEventActions.Up);
            int whenLifted = (int)editor.Value;
            await Task.Delay(300);

            Assert.IsTrue(whenLifted > 2, $"Holding repeats the step; got {whenLifted}");
            Assert.AreEqual(whenLifted, (int)editor.Value, "Lifting stops it");
        }

        [TestMethod]
        public void Typing_SetsTheValue ()
        {
            var editor = new NumericEditor(TestHost.RequiredContext) { Value = 1 };
            using IDisposable attached = TestHost.Attach(editor);

            editor.Field.Text = "42";
            Assert.AreEqual(42, editor.Value);
        }

        [TestMethod]
        public void ValueSetAsText_ValueChangedEndsOnTheParsedNumber ()
        {
            var editor = new NumericEditor(TestHost.RequiredContext);
            object? lastReported = null;
            editor.ValueChanged += _OnChanged;

            editor.Value = "42";
            editor.ValueChanged -= _OnChanged;

            Assert.AreEqual(42f, editor.Value, "Text is written back as the number it parsed to");
            Assert.AreEqual(editor.Value, lastReported, "The last ValueChanged reports the value the editor kept");

            void _OnChanged (object? sender, EventArgs<object> e) => lastReported = e.Value;
        }

        [TestMethod]
        public void EnforceNumericType_KeepsTextAsThatType ()
        {
            var editor = new NumericEditor(TestHost.RequiredContext) { EnforceNumericType = typeof(int) };
            using IDisposable attached = TestHost.Attach(editor);
            editor.Value = "42";
            Assert.AreEqual(42, editor.Value, "Text with an int type stays an int");

            editor.Field.Text = "7";
            Assert.AreEqual(7, editor.Value, "So do edits");
        }

        [TestMethod]
        public void EnforceNumericType_SetAfterTheValue_ConvertsIt ()
        {
            var editor = new NumericEditor(TestHost.RequiredContext);
            object? lastReported = null;
            editor.ValueChanged += _OnChanged;

            editor.Value = "42";
            editor.EnforceNumericType = typeof(int);
            editor.ValueChanged -= _OnChanged;

            Assert.AreEqual(42, editor.Value, "The value already there becomes an int");
            Assert.AreEqual(42, lastReported, "And the change is reported");

            void _OnChanged (object? sender, EventArgs<object> e) => lastReported = e.Value;
        }

        [TestMethod]
        public void OutOfBoundsText_IsFlagged_OrFixedOnCommit ()
        {
            var editor = new NumericEditor(TestHost.RequiredContext) { Maximum = 10, Value = 1.0 };
            using IDisposable attached = TestHost.Attach(editor);

            editor.Field.Text = "500";
            Assert.IsNotNull(editor.ErrorMessage, "Flagged and left for the user to fix");

            editor.OutOfBoundsResponse = eOutOfBoundsResponse.FixOnCommit;
            editor.Field.OnEditorAction(ImeAction.Done);
            Assert.AreEqual(10.0, editor.Value, "Done snaps it to the bound");
            Assert.AreEqual("10", editor.Field.Text);
        }

        [TestMethod]
        public void InflatesFromLayoutWithItsAttributes ()
        {
            View root = LayoutInflater.From(TestHost.RequiredContext)!.Inflate(Resource.Layout.numeric_editor_test, null)!;
            var editor = root.FindViewById<NumericEditor>(Resource.Id.numeric_editor)!;
            Assert.AreEqual(-5.0, editor.Minimum);
            Assert.AreEqual(50.0, editor.Maximum);
            Assert.AreEqual(2.5, editor.Nudge);
            Assert.AreEqual(1, editor.DecimalPlacesAllowed);
            Assert.AreEqual("Volume", editor.Label);
            Assert.AreEqual(eOutOfBoundsResponse.FixOnCommit, editor.OutOfBoundsResponse);
            Assert.AreEqual(typeof(int), editor.EnforceNumericType);
            Assert.AreEqual(0, editor.Value, "The starting value is kept in the enforced type");
        }

        [TestMethod]
        public void BindValue_BothWays_InThePropertysType ()
        {
            var model = new Model();
            var editor = new NumericEditor(TestHost.RequiredContext);
            using IDisposable attached = TestHost.Attach(editor);
            using var bindings = new BindingSet();
            bindings.BindValue(editor, model, nameof(Model.Count), () => model.Count, value => model.Count = value);
            Assert.AreEqual(3, editor.Value);

            model.Count = 7;
            Assert.AreEqual("7", editor.Field.Text);

            _Tap(_StepButton(editor, increase: true));
            Assert.AreEqual(8, model.Count);
        }

        // ===========[ Helper Methods ]===================================
        // The editor is a label, then a frame of minus, field and plus.
        private static TextView _StepButton (NumericEditor editor, bool increase)
        {
            var frame = (ViewGroup)editor.GetChildAt(1)!;
            return (TextView)frame.GetChildAt(increase ? 2 : 0)!;
        }

        private static void _Tap (View view)
        {
            _Touch(view, MotionEventActions.Down);
            _Touch(view, MotionEventActions.Up);
        }

        private static void _Touch (View view, MotionEventActions action)
        {
            long now = SystemClock.UptimeMillis();
            using MotionEvent touch = MotionEvent.Obtain(now, now, action, 1, 1, 0)!;
            view.DispatchTouchEvent(touch);
        }

        // ===========[ Subclasses/structs ]===================================
        private sealed class Model : INotifyPropertyChanged
        {
            private int m_count = 3;

            public event PropertyChangedEventHandler? PropertyChanged;

            public int Count
            {
                get => m_count;
                set
                {
                    if (m_count != value)
                    {
                        m_count = value;
                        this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Count)));
                    }
                }
            }
        }
    }
}
