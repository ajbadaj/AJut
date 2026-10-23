namespace AJut.UX.WinUI3.Tests
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using AJut.UX.Theming;
    using Microsoft.UI.Dispatching;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// The WinUI3 watcher moving touch preference notices onto its dispatcher queue's thread. The test project can't make
    /// windows, so these use a dispatcher queue on a thread of its own and a stand-in source that can change; Windows' own
    /// source is covered in AJut.UX.Wpf.Tests, against a real window.
    /// </summary>
    [TestClass]
    public class WinUI3TouchPreferenceWatcherTests
    {
        private static readonly TimeSpan kWait = TimeSpan.FromSeconds(10);

        [TestMethod]
        public void TouchPreferenceWatcher_NoticeOffTheQueueThread_ChangesOnTheQueueThread ()
        {
            DispatcherQueueController controller = DispatcherQueueController.CreateOnDedicatedThread();
            try
            {
                var source = new StandInSource();
                TouchPreferenceWatcher watcher = CreateOnQueue(controller.DispatcherQueue, source, out int queueThreadId);
                var changes = new ChangeRecorder();
                watcher.IsTouchPreferredChanged += changes.OnChanged;

                source.IsTouchPreferred = true;
                Task.Run(source.RaiseNotice).Wait();

                Assert.IsTrue(changes.Raised.Wait(kWait), "The change is raised");
                watcher.IsTouchPreferredChanged -= changes.OnChanged;
                Assert.AreEqual(1, changes.Count);
                Assert.IsTrue(changes.LastValue);
                Assert.AreEqual(queueThreadId, changes.LastThreadId, "Raised on the dispatcher queue's thread");
                Assert.IsTrue(watcher.IsTouchPreferred);
                watcher.Dispose();
                Assert.AreEqual(0, source.SubscriberCount, "Disposed, the watcher lets go of the source");
            }
            finally
            {
                ShutDown(controller);
            }
        }

        [TestMethod]
        public void TouchPreferenceWatcher_NoticeWithNoChange_RaisesNothing ()
        {
            DispatcherQueueController controller = DispatcherQueueController.CreateOnDedicatedThread();
            try
            {
                var source = new StandInSource { IsTouchPreferred = true };
                TouchPreferenceWatcher watcher = CreateOnQueue(controller.DispatcherQueue, source, out _);
                var changes = new ChangeRecorder();
                watcher.IsTouchPreferredChanged += changes.OnChanged;

                Task.Run(source.RaiseNotice).Wait();
                DrainQueue(controller.DispatcherQueue);
                watcher.IsTouchPreferredChanged -= changes.OnChanged;

                Assert.IsTrue(watcher.IsTouchPreferred, "It starts from what the source says");
                Assert.AreEqual(0, changes.Count, "Windows says it may have changed; it hadn't");
                watcher.Dispose();
            }
            finally
            {
                ShutDown(controller);
            }
        }

        // ===========[ Helper Methods ]===================================
        // A queue on a dedicated thread is shut down asynchronously, from any thread
        private static void ShutDown (DispatcherQueueController controller)
        {
            Assert.IsTrue(controller.ShutdownQueueAsync().AsTask().Wait(kWait), "The queue shuts down");
        }

        private static TouchPreferenceWatcher CreateOnQueue (DispatcherQueue queue, StandInSource source, out int queueThreadId)
        {
            var maker = new WatcherMaker(queue, source);
            Assert.IsTrue(queue.TryEnqueue(maker.Make), "The queue takes work");
            Assert.IsTrue(maker.Made.Wait(kWait), "The watcher is made on the queue's thread");
            queueThreadId = maker.ThreadId;
            return maker.Watcher;
        }

        // Waits until everything already queued has run
        private static void DrainQueue (DispatcherQueue queue)
        {
            using var drained = new ManualResetEventSlim();
            Assert.IsTrue(queue.TryEnqueue(DispatcherQueuePriority.Low, drained.Set));
            Assert.IsTrue(drained.Wait(kWait), "The queue drains");
        }

        // ===========[ Subclasses/structs ]===================================
        private sealed class WatcherMaker
        {
            private readonly DispatcherQueue m_queue;
            private readonly StandInSource m_source;

            public WatcherMaker (DispatcherQueue queue, StandInSource source)
            {
                m_queue = queue;
                m_source = source;
            }

            public ManualResetEventSlim Made { get; } = new ManualResetEventSlim();

            public TouchPreferenceWatcher Watcher { get; private set; }

            public int ThreadId { get; private set; }

            public void Make ()
            {
                this.Watcher = new TouchPreferenceWatcher(m_source, m_queue);
                this.ThreadId = Environment.CurrentManagedThreadId;
                this.Made.Set();
            }
        }

        private sealed class StandInSource : ITouchPreferenceSource
        {
            private EventHandler m_preferenceMayHaveChanged;

            public event EventHandler PreferenceMayHaveChanged
            {
                add
                {
                    m_preferenceMayHaveChanged += value;
                    ++this.SubscriberCount;
                }
                remove
                {
                    m_preferenceMayHaveChanged -= value;
                    --this.SubscriberCount;
                }
            }

            public bool IsTouchPreferred { get; set; }

            public int SubscriberCount { get; private set; }

            public void RaiseNotice () => m_preferenceMayHaveChanged?.Invoke(this, EventArgs.Empty);
        }

        private sealed class ChangeRecorder
        {
            public ManualResetEventSlim Raised { get; } = new ManualResetEventSlim();

            public int Count { get; private set; }

            public bool LastValue { get; private set; }

            public int LastThreadId { get; private set; }

            public void OnChanged (object sender, EventArgs<bool> e)
            {
                ++this.Count;
                this.LastValue = e.Value;
                this.LastThreadId = Environment.CurrentManagedThreadId;
                this.Raised.Set();
            }
        }
    }
}
