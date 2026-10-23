namespace AJut.UX.Tests
{
    using System;
    using System.Collections.Concurrent;
    using System.Runtime.ExceptionServices;
    using System.Threading;

    /// <summary>
    /// Runs a test's WPF work on the one STA thread every WPF test shares, since WPF objects need one. Nothing it makes is shown.
    /// </summary>
    /// <remarks>
    /// One thread for all of them, the way an app has one UI thread, rather than a new thread per test. WPF loads a
    /// control's default style once per process, on whichever thread first asks for it, and a Freezable in that style that
    /// can't be frozen (a template trigger's gradient brush with bound stops, say) belongs to that thread. A later test on a
    /// thread of its own that applies the same template then throws "Cannot access Freezable ... across threads".
    /// </remarks>
    internal static class StaTestThread
    {
        private static readonly BlockingCollection<Action> g_work = new BlockingCollection<Action>();
        private static readonly Thread g_thread = StartThread();

        /// <summary>Runs <paramref name="body"/> on the shared STA thread and rethrows anything it throws (a failed assert included) on the caller's thread</summary>
        public static void Run (Action body)
        {
            Exception failure = null;
            using var done = new ManualResetEventSlim();
            g_work.Add(() =>
            {
                try
                {
                    body();
                }
                catch (Exception e)
                {
                    failure = e;
                }
                finally
                {
                    done.Set();
                }
            });

            done.Wait();
            if (failure != null)
            {
                ExceptionDispatchInfo.Capture(failure).Throw();
            }
        }

        private static Thread StartThread ()
        {
            var thread = new Thread(RunWork) { IsBackground = true, Name = "WPF test thread" };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            return thread;
        }

        private static void RunWork ()
        {
            foreach (Action work in g_work.GetConsumingEnumerable())
            {
                work();
            }
        }
    }
}
