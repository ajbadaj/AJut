namespace AJut.UX.Tests
{
    using System;
    using System.ComponentModel;
    using System.Threading.Tasks;
    using AJut.UX.CodeBinding;
    using AJut.UX.Controls;
    using AJut.UX.Helpers;
    using Android.OS;
    using Android.Views;
    using Android.Widget;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using AndroidColor = Android.Graphics.Color;

    /// <summary>
    /// The Android adapters on BindingSet, on real views. The set's own behavior (watching, the two-way guard, the
    /// main-thread marshal, single use) is covered headless in AJut.UX.Tests; these cover what only a device can:
    /// that each Android view's events land in the right place, and that Android's main-thread context carries a push.
    /// </summary>
    [TestClass]
    public class BindingSetXTTests
    {
        private BindingSet m_bindings = null!;
        private Model m_model = null!;

        [TestInitialize]
        public void Setup ()
        {
            m_bindings = new BindingSet();
            m_model = new Model();
        }

        [TestCleanup]
        public void Teardown ()
        {
            m_bindings.Dispose();
        }

        [TestMethod]
        public void BindText_PushesBothWaysWithoutEcho ()
        {
            var field = new EditText(TestHost.RequiredContext);
            int writes = 0;
            m_bindings.BindText(field, m_model, nameof(Model.Text), () => m_model.Text, v => { ++writes; m_model.Text = v; });
            Assert.AreEqual("start", field.Text);
            Assert.AreEqual(0, writes, "The opening push does not write back");

            m_model.Text = "from model";
            Assert.AreEqual("from model", field.Text);
            Assert.AreEqual(0, writes, "A model push does not echo back through TextChanged");

            field.Text = "typed";
            Assert.AreEqual("typed", m_model.Text);
            Assert.AreEqual(1, writes);
        }

        [TestMethod]
        public void BindText_ModelPushKeepsTheCaret ()
        {
            var field = new EditText(TestHost.RequiredContext);
            m_bindings.BindText(field, m_model, nameof(Model.Text), () => m_model.Text, v => m_model.Text = v);
            field.Text = "abcdef";
            field.SetSelection(3);

            m_model.Text = "abcdefgh";
            Assert.AreEqual(3, field.SelectionEnd, "The caret stays where it was");

            field.SetSelection(8);
            m_model.Text = "ab";
            Assert.AreEqual(2, field.SelectionEnd, "The caret is clamped to the new length");
        }

        [TestMethod]
        public void BindText_AfterDisposeTypingWritesNothing ()
        {
            var field = new EditText(TestHost.RequiredContext);
            m_bindings.BindText(field, m_model, nameof(Model.Text), () => m_model.Text, v => m_model.Text = v);
            m_bindings.Dispose();

            field.Text = "after";
            Assert.AreEqual("start", m_model.Text);
            Assert.AreEqual(0, m_model.PropertyChangedSubscriberCount);
        }

        [TestMethod]
        public void BindChecked_PushesBothWaysWithoutEcho ()
        {
            var check = new CheckBox(TestHost.RequiredContext);
            int writes = 0;
            m_bindings.BindChecked(check, m_model, nameof(Model.IsOn), () => m_model.IsOn, v => { ++writes; m_model.IsOn = v; });
            Assert.IsFalse(check.Checked);

            m_model.IsOn = true;
            Assert.IsTrue(check.Checked);
            Assert.AreEqual(0, writes);

            check.Checked = false;
            Assert.IsFalse(m_model.IsOn);
            Assert.AreEqual(1, writes);
        }

        [TestMethod]
        public void TwoWayBinds_TurnOffTheViewsOwnSavedState ()
        {
            // A restored view value would land after the binding's push and write itself over the model.
            var field = new EditText(TestHost.RequiredContext);
            var check = new CheckBox(TestHost.RequiredContext);
            Assert.IsTrue(field.SaveEnabled && check.SaveEnabled, "Views save their state by default");

            m_bindings.BindText(field, m_model, nameof(Model.Text), () => m_model.Text, v => m_model.Text = v);
            m_bindings.BindChecked(check, m_model, nameof(Model.IsOn), () => m_model.IsOn, v => m_model.IsOn = v);
            Assert.IsFalse(field.SaveEnabled);
            Assert.IsFalse(check.SaveEnabled);
        }

        [TestMethod]
        public void BindLabel_BindVisible_BindEnabled_FollowTheModel ()
        {
            var label = new TextView(TestHost.RequiredContext);
            var view = new View(TestHost.RequiredContext);
            m_bindings.BindLabel(label, m_model, nameof(Model.Text), () => m_model.Text);
            m_bindings.BindVisible(view, m_model, nameof(Model.IsOn), () => m_model.IsOn);
            m_bindings.BindEnabled(label, m_model, nameof(Model.IsOn), () => m_model.IsOn);
            Assert.AreEqual("start", label.Text);
            Assert.AreEqual(ViewStates.Gone, view.Visibility);
            Assert.IsFalse(label.Enabled);

            m_model.Text = "next";
            m_model.IsOn = true;
            Assert.AreEqual("next", label.Text);
            Assert.AreEqual(ViewStates.Visible, view.Visibility);
            Assert.IsTrue(label.Enabled);
        }

        [TestMethod]
        public void BindCommand_ClickExecutesAndEnabledFollowsCanExecute ()
        {
            var button = new Button(TestHost.RequiredContext);
            var command = new TestCommand();
            m_bindings.BindCommand(button, command);
            Assert.IsTrue(button.Enabled);

            button.PerformClick();
            Assert.AreEqual(1, command.ExecuteCount);

            command.SetCanExecute(false);
            Assert.IsFalse(button.Enabled);

            button.PerformClick();
            Assert.AreEqual(1, command.ExecuteCount, "A click while CanExecute is false does not execute");

            m_bindings.Dispose();
            command.SetCanExecute(true);
            button.PerformClick();
            Assert.AreEqual(1, command.ExecuteCount, "Nothing runs after Dispose");
            Assert.AreEqual(0, command.CanExecuteChangedSubscriberCount);
        }

        [TestMethod]
        public void OnClick_RunsTheActionUntilDisposed ()
        {
            var button = new Button(TestHost.RequiredContext);
            int clicks = 0;
            m_bindings.OnClick(button, () => ++clicks);

            button.PerformClick();
            Assert.AreEqual(1, clicks);

            m_bindings.Dispose();
            button.PerformClick();
            Assert.AreEqual(1, clicks);
        }

        [TestMethod]
        public void BindEditColor_PushesBothWaysWithoutEcho ()
        {
            var control = new ColorEditIngressControl(TestHost.RequiredContext);
            int writes = 0;
            m_bindings.BindEditColor(control, m_model, nameof(Model.Accent), () => m_model.Accent, v => { ++writes; m_model.Accent = v; });
            Assert.AreEqual(m_model.Accent, control.EditColor);

            m_model.Accent = new ColorArgb(255, 1, 2, 3);
            Assert.AreEqual(m_model.Accent, control.EditColor);
            Assert.AreEqual(0, writes);

            control.EditColor = new ColorArgb(255, 9, 8, 7);
            Assert.AreEqual(new ColorArgb(255, 9, 8, 7), m_model.Accent);
            Assert.AreEqual(1, writes);
        }

        [TestMethod]
        public void BindView_AnyView_BothWaysWithoutEcho ()
        {
            var rating = new RatingBar(TestHost.RequiredContext) { NumStars = 5, StepSize = 0.5f };
            int writes = 0;
            BindingSet.TwoWayChannel channel = m_bindings.BindView<RatingBar, RatingBar.RatingBarChangeEventArgs, float>(
                rating, _HookRatingChange, _UnhookRatingChange, _ReadRating, _WriteRating,
                m_model, nameof(Model.Stars), () => m_model.Stars, v => { ++writes; m_model.Stars = v; }
            );

            Assert.IsNotNull(channel);
            Assert.AreEqual(2f, rating.Rating, "The opening push shows the model");
            Assert.IsFalse(rating.SaveEnabled, "And turns off the view's own saved state, as every two-way bind does");

            m_model.Stars = 3.5f;
            Assert.AreEqual(3.5f, rating.Rating);
            Assert.AreEqual(0, writes, "A model push does not echo back through the edit event");

            rating.Rating = 4f;
            Assert.AreEqual(4f, m_model.Stars, "The view's edit event writes the model");
            Assert.AreEqual(1, writes);

            m_bindings.Dispose();
            rating.Rating = 1f;
            Assert.AreEqual(4f, m_model.Stars, "Disposed, the edit event is unhooked");

            static void _HookRatingChange (RatingBar view, EventHandler<RatingBar.RatingBarChangeEventArgs> handler) => view.RatingBarChange += handler;
            static void _UnhookRatingChange (RatingBar view, EventHandler<RatingBar.RatingBarChangeEventArgs> handler) => view.RatingBarChange -= handler;
            static float _ReadRating (RatingBar view) => view.Rating;
            static void _WriteRating (RatingBar view, float value) => view.Rating = value;
        }

        [TestMethod]
        public void BindProgress_SeekBar_BothWaysWithoutEcho ()
        {
            var seekBar = new SeekBar(TestHost.RequiredContext) { Max = 10 };
            int writes = 0;
            m_bindings.BindProgress(seekBar, m_model, nameof(Model.Level), () => m_model.Level, v => { ++writes; m_model.Level = v; });
            Assert.AreEqual(3, seekBar.Progress);

            m_model.Level = 7;
            Assert.AreEqual(7, seekBar.Progress);
            Assert.AreEqual(0, writes);

            seekBar.Progress = 2;
            Assert.AreEqual(2, m_model.Level);
            Assert.AreEqual(1, writes);
        }

        [TestMethod]
        public async Task BindSelectedPosition_Spinner_BothWays ()
        {
            var spinner = new Spinner(TestHost.RequiredContext)
            {
                Adapter = new ArrayAdapter<string>(TestHost.RequiredContext, Android.Resource.Layout.SimpleSpinnerItem, new[] { "Low", "Medium", "High" }),
            };
            using IDisposable attached = TestHost.Attach(spinner);
            m_bindings.BindSelectedPosition(spinner, m_model, nameof(Model.Index), () => m_model.Index, v => m_model.Index = v);
            Assert.AreEqual(1, spinner.SelectedItemPosition);

            m_model.Index = 2;
            Assert.AreEqual(2, spinner.SelectedItemPosition);

            // A spinner reports a pick when it lays it out
            const int kPickReportedWithinSeconds = 3;
            const int kPollEveryMilliseconds = 10;
            spinner.SetSelection(0);
            DateTime deadline = DateTime.UtcNow + TimeSpan.FromSeconds(kPickReportedWithinSeconds);
            while ((m_model.Index != 0) && (DateTime.UtcNow < deadline))
            {
                await Task.Delay(kPollEveryMilliseconds);
            }

            Assert.AreEqual(0, m_model.Index);

            m_model.Index = 7;
            Assert.AreEqual(0, spinner.SelectedItemPosition, "A position outside its items leaves it as it is");
        }

        [TestMethod]
        public void TwoWayBinds_HandBackTheirChannel ()
        {
            Assert.IsNotNull(m_bindings.BindText(new EditText(TestHost.RequiredContext), m_model, nameof(Model.Text), () => m_model.Text, v => m_model.Text = v));
            Assert.IsNotNull(m_bindings.BindChecked(new CheckBox(TestHost.RequiredContext), m_model, nameof(Model.IsOn), () => m_model.IsOn, v => m_model.IsOn = v));
            Assert.IsNotNull(m_bindings.BindEditColor(new ColorEditIngressControl(TestHost.RequiredContext), m_model, nameof(Model.Accent), () => m_model.Accent, v => m_model.Accent = v));
            Assert.IsNotNull(m_bindings.BindCommand(new Button(TestHost.RequiredContext), new TestCommand()));
        }

        [TestMethod]
        public async Task Watch_ChangeOffTheMainThreadPushesOnTheMainThread ()
        {
            var pushedOnMainThread = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            bool registered = false;
            m_bindings.Watch(m_model, nameof(Model.Text), () =>
            {
                if (registered)
                {
                    pushedOnMainThread.TrySetResult(Looper.MyLooper() == Looper.MainLooper);
                }
            });
            registered = true;

            await Task.Run(() => m_model.Text = "from a worker");
            Task finished = await Task.WhenAny(pushedOnMainThread.Task, Task.Delay(TimeSpan.FromSeconds(5)));
            Assert.AreSame(pushedOnMainThread.Task, finished, "The push never ran");
            Assert.IsTrue(pushedOnMainThread.Task.Result, "The push ran off the main thread");
        }

        [TestMethod]
        public async Task Hookup_OffTheMainThreadThrows ()
        {
            // A hookup adds to the set's own lists, which the main thread reads, so the set refuses one from a worker
            var label = new TextView(TestHost.RequiredContext);
            await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                () => Task.Run(() => m_bindings.BindLabel(label, m_model, nameof(Model.Text), () => m_model.Text))
            );
            Assert.AreEqual(0, m_model.PropertyChangedSubscriberCount, "The refused hookup subscribed nothing");
        }

        // ===========[ Subclasses/structs ]===================================
        private sealed class Model : INotifyPropertyChanged
        {
            private string m_text = "start";
            private bool m_isOn;
            private ColorArgb m_accent = new ColorArgb(255, 13, 131, 216);
            private float m_stars = 2f;
            private int m_level = 3;
            private int m_index = 1;

            public event PropertyChangedEventHandler? PropertyChanged;

            public string Text
            {
                get => m_text;
                set
                {
                    if (m_text != value)
                    {
                        m_text = value;
                        this.Raise(nameof(Text));
                    }
                }
            }

            public bool IsOn
            {
                get => m_isOn;
                set
                {
                    if (m_isOn != value)
                    {
                        m_isOn = value;
                        this.Raise(nameof(IsOn));
                    }
                }
            }

            public ColorArgb Accent
            {
                get => m_accent;
                set
                {
                    if (m_accent != value)
                    {
                        m_accent = value;
                        this.Raise(nameof(Accent));
                    }
                }
            }

            public float Stars
            {
                get => m_stars;
                set
                {
                    if (m_stars != value)
                    {
                        m_stars = value;
                        this.Raise(nameof(Stars));
                    }
                }
            }

            public int Level
            {
                get => m_level;
                set
                {
                    if (m_level != value)
                    {
                        m_level = value;
                        this.Raise(nameof(Level));
                    }
                }
            }

            public int Index
            {
                get => m_index;
                set
                {
                    if (m_index != value)
                    {
                        m_index = value;
                        this.Raise(nameof(Index));
                    }
                }
            }

            public int PropertyChangedSubscriberCount => this.PropertyChanged?.GetInvocationList().Length ?? 0;

            private void Raise (string propertyName) => this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private sealed class TestCommand : System.Windows.Input.ICommand
        {
            private bool m_canExecute = true;

            public event EventHandler? CanExecuteChanged;

            public int ExecuteCount { get; private set; }

            public int CanExecuteChangedSubscriberCount => this.CanExecuteChanged?.GetInvocationList().Length ?? 0;

            public bool CanExecute (object? parameter) => m_canExecute;

            public void Execute (object? parameter) => ++this.ExecuteCount;

            public void SetCanExecute (bool canExecute)
            {
                m_canExecute = canExecute;
                this.CanExecuteChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }
}
