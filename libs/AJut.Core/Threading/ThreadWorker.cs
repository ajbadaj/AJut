namespace AJut.Threading
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using AJut.Storage;

    /// <summary>
    /// A utility class that spins up a thread, and manages an input queue, and output queue and state.
    /// </summary>
    /// <typeparam name="TInput">The type of input queue items (input items are queued, processed, and then removed)</typeparam>
    /// <typeparam name="TExecutionState">The type of execution state items (items that are utilized by the thread actor)</typeparam>
    /// <typeparam name="TOutput">The type of output queue items (outputs are created by the thread actor, and are expected to be removed by the worker owner)</typeparam>
    public class ThreadWorker<TInput, TExecutionState, TOutput> : IDisposable
    {
        private static int kDefaultThreadNameCounter = 0;
        public delegate void FrameActor (ThreadWorkerDataTracker<TInput, TExecutionState, TOutput> data);

        private Thread m_thread;
        private ThreadWorkerDataTracker<TInput, TExecutionState, TOutput> m_activeThreadData;
        private TaskCompletionSource<bool> m_gracefulShutdownIndicator;
        private TaskCompletionSource<bool> m_allInputProcessingCompleted;
        private readonly object m_inputProcessingWaitLock = new object();
        private CancellationTokenRegistration m_cancellationRegistration;

        // ==================[ Properties ]============================
        public event EventHandler<EventArgs> IsActiveChanged;
        public bool IsActive => m_thread != null;
        public Result LastRunResult { get; private set; } = Result.Success();

        /// <summary>
        /// Work items to process - these items are added externally and removed as they are processed
        /// </summary>
        public ThreadWorkerDataQueue<TInput> InputToProcess { get; } = new ThreadWorkerDataQueue<TInput>();

        /// <summary>
        /// Output created from the work item inputs
        /// </summary>
        public ThreadWorkerDataQueue<TOutput> OutputResults { get; } = new ThreadWorkerDataQueue<TOutput>();

        /// <summary>
        /// State that the thread uses to execute every iteration (if any)
        /// </summary>
        public ThreadWorkerState<TExecutionState> ExecutionState { get; } = new ThreadWorkerState<TExecutionState>();

        // ==================[ Methods ]============================

        /// <summary>
        /// Starts up the thread loop with the <see cref="FrameActor"/> that will run per frame of the loop.
        /// </summary>
        /// <param name="frameActor">The <see cref="FrameActor"/> that will run per frame of the loop.</param>
        /// <param name="name">An optional name for the thread</param>
        /// <param name="sleepYieldMs">How long to sleep to yield to other threads (recommend atleast 1)</param>
        public void StartThreadLoop (FrameActor frameActor, string name = null, int sleepYieldMs = 10)
        {
            this.StartThreadLoop(frameActor, CancellationToken.None, name, sleepYieldMs);
        }

        /// <summary>
        /// Starts up the thread loop with the <see cref="FrameActor"/> that will run per frame of the loop.
        /// </summary>
        /// <param name="frameActor">The <see cref="FrameActor"/> that will run per frame of the loop.</param>
        /// <param name="cancellationToken">A cancellation token for cancelling this execution early</param>
        /// <param name="name">An optional name for the thread</param>
        /// <param name="sleepYieldMs">How long to sleep to yield to other threads (recommend atleast 1)</param>
        public void StartThreadLoop (FrameActor frameActor, CancellationToken cancellationToken, string name = null, int sleepYieldMs = 10)
        {
            name = name ?? $"Thread worker thread #{kDefaultThreadNameCounter++}";
            // Reset
            this.DeactivateAndCleanup();

            // Reset the graceful shutdown indicator. The worker thread completes it, so its continuations are sent to the
            //  thread pool rather than run inline on the worker's way out.
            m_gracefulShutdownIndicator = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            // Create new data
            m_activeThreadData = new ThreadWorkerDataTracker<TInput, TExecutionState, TOutput>(this.InputToProcess, this.ExecutionState, this.OutputResults);

            m_thread = new Thread(new ParameterizedThreadStart(_Runner));
            m_thread.Name = name;
            m_thread.IsBackground = true;

            // Registered after the reset above released the last run's registration, so only this run's token can stop
            //  this run, and before the thread starts, so a token that is already canceled stops it straight away
            m_cancellationRegistration = cancellationToken.Register(this.InitiateGracefulShutdown);
            m_thread.Start(m_activeThreadData);
            this.IsActiveChanged?.Invoke(this, EventArgs.Empty);

            void _Runner (object state)
            {
                var castedState = (ThreadWorkerDataTracker<TInput, TExecutionState, TOutput>)state;
                try
                {
                    while (castedState.ShouldContinue)
                    {
                        frameActor(castedState);
                        Thread.Sleep(sleepYieldMs);
                    }
                }
                catch (Exception exc)
                {
                    this.LastRunResult = Result.Error(exc.Message);
                }
                
                var gracefulShutdownNotifier = m_gracefulShutdownIndicator;
                this.DeactivateAndCleanup();
                gracefulShutdownNotifier?.TrySetResult(true);
            }
        }

        /// <summary>
        /// Waits until the input queue is empty, meaning the frame actor has taken every queued item. The frame that took the
        /// last item may still be running when this finishes; a graceful shutdown requested then lets that frame finish, so
        /// awaiting this before <see cref="ShutdownGracefullyAndWaitForCompletion()"/> is how to have the queue processed
        /// before the loop stops. If the loop stops first, this finishes then too: stopping clears whatever is still queued.
        /// </summary>
        public async Task WhenAllInputProcessingCompleted ()
        {
            TaskCompletionSource<bool> allProcessed;
            lock (m_inputProcessingWaitLock)
            {
                if (m_allInputProcessingCompleted == null)
                {
                    m_allInputProcessingCompleted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                    this.InputToProcess.DataProcessed -= this.InputToProcess_DataProcessed;
                    this.InputToProcess.DataProcessed += this.InputToProcess_DataProcessed;
                }

                allProcessed = m_allInputProcessingCompleted;
            }

            // Checked once the handler is in place, so the last item can't be taken unseen between a check and the subscribe
            this.CompleteInputProcessingWaitIfQueueIsEmpty();
            await allProcessed.Task.ConfigureAwait(false);
        }

        /// <summary>
        /// Requests a graceful shutdown without waiting for it: the frame running now finishes, and no further frame starts.
        /// Input still queued is not processed. See <see cref="ShutdownGracefullyAndWaitForCompletion()"/>.
        /// </summary>
        public void InitiateGracefulShutdown ()
        {
            // Read once: the worker clears it on its way out, from its own thread
            m_activeThreadData?.InitiateGracefulShutdown();
        }

        /// <summary>
        /// Requests a graceful shutdown and waits, with no time limit, for the loop to stop. Graceful means the frame running
        /// now finishes and no further frame starts, so input still queued is not processed. To have the queue processed
        /// first, await <see cref="WhenAllInputProcessingCompleted"/> before calling this.
        /// </summary>
        /// <returns>true once the loop has stopped, or straight away if it was not running</returns>
        public Task<bool> ShutdownGracefullyAndWaitForCompletion ()
        {
            // No limit is Timeout.InfiniteTimeSpan (-1 ms), which is how CancellationTokenSource.CancelAfter, Task.Delay and
            //  Timer all take it. TimeSpan.MaxValue is not "no limit" to any of them: a timer holds at most uint.MaxValue - 1
            //  milliseconds (about 49 days), and anything longer throws ArgumentOutOfRangeException.
            return this.ShutdownGracefullyAndWaitForCompletion(Timeout.InfiniteTimeSpan);
        }

        /// <summary>
        /// Requests a graceful shutdown and waits up to <paramref name="timeout"/> for the loop to stop. Graceful means the frame
        /// running now finishes and no further frame starts (see <see cref="ShutdownGracefullyAndWaitForCompletion()"/>). A
        /// timeout does not stop the loop any sooner: the stop stays requested, and the loop stops once its current frame ends.
        /// </summary>
        /// <param name="timeout">How long to wait, or <see cref="Timeout.InfiniteTimeSpan"/> to wait with no limit</param>
        /// <returns>true if the loop stopped within the timeout, or was not running; false if the timeout passed first</returns>
        public async Task<bool> ShutdownGracefullyAndWaitForCompletion (TimeSpan timeout)
        {
            // Read before the stop is requested: the worker clears both on its way out, so a read after the request could find
            //  nothing to wait on, or nothing to stop
            TaskCompletionSource<bool> gracefulShutdown = m_gracefulShutdownIndicator;
            ThreadWorkerDataTracker<TInput, TExecutionState, TOutput> activeThreadData = m_activeThreadData;
            if (gracefulShutdown == null || activeThreadData == null)
            {
                return true;
            }

            activeThreadData.InitiateGracefulShutdown();
            if (timeout == Timeout.InfiniteTimeSpan)
            {
                return await gracefulShutdown.Task.ConfigureAwait(false);
            }

            // The timeout is a delay raced against the shutdown, rather than a cancel of the shutdown's completion source, so
            //  it leaves that source for anyone else waiting on it, and a timeout comes back as false instead of an exception.
            //  The delay is canceled once the race is decided, so its timer doesn't outlive the wait.
            using (var delayCancellation = new CancellationTokenSource())
            {
                Task finishedFirst = await Task.WhenAny(
                    gracefulShutdown.Task,
                    Task.Delay(timeout, delayCancellation.Token)
                ).ConfigureAwait(false);
                delayCancellation.Cancel();
                return finishedFirst == gracefulShutdown.Task;
            }
        }

        private void InputToProcess_DataProcessed (object sender, EventArgs e)
        {
            this.CompleteInputProcessingWaitIfQueueIsEmpty();
        }

        private void CompleteInputProcessingWaitIfQueueIsEmpty ()
        {
            TaskCompletionSource<bool> allProcessed;
            lock (m_inputProcessingWaitLock)
            {
                if (m_allInputProcessingCompleted == null || this.InputToProcess.Any())
                {
                    return;
                }

                allProcessed = m_allInputProcessingCompleted;
                m_allInputProcessingCompleted = null;
                this.InputToProcess.DataProcessed -= this.InputToProcess_DataProcessed;
            }

            allProcessed.TrySetResult(true);
        }

        private void DeactivateAndCleanup ()
        {
            m_activeThreadData?.ExecutionState.DisposeAndClearDisposables();
            m_activeThreadData?.InputToProcess.DisposeAndClearDisposables();
            m_activeThreadData?.OutputResults.DisposeAndClearDisposables();

            // The input queue was just cleared without a DataProcessed, so anyone waiting for it to empty is told here
            this.CompleteInputProcessingWaitIfQueueIsEmpty();

            // Released with the run it belongs to, so the token neither stops a later run nor keeps this worker alive
            m_cancellationRegistration.Dispose();
            m_cancellationRegistration = default;

            m_thread = null;
            m_activeThreadData = null;
            m_gracefulShutdownIndicator = null;
            this.IsActiveChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Dispose ()
        {
            this.InitiateGracefulShutdown();
            this.DeactivateAndCleanup();
        }
    }

    public class TNone { }

    /// <summary>
    /// A <see cref="ThreadWorker{TInput, TExecutionState, TOutput}"/> that does not utilize execution state, simply translates inputs into outputs
    /// </summary>
    public class ThreadWorker<TInput, TOutput> : ThreadWorker<TInput, TNone, TOutput> { }

    /// <summary>
    /// A <see cref="ThreadWorker{TInput, TExecutionState, TOutput}"/> that does not utilize any inputs, just operates on some execution state and produces outputs.
    /// </summary>
    public class LoopingThreadWorker<TExecutionState, TOutput> : ThreadWorker<TNone, TExecutionState, TOutput> { }
}
