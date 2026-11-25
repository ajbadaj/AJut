namespace AJut.UX.Tests
{
    using System;
    using System.Threading.Tasks;
    using AJut.Storage;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// How a StackNav page's popovers take turns: one shows at a time, a popover asked for while another is showing waits
    /// for it to be answered, and every caller gets its own popover's answer.
    /// </summary>
    [TestClass]
    public class WpfStackNavPopoverTests
    {
        [TestMethod]
        public void WpfStackNavPopover_ShownWhileAnotherIsShowing_WaitsForItToBeAnswered ()
        {
            StackNavAdapter adapter = MakeAdapter();
            var first = new TestPopover();
            var second = new TestPopover();

            Task<Result> firstAnswer = adapter.ShowPopover(first);
            Task<Result> secondAnswer = adapter.ShowPopover(second);

            Assert.AreSame(first, adapter.PopoverDisplay, "The popover already showing should stay up");
            Assert.IsFalse(secondAnswer.IsCompleted);

            Result firstResult = Result.Success();
            first.Answer(firstResult);

            Assert.IsTrue(firstAnswer.IsCompleted, "The first caller should get its answer");
            Assert.AreSame(firstResult, firstAnswer.Result);
            Assert.AreSame(second, adapter.PopoverDisplay, "The waiting popover should show once the first is answered");
            Assert.IsFalse(secondAnswer.IsCompleted);

            Result secondResult = Result.Success();
            second.Answer(secondResult);

            Assert.AreSame(secondResult, secondAnswer.Result);
            Assert.IsNull(adapter.PopoverDisplay);
            Assert.IsFalse(adapter.IsShowingPopover);
        }

        [TestMethod]
        public void WpfStackNavPopover_FirstCancelledFromCode_LeavesTheSecondShowing ()
        {
            StackNavAdapter adapter = MakeAdapter();
            var first = new TestPopover();
            var second = new TestPopover();

            Task<Result> firstAnswer = adapter.ShowPopover(first);
            Task<Result> secondAnswer = adapter.ShowPopover(second);
            first.Cancel("No longer needed");

            Assert.IsTrue(firstAnswer.IsCompleted);
            Assert.IsTrue(firstAnswer.Result.HasErrors, "A cancelled popover answers with an error");
            Assert.AreSame(second, adapter.PopoverDisplay, "Cancelling the first should not clear the second off the screen");
            Assert.IsTrue(adapter.IsShowingPopover);
            Assert.IsFalse(secondAnswer.IsCompleted);
        }

        [TestMethod]
        public void WpfStackNavPopover_TypedPopoverShownWhileAnotherIsShowing_WaitsForItToBeAnswered ()
        {
            StackNavAdapter adapter = MakeAdapter();
            var first = new TestPopover();
            var second = new TestPopover<string>();

            Task<Result> firstAnswer = adapter.ShowPopover(first);
            Task<Result<string>> secondAnswer = adapter.ShowPopover(second);

            Assert.AreSame(first, adapter.PopoverDisplay, "The popover already showing should stay up");

            first.Answer(Result.Success());

            Assert.IsTrue(firstAnswer.IsCompleted);
            Assert.AreSame(second, adapter.PopoverDisplay);

            second.Answer(Result<string>.Success("Picked"));

            Assert.AreEqual("Picked", secondAnswer.Result.Value);
            Assert.IsNull(adapter.PopoverDisplay);
        }

        [TestMethod]
        public void WpfStackNavPopover_WaitingPopoverCancelled_EndsWithoutShowing ()
        {
            StackNavAdapter adapter = MakeAdapter();
            var first = new TestPopover();
            var second = new TestPopover();
            var third = new TestPopover();

            Task<Result> firstAnswer = adapter.ShowPopover(first);
            Task<Result> secondAnswer = adapter.ShowPopover(second);
            Task<Result> thirdAnswer = adapter.ShowPopover(third);
            second.Cancel("No longer needed");

            Assert.IsTrue(secondAnswer.IsCompleted, "A waiting popover cancelled from code should answer at once");
            Assert.IsTrue(secondAnswer.Result.HasErrors);
            Assert.AreSame(first, adapter.PopoverDisplay, "Cancelling a waiting popover should not disturb the one showing");

            first.Answer(Result.Success());

            Assert.AreSame(third, adapter.PopoverDisplay, "The cancelled popover should be skipped, never shown");
            Assert.IsFalse(thirdAnswer.IsCompleted);
        }

        [TestMethod]
        public void WpfStackNavPopover_ShownFromTheFirstCallersContinuation_ShowsAfterTheOneWaiting ()
        {
            StackNavAdapter adapter = MakeAdapter();
            var first = new TestPopover();
            var second = new TestPopover();
            var followUp = new TestPopover();
            Task<Result> followUpAnswer = null;

            Task<Result> firstAnswer = adapter.ShowPopover(first);
            Task<Result> secondAnswer = adapter.ShowPopover(second);
            firstAnswer.ContinueWith(_ => followUpAnswer = adapter.ShowPopover(followUp), TaskContinuationOptions.ExecuteSynchronously);

            first.Answer(Result.Success());

            Assert.IsNotNull(followUpAnswer);
            Assert.AreSame(second, adapter.PopoverDisplay, "The popover that was already waiting goes first");

            second.Answer(Result.Success());

            Assert.AreSame(followUp, adapter.PopoverDisplay);
            Assert.IsFalse(followUpAnswer.IsCompleted);
        }

        // ===========[ Helper Methods ]========================================
        private static StackNavAdapter MakeAdapter () => new StackNavAdapter(null, new TestDisplay());

        // ===========[ Subclasses/structs ]====================================
        private sealed class TestDisplay : IStackNavDisplayControl
        {
            public void Setup (StackNavAdapter adapter)
            {
            }
        }

        private sealed class TestPopover : IStackNavPopoverDisplay
        {
            public event EventHandler<EventArgs<Result>> ResultSet;

            public void Answer (Result result) => this.ResultSet?.Invoke(this, new EventArgs<Result>(result));

            public void Cancel (string cancelReason = null) => this.Answer(Result.Error(cancelReason));
        }

        private sealed class TestPopover<T> : IStackNavPopoverDisplay<T>
        {
            public event EventHandler<EventArgs<Result<T>>> ResultSet;

            public void Answer (Result<T> result) => this.ResultSet?.Invoke(this, new EventArgs<Result<T>>(result));

            public void Cancel (string cancelReason = null) => this.Answer(Result<T>.Error(cancelReason));
        }
    }
}
