namespace AJut.Threading
{
    using System.Threading;

    public class ThreadWorkerDataTracker<TInput, TExecutionState, TOutput>
    {
        private int m_isStopRequested = 0;

        internal ThreadWorkerDataTracker (ThreadWorkerDataQueue<TInput> toProcess, ThreadWorkerState<TExecutionState> executionState, ThreadWorkerDataQueue<TOutput> outputState)
        {
            this.InputToProcess = toProcess;
            this.ExecutionState = executionState;
            this.OutputResults = outputState;
        }

        public ThreadWorkerDataQueue<TInput> InputToProcess { get; }
        public ThreadWorkerDataQueue<TOutput> OutputResults { get; }
        public ThreadWorkerState<TExecutionState> ExecutionState { get; }
        private bool HasGracefulShutdownBeenRequested
        {
            get => Interlocked.CompareExchange(ref m_isStopRequested, 0, 1) == 1;
            set => Interlocked.Exchange(ref m_isStopRequested, value ? 1 : 0);
        }

        public bool ShouldContinue => !this.HasGracefulShutdownBeenRequested;

        internal void InitiateGracefulShutdown ()
        {
            this.HasGracefulShutdownBeenRequested = true;
        }
    }
}
