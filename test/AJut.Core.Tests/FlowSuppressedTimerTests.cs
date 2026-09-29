namespace AJut.Core.UnitTests
{
    using System;
    using System.Threading;
    using AJut.Threading;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class FlowSuppressedTimerTests
    {
        private static readonly TimeSpan kWaitTimeout = TimeSpan.FromSeconds(5);
        private static readonly AsyncLocal<string> g_ambient = new AsyncLocal<string>();

        [TestMethod]
        public void Create_CallbackDoesNotSeeTheCallersAsyncLocals ()
        {
            g_ambient.Value = "CALLER_VALUE";
            try
            {
                // The control - a plain timer carries the caller's value into its callback, so the check below means something
                Assert.AreEqual("CALLER_VALUE", ReadAmbientFromCallbackOf(_BuildPlain));
                Assert.IsNull(ReadAmbientFromCallbackOf(FlowSuppressedTimer.Create));
            }
            finally
            {
                g_ambient.Value = null;
            }

            static Timer _BuildPlain (TimerCallback callback, object state, TimeSpan dueTime, TimeSpan period)
                => new Timer(callback, state, dueTime, period);
        }

        [TestMethod]
        public void Create_LeavesFlowAsItFoundIt ()
        {
            using (Timer timer = FlowSuppressedTimer.Create(_Ignore, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan))
            {
                Assert.IsFalse(ExecutionContext.IsFlowSuppressed());
            }

            using (ExecutionContext.SuppressFlow())
            {
                using (Timer timer = FlowSuppressedTimer.Create(_Ignore, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan))
                {
                    Assert.IsTrue(ExecutionContext.IsFlowSuppressed(), "A suppression the caller made is the caller's to undo");
                }
            }

            static void _Ignore (object state) { }
        }

        [TestMethod]
        public void Create_WhenFlowIsAlreadySuppressed_StillBuildsAWorkingTimer ()
        {
            using var fired = new ManualResetEventSlim();
            using (ExecutionContext.SuppressFlow())
            {
                using (Timer timer = FlowSuppressedTimer.Create(_Fire, fired, TimeSpan.Zero, Timeout.InfiniteTimeSpan))
                {
                    Assert.IsTrue(fired.Wait(kWaitTimeout));
                }
            }

            static void _Fire (object state) => ((ManualResetEventSlim)state).Set();
        }

        private static string ReadAmbientFromCallbackOf (Func<TimerCallback, object, TimeSpan, TimeSpan, Timer> buildTimer)
        {
            string seen = "NOT_CALLED";
            using var fired = new ManualResetEventSlim();
            using (Timer timer = buildTimer(_Read, null, TimeSpan.Zero, Timeout.InfiniteTimeSpan))
            {
                Assert.IsTrue(fired.Wait(kWaitTimeout), "The timer never fired");
            }

            return seen;

            void _Read (object state)
            {
                seen = g_ambient.Value;
                fired.Set();
            }
        }
    }
}
