namespace AJut.UX.Tests
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// The rules every stack's dialog service shares through <see cref="DialogServiceBase{TDialog}"/>: one dialog showing at
    /// a time, what happens to a dialog asked for while another shows, the nav back stack, and how each dialog ends. The
    /// dialogs are stand-ins the tests close by hand, the way a person or a finished close animation would.
    /// </summary>
    [TestClass]
    public class DialogServiceBaseTests
    {
        // ===========[ Showing and queueing ]==================================
        [TestMethod]
        public void ShowAsync_NothingShowing_ShowsAtOnceAndEndsWithTheAnswer ()
        {
            var service = new TestDialogService();
            var form = new TestDialog("form");

            Task<eDialogOutcome> formOutcome = service.ShowAsync(form);
            Assert.IsTrue(form.IsShowing, "Nothing was in the way");
            AssertWaiting(formOutcome, "Not answered yet");

            form.Close(eDialogOutcome.Primary);
            AssertEnded(eDialogOutcome.Primary, formOutcome, "The person's answer");
            Assert.IsFalse(service.CanPopBack);
        }

        [TestMethod]
        public void ShowAsync_QueueWhileAnotherShows_WaitsItsTurn ()
        {
            var service = new TestDialogService();
            var first = new TestDialog("first");
            var second = new TestDialog("second");
            Task<eDialogOutcome> firstOutcome = service.ShowAsync(first);

            Task<eDialogOutcome> secondOutcome = service.ShowAsync(second, eDialogOverlapResponse.Queue);
            Assert.IsFalse(second.IsShowing, "Waits for the first");
            Assert.IsFalse(first.IsHideRequested, "Queueing interrupts nothing");

            first.Close(eDialogOutcome.Secondary);
            AssertEnded(eDialogOutcome.Secondary, firstOutcome, "The first's own answer");
            Assert.IsTrue(second.IsShowing, "Its turn");
            AssertWaiting(secondOutcome, "Not answered yet");
        }

        [TestMethod]
        public void ShowAsync_QueueWhileANavBackStackIsHeld_WaitsForTheWholeStack ()
        {
            var service = new TestDialogService();
            var form = new TestDialog("form");
            var picker = new TestDialog("picker");
            var notice = new TestDialog("notice");
            service.ShowAsync(form);
            service.ShowAsync(picker, eDialogOverlapResponse.HideForNavBack);
            form.FinishClosing();

            Task<eDialogOutcome> noticeOutcome = service.ShowAsync(notice, eDialogOverlapResponse.Queue);
            picker.Close(eDialogOutcome.Primary);
            Assert.IsTrue(form.IsShowing, "The form the picker was over comes back first");
            Assert.IsFalse(notice.IsShowing, "The notice doesn't cut into the form's flow");

            form.Close(eDialogOutcome.Primary);
            Assert.IsTrue(notice.IsShowing, "Nothing is left, so the notice shows");
            AssertWaiting(noticeOutcome, "Not answered yet");
        }

        [TestMethod]
        public void ShowAsync_SkipIfBusyWhileAnotherShows_EndsNotShownAndChangesNothing ()
        {
            var service = new TestDialogService();
            var form = new TestDialog("form");
            var tip = new TestDialog("tip");
            service.ShowAsync(form);

            Task<eDialogOutcome> skippedOutcome = service.ShowAsync(tip, eDialogOverlapResponse.SkipIfBusy);
            AssertEnded(eDialogOutcome.NotShown, skippedOutcome, "Something was showing");
            Assert.AreEqual(0, tip.ShowCount);
            Assert.IsFalse(form.IsHideRequested, "The form is left alone");

            form.Close(eDialogOutcome.None);
            Task<eDialogOutcome> tipOutcome = service.ShowAsync(tip, eDialogOverlapResponse.SkipIfBusy);
            Assert.IsTrue(tip.IsShowing, "With nothing showing, it shows");
            AssertWaiting(tipOutcome, "Not answered yet");
        }

        [TestMethod]
        public void ShowAsync_ADialogAlreadyHeld_GetsTheSameTaskAndChangesNothing ()
        {
            var service = new TestDialogService();
            var picker = new TestDialog("picker");
            Task<eDialogOutcome> firstAsk = service.ShowAsync(picker);

            Task<eDialogOutcome> secondAsk = service.ShowAsync(picker, eDialogOverlapResponse.Replace);
            Assert.AreSame(firstAsk, secondAsk, "Both callers wait on the one showing");
            Assert.AreEqual(1, picker.ShowCount);
            Assert.IsFalse(picker.IsHideRequested);
            Assert.IsFalse(service.CanPopBack);
        }

        [TestMethod]
        public void ShowAsync_ShowingFails_EndsNotShownAndTheNextOneShows ()
        {
            var service = new TestDialogService();
            var form = new TestDialog("form");
            var broken = new TestDialog("broken") { FailsToShow = true };
            var next = new TestDialog("next");
            service.ShowAsync(form);
            Task<eDialogOutcome> brokenOutcome = service.ShowAsync(broken);
            Task<eDialogOutcome> nextOutcome = service.ShowAsync(next);

            form.Close(eDialogOutcome.Primary);
            AssertEnded(eDialogOutcome.NotShown, brokenOutcome, "It could not be shown");
            Assert.IsTrue(next.IsShowing, "One failure doesn't stop the rest");
            AssertWaiting(nextOutcome, "Not answered yet");
        }

        [TestMethod]
        public void ShowAsync_HoldingRefused_ThrowsAndHoldsNothing ()
        {
            var service = new TestDialogService();
            var unready = new TestDialog("unready") { IsRefusedBeforeHolding = true };
            var form = new TestDialog("form");

            Assert.ThrowsExactly<ArgumentException>(() => { _ = service.ShowAsync(unready); });
            Assert.AreEqual(0, unready.ShowCount);

            service.ShowAsync(form);
            Assert.IsTrue(form.IsShowing, "Nothing was left in the way");
        }

        // ===========[ Hide for nav back ]=====================================
        [TestMethod]
        public void ShowAsync_HideForNavBack_HidesTheShowingDialogBeforeShowing ()
        {
            var service = new TestDialogService();
            var form = new TestDialog("form");
            var picker = new TestDialog("picker");
            Task<eDialogOutcome> formOutcome = service.ShowAsync(form);

            service.ShowAsync(picker, eDialogOverlapResponse.HideForNavBack);
            Assert.IsTrue(form.IsHideRequested);
            Assert.IsFalse(picker.IsShowing, "Waits for the form to finish closing");
            Assert.IsTrue(service.CanPopBack);

            form.FinishClosing();
            Assert.IsTrue(picker.IsShowing);
            AssertWaiting(formOutcome, "Hidden for nav back is not an ending");
        }

        [TestMethod]
        public void ShowAsync_HideForNavBack_TopClosed_TheSameHiddenDialogComesBackAndItsCallerGetsItsAnswer ()
        {
            var service = new TestDialogService();
            var form = new TestDialog("form");
            var picker = new TestDialog("picker");
            Task<eDialogOutcome> formOutcome = service.ShowAsync(form);
            Task<eDialogOutcome> pickerOutcome = service.ShowAsync(picker, eDialogOverlapResponse.HideForNavBack);
            form.FinishClosing();

            picker.Close(eDialogOutcome.None);
            AssertEnded(eDialogOutcome.None, pickerOutcome, "The person closed it with no choice");
            Assert.IsTrue(form.IsShowing, "The form comes back");
            Assert.AreEqual(2, form.ShowCount, "The same instance, shown again");
            Assert.IsFalse(service.CanPopBack);
            AssertWaiting(formOutcome, "Still the form's first wait");

            form.Close(eDialogOutcome.Primary);
            AssertEnded(eDialogOutcome.Primary, formOutcome, "The answer the form was finally given");
        }

        [TestMethod]
        public void ShowAsync_HideForNavBack_TheShowingDialogClosesAtOnceWhenHidden_TheNewOneShows ()
        {
            var service = new TestDialogService();
            var form = new TestDialog("form") { ClosesAtOnceWhenHidden = true };
            var picker = new TestDialog("picker");
            Task<eDialogOutcome> formOutcome = service.ShowAsync(form);

            service.ShowAsync(picker, eDialogOverlapResponse.HideForNavBack);
            Assert.IsTrue(picker.IsShowing, "The form was out of the way before the request returned");
            Assert.IsFalse(form.IsShowing);
            Assert.IsTrue(service.CanPopBack);
            AssertWaiting(formOutcome, "Hidden for nav back is not an ending");
        }

        [TestMethod]
        public void ShowAsync_HideForNavBack_TheShowingDialogRefusesToHide_TheNewOneWaitsAndTheAnswerWins ()
        {
            var service = new TestDialogService();
            var form = new TestDialog("form") { RefusesToHide = true };
            var picker = new TestDialog("picker");
            Task<eDialogOutcome> formOutcome = service.ShowAsync(form);

            Task<eDialogOutcome> pickerOutcome = service.ShowAsync(picker, eDialogOverlapResponse.HideForNavBack);
            Assert.IsTrue(form.IsHideRequested);
            Assert.IsTrue(form.IsShowing, "It refused");
            Assert.IsFalse(picker.IsShowing, "Waits until the form really closes");

            form.Close(eDialogOutcome.Primary);
            AssertEnded(eDialogOutcome.Primary, formOutcome, "An answer is the person's, even with a hide asked for");
            Assert.IsTrue(picker.IsShowing);
            Assert.IsFalse(service.CanPopBack, "The form is done, so there is nothing to go back to");
            AssertWaiting(pickerOutcome, "Not answered yet");
        }

        // ===========[ Pop back ]==============================================
        [TestMethod]
        public void PopBack_WithADialogBeneath_EndsTheTopAsPoppedBackAndShowsTheOneBeneath ()
        {
            var service = new TestDialogService();
            var form = new TestDialog("form");
            var picker = new TestDialog("picker");
            Task<eDialogOutcome> formOutcome = service.ShowAsync(form);
            Task<eDialogOutcome> pickerOutcome = service.ShowAsync(picker, eDialogOverlapResponse.HideForNavBack);
            form.FinishClosing();

            Assert.IsTrue(service.PopBack());
            Assert.IsTrue(picker.IsHideRequested);
            Assert.IsFalse(service.CanPopBack);

            picker.FinishClosing();
            AssertEnded(eDialogOutcome.PoppedBack, pickerOutcome, "Popped, not answered");
            Assert.IsTrue(form.IsShowing);
            AssertWaiting(formOutcome, "The form's wait carries on");
        }

        [TestMethod]
        public void PopBack_NothingBeneath_ReturnsFalseAndChangesNothing ()
        {
            var service = new TestDialogService();
            var form = new TestDialog("form");
            Task<eDialogOutcome> formOutcome = service.ShowAsync(form);

            Assert.IsFalse(service.PopBack());
            Assert.IsTrue(form.IsShowing);
            Assert.IsFalse(form.IsHideRequested);
            AssertWaiting(formOutcome, "Not answered yet");
        }

        [TestMethod]
        public void PopBack_BeforeTheHiddenDialogHasFinishedClosing_ItIsShownAgain ()
        {
            var service = new TestDialogService();
            var form = new TestDialog("form");
            var picker = new TestDialog("picker");
            Task<eDialogOutcome> formOutcome = service.ShowAsync(form);
            Task<eDialogOutcome> pickerOutcome = service.ShowAsync(picker, eDialogOverlapResponse.HideForNavBack);

            Assert.IsTrue(service.PopBack());
            AssertEnded(eDialogOutcome.PoppedBack, pickerOutcome, "Never shown, so it ends at once");
            Assert.AreEqual(0, picker.ShowCount);

            form.FinishClosing();
            Assert.IsTrue(form.IsShowing, "Back on top, so shown again");
            Assert.AreEqual(2, form.ShowCount);
            AssertWaiting(formOutcome, "The form's wait carries on");
        }

        // ===========[ Replace ]===============================================
        [TestMethod]
        public void ShowAsync_Replace_EndsTheShowingDialogAsReplacedAndKeepsWhatIsHiddenBeneath ()
        {
            var service = new TestDialogService();
            var form = new TestDialog("form");
            var datePicker = new TestDialog("date picker");
            var timePicker = new TestDialog("time picker");
            Task<eDialogOutcome> formOutcome = service.ShowAsync(form);
            Task<eDialogOutcome> datePickerOutcome = service.ShowAsync(datePicker, eDialogOverlapResponse.HideForNavBack);
            form.FinishClosing();

            Task<eDialogOutcome> timePickerOutcome = service.ShowAsync(timePicker, eDialogOverlapResponse.Replace);
            Assert.IsTrue(datePicker.IsHideRequested);
            datePicker.FinishClosing();
            AssertEnded(eDialogOutcome.Replaced, datePickerOutcome, "Its place was taken");
            Assert.IsTrue(timePicker.IsShowing);
            Assert.IsTrue(service.CanPopBack, "The form is still beneath");

            timePicker.Close(eDialogOutcome.Primary);
            AssertEnded(eDialogOutcome.Primary, timePickerOutcome, "The person's answer");
            Assert.IsTrue(form.IsShowing, "The form comes back");
            AssertWaiting(formOutcome, "The form's wait carries on");
        }

        // ===========[ Windows closing ]=======================================
        [TestMethod]
        public void OnWindowClosed_EndsOnlyThatWindowsDialogsAndTheNextOneShows ()
        {
            var service = new TestDialogService();
            var form = new TestDialog("form", window: "editor");
            var picker = new TestDialog("picker", window: "editor");
            var notice = new TestDialog("notice", window: "main");
            Task<eDialogOutcome> formOutcome = service.ShowAsync(form);
            Task<eDialogOutcome> pickerOutcome = service.ShowAsync(picker, eDialogOverlapResponse.HideForNavBack);
            form.FinishClosing();
            Task<eDialogOutcome> noticeOutcome = service.ShowAsync(notice);

            service.ReportWindowClosed("editor");
            AssertEnded(eDialogOutcome.WindowClosed, formOutcome, "Hidden in the closed window");
            AssertEnded(eDialogOutcome.WindowClosed, pickerOutcome, "Showing in the closed window");
            Assert.IsFalse(picker.IsHideRequested, "Its window is gone, so it isn't asked to hide");
            Assert.IsTrue(notice.IsShowing, "The other window's dialog is next");
            Assert.IsFalse(service.CanPopBack);

            picker.Close(eDialogOutcome.None);
            AssertEnded(eDialogOutcome.WindowClosed, pickerOutcome, "A late ending from the closed window changes nothing");
            Assert.IsTrue(notice.IsShowing);
            AssertWaiting(noticeOutcome, "Not answered yet");
        }

        // ===========[ Callers and threads ]===================================
        [TestMethod]
        public void ShowAsync_FromTheContinuationOfTheLastDialog_ShowsTheNextOne ()
        {
            var service = new TestDialogService();
            var first = new TestDialog("first");
            var second = new TestDialog("second");
            Task<eDialogOutcome> bothOutcome = _AskBothAsync();

            first.Close(eDialogOutcome.Primary);
            Assert.IsTrue(second.IsShowing, "Asked for straight from the first one's ending");

            second.Close(eDialogOutcome.Secondary);
            AssertEnded(eDialogOutcome.Secondary, bothOutcome, "The second's answer");

            async Task<eDialogOutcome> _AskBothAsync ()
            {
                await service.ShowAsync(first);
                return await service.ShowAsync(second);
            }
        }

        [TestMethod]
        public void ShowAsync_FromAnotherThread_IsSentToTheDialogThread ()
        {
            var service = new TestDialogService { IsCallerOnDialogThread = false };
            var form = new TestDialog("form");

            Task<eDialogOutcome> formOutcome = service.ShowAsync(form);
            Assert.IsFalse(form.IsShowing, "Not until the dialog thread runs it");

            service.RunDialogThreadWork();
            Assert.IsTrue(form.IsShowing);

            form.Close(eDialogOutcome.Primary);
            AssertEnded(eDialogOutcome.Primary, formOutcome, "The answer comes back to the other thread's caller");
        }

        [TestMethod]
        public void CanPopBack_RaisesPropertyChangedAsTheStackGrowsAndShrinks ()
        {
            var service = new TestDialogService();
            var form = new TestDialog("form");
            var picker = new TestDialog("picker");
            var changes = new List<bool>();
            service.PropertyChanged += _OnPropertyChanged;

            service.ShowAsync(form);
            service.ShowAsync(picker, eDialogOverlapResponse.HideForNavBack);
            form.FinishClosing();
            picker.Close(eDialogOutcome.Primary);

            service.PropertyChanged -= _OnPropertyChanged;
            CollectionAssert.AreEqual(new[] { true, false }, changes);

            void _OnPropertyChanged (object sender, PropertyChangedEventArgs e)
            {
                if (e.PropertyName == nameof(TestDialogService.CanPopBack))
                {
                    changes.Add(service.CanPopBack);
                }
            }
        }

        // ===========[ Helper Methods ]========================================
        private static void AssertWaiting (Task<eDialogOutcome> outcome, string because)
        {
            Assert.IsFalse(outcome.IsCompleted, $"Still waiting: {because}");
        }

        private static void AssertEnded (eDialogOutcome expected, Task<eDialogOutcome> outcome, string because)
        {
            Assert.IsTrue(outcome.IsCompletedSuccessfully, $"Ended: {because}");
            Assert.AreEqual(expected, outcome.Result, because);
        }

        // ===========[ Subclasses/structs ]====================================
        // A dialog that shows when asked and stays open until a test closes it: Close is the person's choice (or None for
        //  the close button), FinishClosing is a hide the service asked for landing, as a ContentDialog's does once its
        //  close has run
        private sealed class TestDialog
        {
            private TaskCompletionSource<eDialogOutcome> m_showing;

            public TestDialog (string name, string window = "main")
            {
                this.Name = name;
                this.Window = window;
            }

            public string Name { get; }
            public string Window { get; }
            public bool IsShowing => m_showing != null;
            public bool IsHideRequested { get; private set; }
            public int ShowCount { get; private set; }

            /// <summary>Cancels a hide, the way a ContentDialog that cancels its Closing does</summary>
            public bool RefusesToHide { get; set; }

            /// <summary>Has closed by the time the hide request returns</summary>
            public bool ClosesAtOnceWhenHidden { get; set; }

            /// <summary>Throws when shown, the way ShowAsync does with a dialog the service didn't show already open</summary>
            public bool FailsToShow { get; set; }

            /// <summary>Refused by the service before it is held, the way a ContentDialog with no XamlRoot is</summary>
            public bool IsRefusedBeforeHolding { get; set; }

            public Task<eDialogOutcome> BeginShowing ()
            {
                if (this.FailsToShow)
                {
                    throw new InvalidOperationException($"{this.Name} can't be shown");
                }

                Assert.IsNull(m_showing, $"{this.Name} was shown while already showing");
                ++this.ShowCount;
                this.IsHideRequested = false;
                m_showing = new TaskCompletionSource<eDialogOutcome>();
                return m_showing.Task;
            }

            public void OnHideRequested ()
            {
                Assert.IsTrue(this.IsShowing, $"{this.Name} was asked to hide while not showing");
                this.IsHideRequested = true;
                if (this.ClosesAtOnceWhenHidden && !this.RefusesToHide)
                {
                    this.Close(eDialogOutcome.None);
                }
            }

            public void Close (eDialogOutcome closedWith)
            {
                TaskCompletionSource<eDialogOutcome> showing = m_showing;
                Assert.IsNotNull(showing, $"{this.Name} was closed while not showing");
                m_showing = null;
                showing.SetResult(closedWith);
            }

            public void FinishClosing ()
            {
                Assert.IsTrue(this.IsHideRequested && !this.RefusesToHide, $"{this.Name} had no hide to finish");
                this.Close(eDialogOutcome.None);
            }

            public override string ToString () => this.Name;
        }

        // Shows and hides the stand-ins, and queues work sent to the dialog thread until the test runs it
        private sealed class TestDialogService : DialogServiceBase<TestDialog>
        {
            private readonly Queue<Action> m_dialogThreadWork = new Queue<Action>();

            public bool IsCallerOnDialogThread { get; set; } = true;

            public void ReportWindowClosed (string window)
            {
                this.OnWindowClosed(_WasInIt);

                bool _WasInIt (TestDialog dialog) => dialog.Window == window;
            }

            public void RunDialogThreadWork ()
            {
                bool wasOnDialogThread = this.IsCallerOnDialogThread;
                this.IsCallerOnDialogThread = true;
                while (m_dialogThreadWork.Count > 0)
                {
                    m_dialogThreadWork.Dequeue()();
                }

                this.IsCallerOnDialogThread = wasOnDialogThread;
            }

            protected override void OnHoldingDialog (TestDialog dialog)
            {
                if (dialog.IsRefusedBeforeHolding)
                {
                    throw new ArgumentException($"{dialog.Name} isn't ready to be shown", nameof(dialog));
                }
            }

            protected override Task<eDialogOutcome> ShowDialogAsync (TestDialog dialog) => dialog.BeginShowing();
            protected override void HideDialog (TestDialog dialog) => dialog.OnHideRequested();
            protected override bool IsOnDialogThread => this.IsCallerOnDialogThread;

            protected override bool TryRunOnDialogThread (Action action)
            {
                m_dialogThreadWork.Enqueue(action);
                return true;
            }
        }
    }
}
