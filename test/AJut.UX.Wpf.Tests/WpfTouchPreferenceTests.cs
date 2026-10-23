namespace AJut.UX.Tests
{
    using System;
    using System.Collections.Concurrent;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Runtime.ExceptionServices;
    using System.Runtime.InteropServices;
    using System.Threading;
    using System.Windows;
    using System.Windows.Interop;
    using System.Windows.Threading;
    using AJut.UX.Theming;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Windows' touch preference, and the WPF watcher that follows it. Seeing a real switch between touch and mouse needs a
    /// Windows 11 24H2 2-in-1, so these check everything short of that: the query, the subscription and the cleanup against
    /// a real window (never shown), a change notice delivered through the handler's COM interface from another thread,
    /// and the watcher moving notices to the UI thread, with a stand-in source that can change.
    /// </summary>
    [TestClass]
    public class WpfTouchPreferenceTests
    {
        private static bool IsWindowsWithTouchPreference => OperatingSystem.IsWindowsVersionAtLeast(10, 0, 26100);

        // ===========[ Windows' source ]===================================
        [TestMethod]
        public void WindowsTouchPreferenceSource_ForAWindow_AsksWindowsAndSubscribes ()
        {
            AppLikeUIThread.Run(() =>
            {
                var window = new Window();
                try
                {
                    using WindowsTouchPreferenceSource source = WindowsTouchPreferenceSource.TryCreateForWindow(HandleOf(window));
                    if (!IsWindowsWithTouchPreference)
                    {
                        Assert.IsNull(source, "Windows offers desktop apps no touch preference before Windows 11 24H2");
                        return;
                    }

                    Assert.IsNotNull(source, "Windows 11 24H2 and later give a window its preferred interaction mode");
                    Assert.IsTrue(source.IsSubscribed, "Windows took the change handler");

                    // What Windows answers depends on the machine; asking has to work
                    _ = source.IsTouchPreferred;
                }
                finally
                {
                    window.Close();
                }
            });
        }

        [TestMethod]
        public void WindowsTouchPreferenceSource_Dispose_UnsubscribesOnce ()
        {
            AssumeWindowsWithTouchPreference();
            AppLikeUIThread.Run(() =>
            {
                var window = new Window();
                try
                {
                    WindowsTouchPreferenceSource source = WindowsTouchPreferenceSource.TryCreateForWindow(HandleOf(window));
                    source.Dispose();

                    Assert.IsFalse(source.IsTouchPreferred, "Disposed, there is nothing left to ask");
                    Assert.IsTrue(PumpUntil(() => !source.IsSubscribed), "Windows gets its change handler back");
                    source.Dispose();
                }
                finally
                {
                    window.Close();
                }
            });
        }

        [TestMethod]
        public void WindowsTouchPreferenceSource_Subscribed_HearsWindowsFirstNotice ()
        {
            AssumeWindowsWithTouchPreference();
            AppLikeUIThread.Run(() =>
            {
                var window = new Window();
                try
                {
                    using WindowsTouchPreferenceSource source = WindowsTouchPreferenceSource.TryCreateForWindow(HandleOf(window));
                    var notices = new NoticeRecorder();
                    source.PreferenceMayHaveChanged += notices.OnNotice;

                    bool heard = PumpUntil(() => notices.Count > 0);
                    source.PreferenceMayHaveChanged -= notices.OnNotice;

                    Assert.IsTrue(heard, "Windows sends a notice soon after a handler is added");
                }
                finally
                {
                    window.Close();
                }
            });
        }

        [TestMethod]
        public void WindowsTouchPreferenceSource_DisposedBeforeWindowsFirstNotice_KeepsTheHandlerUntilItComes ()
        {
            AssumeWindowsWithTouchPreference();
            AppLikeUIThread.Run(() =>
            {
                var window = new Window();
                try
                {
                    WindowsTouchPreferenceSource source = WindowsTouchPreferenceSource.TryCreateForWindow(HandleOf(window));
                    source.Dispose();

                    // Windows' first notice is delivered when this thread next pumps messages, and taking the handler back
                    // before then can deadlock inside Windows, so the source waits for it
                    Assert.IsTrue(source.IsSubscribed, "The handler stays with Windows until its first notice has been delivered");
                    Assert.IsTrue(PumpUntil(() => !source.IsSubscribed), "Once the notice has come, Windows gets its change handler back");
                }
                finally
                {
                    window.Close();
                }
            });
        }

        [TestMethod]
        public void WindowsTouchPreferenceSource_HandlerCalledFromAnotherThread_RaisesOnThatThread ()
        {
            AssumeWindowsWithTouchPreference();
            AppLikeUIThread.Run(() =>
            {
                var window = new Window();
                try
                {
                    using WindowsTouchPreferenceSource source = WindowsTouchPreferenceSource.TryCreateForWindow(HandleOf(window));
                    var notices = new NoticeRecorder();
                    source.PreferenceMayHaveChanged += notices.OnNotice;

                    int result = CallAHandlerTheWayWindowsDoes(source, out int callingThreadId);
                    source.PreferenceMayHaveChanged -= notices.OnNotice;

                    // Windows sends real notices too (one comes soon after subscribing), so only the calling thread's count
                    Assert.AreEqual(0, result, "The handler answers S_OK");
                    Assert.AreEqual(1, notices.CountOn(callingThreadId), "One call, one notice, raised on the thread that called; moving to the UI thread is the watcher's job");
                }
                finally
                {
                    window.Close();
                }
            });
        }

        [TestMethod]
        public void WindowsTouchPreferenceSource_HandlerCalledAfterDispose_RaisesNothing ()
        {
            AssumeWindowsWithTouchPreference();
            AppLikeUIThread.Run(() =>
            {
                var window = new Window();
                try
                {
                    WindowsTouchPreferenceSource source = WindowsTouchPreferenceSource.TryCreateForWindow(HandleOf(window));
                    var notices = new NoticeRecorder();
                    source.PreferenceMayHaveChanged += notices.OnNotice;
                    source.Dispose();

                    int result = CallAHandlerTheWayWindowsDoes(source, out int callingThreadId);

                    Assert.AreEqual(0, result, "A late call from Windows is still answered S_OK");
                    Assert.AreEqual(0, notices.CountOn(callingThreadId), "A disposed source says nothing");
                }
                finally
                {
                    window.Close();
                }
            });
        }

        [TestMethod]
        public void WindowsTouchPreferenceSource_DisposedThenCollected_ReleasesCleanly ()
        {
            AssumeWindowsWithTouchPreference();
            AppLikeUIThread.Run(() =>
            {
                // A disposed source leaves nothing for the collector, and one never disposed cleans up from its finalizer,
                // so this makes sure a forced collection after both is clean
                for (int round = 0; round < 5; ++round)
                {
                    UseAndDisposeASource();
                }

                LeaveASourceUndisposed();
                for (int pass = 0; pass < 3; ++pass)
                {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                }
            });
        }

        // ===========[ The watcher ]===================================
        [TestMethod]
        public void TouchPreferenceWatcher_NoticeOffTheUIThread_ChangesOnTheUIThread ()
        {
            AppLikeUIThread.Run(() =>
            {
                var source = new StandInSource();
                using var watcher = new TouchPreferenceWatcher(source, Dispatcher.CurrentDispatcher);
                var changes = new ChangeRecorder();
                watcher.IsTouchPreferredChanged += changes.OnChanged;

                source.IsTouchPreferred = true;
                RunOnAnotherThread(source.RaiseNotice);
                Assert.AreEqual(0, changes.Count, "Nothing changes until the UI thread gets to it");
                PumpDispatcher();
                watcher.IsTouchPreferredChanged -= changes.OnChanged;

                Assert.IsTrue(watcher.IsTouchPreferred);
                Assert.AreEqual(1, changes.Count);
                Assert.IsTrue(changes.LastValue);
                Assert.AreEqual(Environment.CurrentManagedThreadId, changes.LastThreadId, "Raised on the UI thread");
            });
        }

        [TestMethod]
        public void TouchPreferenceWatcher_NoticeWithNoChange_RaisesNothing ()
        {
            AppLikeUIThread.Run(() =>
            {
                var source = new StandInSource { IsTouchPreferred = true };
                using var watcher = new TouchPreferenceWatcher(source, Dispatcher.CurrentDispatcher);
                var changes = new ChangeRecorder();
                watcher.IsTouchPreferredChanged += changes.OnChanged;

                RunOnAnotherThread(source.RaiseNotice);
                PumpDispatcher();
                watcher.IsTouchPreferredChanged -= changes.OnChanged;

                Assert.IsTrue(watcher.IsTouchPreferred, "It starts from what the source says");
                Assert.AreEqual(0, changes.Count, "Windows says it may have changed; it hadn't");
            });
        }

        [TestMethod]
        public void TouchPreferenceWatcher_Dispose_StopsListeningAndDropsANoticeOnItsWay ()
        {
            AppLikeUIThread.Run(() =>
            {
                var source = new StandInSource();
                var watcher = new TouchPreferenceWatcher(source, Dispatcher.CurrentDispatcher);

                source.IsTouchPreferred = true;
                RunOnAnotherThread(source.RaiseNotice);
                watcher.Dispose();
                PumpDispatcher();

                Assert.AreEqual(0, source.SubscriberCount, "The watcher let go of the source");
                Assert.IsFalse(watcher.IsAvailable);
                Assert.IsFalse(watcher.IsTouchPreferred, "The notice that arrived after disposal changed nothing");
                watcher.Dispose();
            });
        }

        [TestMethod]
        public void TouchPreferenceWatcher_ForAWindow_FollowsWindowsWhereWindowsOffersIt ()
        {
            AppLikeUIThread.Run(() =>
            {
                var window = new Window();
                try
                {
                    var watcher = new TouchPreferenceWatcher(window);
                    Assert.AreEqual(IsWindowsWithTouchPreference, watcher.IsAvailable, "It follows Windows from Windows 11 24H2 on");

                    watcher.Dispose();
                    Assert.IsFalse(watcher.IsAvailable);
                }
                finally
                {
                    window.Close();
                }
            });
        }

        // ===========[ Helper Methods ]===================================
        private static void AssumeWindowsWithTouchPreference ()
        {
            if (!IsWindowsWithTouchPreference)
            {
                Assert.Inconclusive("Windows offers desktop apps a touch preference from Windows 11 24H2 on");
            }
        }

        private static nint HandleOf (Window window) => new WindowInteropHelper(window).EnsureHandle();

        // Not inlined, so nothing it makes is still referenced from the caller's frame when the caller collects
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void UseAndDisposeASource ()
        {
            var window = new Window();
            WindowsTouchPreferenceSource source = WindowsTouchPreferenceSource.TryCreateForWindow(HandleOf(window));
            _ = source.IsTouchPreferred;
            source.Dispose();
            window.Close();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void LeaveASourceUndisposed ()
        {
            var window = new Window();
            _ = WindowsTouchPreferenceSource.TryCreateForWindow(HandleOf(window));
            window.Close();
        }

        // A handler of the same kind the source gave Windows, called through its COM interface the way Windows calls it,
        // from another thread
        private static int CallAHandlerTheWayWindowsDoes (WindowsTouchPreferenceSource source, out int callingThreadId)
        {
            var handler = new WindowsTouchPreferenceSource.ChangedHandler(source);
            nint handlerPointer = WindowsUIViewSettingsNative.CreateHandlerPointer(handler);
            Assert.AreNotEqual(IntPtr.Zero, handlerPointer, "The handler has a COM interface");
            try
            {
                int result = -1;
                callingThreadId = RunOnAnotherThread(() => result = InvokeThroughVtable(handlerPointer));
                return result;
            }
            finally
            {
                Marshal.Release(handlerPointer);
            }
        }

        // A thread of its own, never the caller's: waiting on a task that hasn't started yet can run it inline on the
        // waiting thread, which here is the UI thread
        private static int RunOnAnotherThread (Action work)
        {
            var thread = new Thread(new ThreadStart(work)) { IsBackground = true, Name = "Touch preference notice" };
            thread.Start();
            Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(10)), "The other thread finishes");
            return thread.ManagedThreadId;
        }

        // TypedEventHandler's Invoke is the first method after IUnknown's three
        private static unsafe int InvokeThroughVtable (nint handlerPointer)
        {
            void** vtable = *(void***)handlerPointer;
            var invoke = (delegate* unmanaged[Stdcall]<nint, nint, nint, int>)vtable[3];
            return invoke(handlerPointer, IntPtr.Zero, IntPtr.Zero);
        }

        // Pumps this thread's messages until the condition holds, or a few seconds pass; says whether it held
        private static bool PumpUntil (Func<bool> condition)
        {
            const int kMaxPumps = 300;
            for (int pump = 0; (pump < kMaxPumps) && !condition(); ++pump)
            {
                PumpDispatcher();
                Thread.Sleep(10);
            }

            return condition();
        }

        // Runs everything queued on this thread's dispatcher
        private static void PumpDispatcher ()
        {
            var frame = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, new DispatcherOperationCallback(EndFrame), frame);
            Dispatcher.PushFrame(frame);
        }

        private static object EndFrame (object frame)
        {
            ((DispatcherFrame)frame).Continue = false;
            return null;
        }

        // ===========[ Subclasses/structs ]===================================
        // A thread of their own for these tests, running a WPF dispatcher loop the way an app's UI thread does. Windows
        // delivers its notices as window messages, and the shared StaTestThread only pumps messages while it waits for
        // the next test, so they arrive there at moments no app's UI thread would see them.
        private static class AppLikeUIThread
        {
            private static readonly Dispatcher g_dispatcher = StartDispatcher();

            public static void Run (Action body)
            {
                Exception failure = null;
                g_dispatcher.Invoke(() =>
                {
                    try
                    {
                        body();
                    }
                    catch (Exception e)
                    {
                        failure = e;
                    }
                });

                if (failure != null)
                {
                    ExceptionDispatchInfo.Capture(failure).Throw();
                }
            }

            private static Dispatcher StartDispatcher ()
            {
                Dispatcher dispatcher = null;
                using var started = new ManualResetEventSlim();
                var thread = new Thread(() =>
                {
                    dispatcher = Dispatcher.CurrentDispatcher;
                    started.Set();
                    Dispatcher.Run();
                })
                {
                    IsBackground = true,
                    Name = "WPF touch test UI thread",
                };

                thread.SetApartmentState(ApartmentState.STA);
                thread.Start();
                started.Wait();
                return dispatcher;
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

        // Notices come from Windows as well as from the test, so each is kept with its thread
        private sealed class NoticeRecorder
        {
            private readonly ConcurrentQueue<int> m_noticeThreadIds = new ConcurrentQueue<int>();

            public int Count => m_noticeThreadIds.Count;

            public int CountOn (int threadId) => m_noticeThreadIds.Count(id => id == threadId);

            public void OnNotice (object sender, EventArgs e)
            {
                m_noticeThreadIds.Enqueue(Environment.CurrentManagedThreadId);
            }
        }

        private sealed class ChangeRecorder
        {
            public int Count { get; private set; }

            public bool LastValue { get; private set; }

            public int LastThreadId { get; private set; }

            public void OnChanged (object sender, EventArgs<bool> e)
            {
                ++this.Count;
                this.LastValue = e.Value;
                this.LastThreadId = Environment.CurrentManagedThreadId;
            }
        }
    }
}
