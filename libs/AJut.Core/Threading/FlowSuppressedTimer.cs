namespace AJut.Threading
{
    using System;
    using System.Threading;

    /// <summary>
    /// Builds <see cref="Timer"/>s with execution context flow suppressed, so their callbacks don't run inside (or keep alive)
    /// the async locals and culture of whichever thread happened to build them.
    /// 
    /// <b>This is the right default for nearly every timer</b>.
    /// 
    /// The only case for a plain <see cref="Timer"/> is a one-shot timer built inside a single operation, whose callback
    /// is the rest of that operation and needs its ambient state - the comment inside <see cref="Create"/> goes through why.
    /// Elsewhere this is known as a non-capturing timer. Mimics the Microsoft NonCapturing timer built in a few
    /// places, example: https://source.dot.net/#Microsoft.Extensions.FileProviders.Physical/src/runtime/src/libraries/Common/src/Extensions/NonCapturingTimer/NonCapturingTimer.cs,f98caeaeb54188ff
    /// </summary>
    public static class FlowSuppressedTimer
    {
        /// <summary>
        /// Builds a <see cref="Timer"/> exactly as its constructor would, except that the calling thread's
        /// <see cref="ExecutionContext"/> is not captured - every callback runs in a clean, default context.
        /// </summary>
        /// <param name="callback">What the timer calls, on a thread pool thread</param>
        /// <param name="state">Passed to <paramref name="callback"/> on every call, or null</param>
        /// <param name="dueTime">How long until the first call, <see cref="TimeSpan.Zero"/> for right away, or
        /// <see cref="Timeout.InfiniteTimeSpan"/> to start it later with <see cref="Timer.Change(TimeSpan, TimeSpan)"/></param>
        /// <param name="period">How long between calls after the first, or <see cref="Timeout.InfiniteTimeSpan"/> for one call</param>
        public static Timer Create (TimerCallback callback, object state, TimeSpan dueTime, TimeSpan period)
        {
            // ------ Why build timers with execution context flow suppressed
            //
            // What "flow" is: an ExecutionContext is the ambient state that belongs to whatever code is running right now. In
            //  .NET (Core and later) that means every AsyncLocal<T> value, plus CultureInfo.CurrentCulture and CurrentUICulture
            //  once either has been set through its property, since those are stored in async locals too. Whenever work is
            //  handed off to run later somewhere else (Task.Run, ThreadPool.QueueUserWorkItem, an await, a Timer), .NET
            //  captures the current context and puts it back in place around that work. That's "flowing" it, and it's why an
            //  async local set before an await is still there after it, even when the rest runs on another thread.
            //
            // What a Timer does with it: System.Threading.Timer captures the context once, in its constructor, and runs every
            //  single callback inside that one capture for as long as the timer exists. No public constructor skips the
            //  capture. Because it only captures once, a timer that gets reused or changed never carries the context of
            //  whoever is using it now - only whatever happened to be ambient when it was built.
            //
            // Why that's wrong for almost every timer: a timer's callbacks are the work of whatever owns the timer - a poll, a
            //  periodic report, a cache sweep, a debounce - and whatever built it is often arbitrary (the first caller to need
            //  it, say). Left alone, every callback for the rest of the timer's life would run inside that one caller's ambient
            //  state - its culture, and whatever async locals it had set (a logging scope, a correlation id, some ambient
            //  "current" object, etc) - so the callback could format or tag its work as though it were part of that caller's.
            //  Worse, the capture holds references to all of those values, so they (and everything they reference) stay alive
            //  until the timer is disposed. That's a retention path that's difficult to find when hunting a leak.
            //
            // The one case a plain Timer is right: a one-shot timer built inside a single operation, whose callback IS the
            //  rest of that operation - wait a bit, then carry on with the same request. That callback should see the
            //  operation's ambient state: Activity.Current (so the trace stays one trace), its logging scope and correlation id
            //  (so the lines get tagged as part of that request), impersonation (WindowsIdentity.RunImpersonated flows through
            //  an async local), its culture. Suppressing flow there quietly drops the trace, the scope or the identity, which is
            //  its own difficult to find bug. These days that shape is nearly always written as await Task.Delay, which flows
            //  the context by itself, so building a Timer for it is rare.
            //
            // So why does .NET flow by default: a Timer is a general "run this later" primitive and can't tell which of those
            //  two it's being used for, and silently dropping context can drop security relevant state like impersonation - so
            //  the runtime keeps it unless told otherwise. Its own libraries opt out for their background timers, which is what
            //  the NonCapturingTimer linked in the summary is.
            //
            // Which makes this the default to reach for when building a timer, and a plain Timer the exception for the one-shot
            //  case above. It isn't a performance choice either way: capturing is one reference, and putting it back in place
            //  around a callback is cheap.
            //
            // The same choice shows up all over .NET under the name "Unsafe" - ThreadPool.UnsafeQueueUserWorkItem,
            //  CancellationToken.UnsafeRegister, Thread.UnsafeStart all skip flow. The name comes from .NET Framework, where
            //  skipping flow also skipped the caller's security context; it has nothing to do with memory safety.
            //
            // How: ExecutionContext.SuppressFlow() stops capturing on this thread until the AsyncFlowControl it returns is
            //  disposed, which the using below does right after the timer is constructed. Built inside that window, the timer
            //  captures nothing, and its callbacks run with every async local at its default and the default culture
            //  (CultureInfo.DefaultThreadCurrentCulture if the app set one, the system's otherwise). The suppression only
            //  affects this thread, and only until the using ends.
            //
            // The IsFlowSuppressed check: SuppressFlow throws InvalidOperationException if flow is already suppressed on this
            //  thread (something further up the stack did it). In that case there's nothing to capture anyway, so the timer can
            //  just be built.
            //
            // Further reading: the Microsoft.Extensions NonCapturingTimer linked in the summary does exactly this for its own
            //  background timers, and the .NET docs on ExecutionContext.SuppressFlow and AsyncLocal<T> cover the mechanics.
            if (ExecutionContext.IsFlowSuppressed())
            {
                return new Timer(callback, state, dueTime, period);
            }

            using (ExecutionContext.SuppressFlow())
            {
                return new Timer(callback, state, dueTime, period);
            }
        }
    }
}
