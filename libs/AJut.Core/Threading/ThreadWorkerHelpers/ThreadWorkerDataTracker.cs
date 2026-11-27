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
            // Compares 0 with 0, so it never changes the value: an atomic read. Swapping in 0 when it finds 1 would clear the
            //  request for whoever reads it next, and the frame actor and the loop both read it.
            get => Interlocked.CompareExchange(ref m_isStopRequested, 0, 0) == 1;
            set => Interlocked.Exchange(ref m_isStopRequested, value ? 1 : 0);
        }

        public bool ShouldContinue => !this.HasGracefulShutdownBeenRequested;

        internal void InitiateGracefulShutdown ()
        {
            this.HasGracefulShutdownBeenRequested = true;
        }
    }
}
