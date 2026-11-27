namespace AJut.UX.Tests
{
    using System;
    using System.ComponentModel;
    using AJut.UX.CodeBinding;
    using Android.Widget;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// The Android adapters on BindingSet, on real views. The set's own behavior (watching, the two-way guard, the
    /// main-thread marshal, single use) is covered headless in AJut.UX.Tests; these cover what only a device can:
    /// that each Android view's events land in the right place.
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
        public void BindView_AnyView_BothWaysWithoutEcho ()
        {
            var rating = new RatingBar(TestHost.RequiredContext) { NumStars = 5, StepSize = 0.5f };
            int writes = 0;
            m_bindings.BindView<RatingBar, RatingBar.RatingBarChangeEventArgs, float>(
                rating, _HookRatingChange, _UnhookRatingChange, _ReadRating, _WriteRating,
                m_model, nameof(Model.Stars), () => m_model.Stars, v => { ++writes; m_model.Stars = v; }
            );

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
        public void BindSelectedPosition_Spinner_ShowsTheModelAndIgnoresAPositionOutsideItsItems ()
        {
            var spinner = new Spinner(TestHost.RequiredContext)
            {
                Adapter = new ArrayAdapter<string>(TestHost.RequiredContext, Android.Resource.Layout.SimpleSpinnerItem, new[] { "Low", "Medium", "High" }),
            };
            m_bindings.BindSelectedPosition(spinner, m_model, nameof(Model.Index), () => m_model.Index, v => m_model.Index = v);
            Assert.AreEqual(1, spinner.SelectedItemPosition);

            m_model.Index = 2;
            Assert.AreEqual(2, spinner.SelectedItemPosition);

            m_model.Index = 7;
            Assert.AreEqual(2, spinner.SelectedItemPosition, "A position outside its items leaves it as it is");
        }

        // ===========[ Subclasses/structs ]===================================
        private sealed class Model : INotifyPropertyChanged
        {
            private string m_text = "start";
            private float m_stars = 2f;
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
