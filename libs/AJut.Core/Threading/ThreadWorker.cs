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
            cancellationToken.Register(this.InitiateGracefulShutdown);
            name = name ?? $"Thread worker thread #{kDefaultThreadNameCounter++}";
            // Reset
            this.DeactivateAndCleanup();

            // Reset graceful shutdown indicators
            m_gracefulShutdownIndicator = new TaskCompletionSource<bool>();

            // Create new data
            m_activeThreadData = new ThreadWorkerDataTracker<TInput, TExecutionState, TOutput>(this.InputToProcess, this.ExecutionState, this.OutputResults);

            m_thread = new Thread(new ParameterizedThreadStart(_Runner));
            m_thread.Name = name;
            m_thread.IsBackground = true;
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

        public async Task WhenAllInputProcessingCompleted ()
        {
            if (!this.InputToProcess.Any())
            {
                return;
            }

            if (m_allInputProcessingCompleted != null)
            {
                await m_allInputProcessingCompleted.Task;
                return;
            }

            m_allInputProcessingCompleted = new TaskCompletionSource<bool>();
            this.InputToProcess.DataProcessed += _OnInputProcessed;
            await m_allInputProcessingCompleted.Task;

            void _OnInputProcessed (object sender, EventArgs e)
            {
                if (!this.InputToProcess.Any())
                {
                    this.InputToProcess.DataProcessed -= _OnInputProcessed;
                    m_allInputProcessingCompleted.TrySetResult(true);
                    m_allInputProcessingCompleted = null;
                }
            }
        }

        public void InitiateGracefulShutdown ()
        {
            if (m_thread == null)
            {
                return;
            }

            m_activeThreadData.InitiateGracefulShutdown();
        }

        public async Task<bool> ShutdownGracefullyAndWaitForCompletion ()
        {
            return await this.ShutdownGracefullyAndWaitForCompletion(TimeSpan.MaxValue).ConfigureAwait(false);
        }
        public async Task<bool> ShutdownGracefullyAndWaitForCompletion (TimeSpan timeout)
        {
            if (!this.IsActive)
            {
                return true;
            }

            // Initiate the graceful shutdown
            m_activeThreadData.InitiateGracefulShutdown();

            // Create a cancellation token
            var gracefulShutdown = m_gracefulShutdownIndicator;
            CancellationTokenSource gracefulShutdownTimeout = new CancellationTokenSource();
            gracefulShutdownTimeout.Token.Register(() => gracefulShutdown?.TrySetCanceled());
            gracefulShutdownTimeout.CancelAfter(timeout);

            // Await the task completion source to confirm that graceful shutdown has completed
            if (await gracefulShutdown.Task)
            {
                return true;
            }

            return false;
        }

        private void DeactivateAndCleanup ()
        {
            m_activeThreadData?.ExecutionState.DisposeAndClearDisposables();
            m_activeThreadData?.InputToProcess.DisposeAndClearDisposables();
            m_activeThreadData?.OutputResults.DisposeAndClearDisposables();
            m_thread = null;
            m_activeThreadData = null;
            m_gracefulShutdownIndicator = null;
            this.IsActiveChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Dispose ()
        {
            m_activeThreadData.InitiateGracefulShutdown();
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
