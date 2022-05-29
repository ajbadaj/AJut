namespace AJut.OS.Run
{
    using System;

    /// <summary>
    /// The results of running a process via the <see cref="ProcRunner"/>
    /// </summary>
    public class ProcRunResults
    {
        internal protected ProcRunResults (ProcConfiguration config)
        {
            this.Configuration = config;
            this.Success = false;
        }

        /// <summary>
        /// The configuration used to generate the results
        /// </summary>
        public ProcConfiguration Configuration { get; }

        /// <summary>
        /// The output text that was collected
        /// </summary>
        public string OutputText { get; private set; }

        /// <summary>
        /// The error text that was collected
        /// </summary>
        public string ErrorText { get; private set; }

        /// <summary>
        /// The exit code given by the process when it exited
        /// </summary>
        public int ExitCode { get; private set; }

        /// <summary>
        /// Indicates if the process was cancelled programatically
        /// </summary>
        public bool WasCancelled { get; private set; }

        /// <summary>
        /// Indicates if the process was successful (as outlined by the <see cref="Configuration"/>)
        /// </summary>
        public bool Success { get; private set; }

        /// <summary>
        /// Indicates if timeout was the cause of the failure if it had failed
        /// </summary>
        public bool IsFailureDueToTimeout { get; private set; }

        /// <summary>
        /// Indicates the exception that was encountered
        /// </summary>
        public Exception ExceptionEncountered { get; private set; }

        /// <summary>
        /// Generates a report of what/how the process run failed (or <c>null</c> if the run was successful)
        /// </summary>
        public string GenerateFailureReport ()
        {
            if (this.Success)
            {
                return null;
            }

            if (this.IsFailureDueToTimeout)
            {
                return "Timeout of {0} reached!".ApplyFormatArgs(this.Configuration.Timeout);
            }

            if (this.ExceptionEncountered != null)
            {
                return "Exception encountered: " + this.ExceptionEncountered;
            }

            if (this.WasCancelled)
            {
                return "Operation cancelled.";
            }

            return "Non-specific error (probably in startup)";
        }

        /// <summary>
        /// Marks result gathering as complete, this is called after a graceful shutdown of the run process, and success is evaluated
        /// </summary>
        internal protected void AllDone (string output, string failures, int exitCode)
        {
            this.OutputText = output;
            this.ErrorText = failures;
            this.ExitCode = exitCode;

            bool wereThereErrors = this.Configuration.FailIfAnyErrors && !String.IsNullOrWhiteSpace(this.ErrorText);
            bool exitCodeIndicatesSuccess = this.Configuration.EvaluateDidProcSucceed?.Invoke(this.ExitCode) ?? true;

            this.Success = !wereThereErrors && exitCodeIndicatesSuccess;
        }

        /// <summary>
        /// Marks result gathering as complete, called when there is a non-specific failure
        /// </summary>
        internal protected void FailedNonspecific ()
        {
            this.Success = false;
        }

        /// <summary>
        /// Marks result gathering as complete, called when the timeout is reached
        /// </summary>
        internal protected void FailedDueToTimeout ()
        {
            this.Success = false;
            this.IsFailureDueToTimeout = true;
        }

        /// <summary>
        /// Marks result gathering as complete, called to manage storage of an exception
        /// </summary>
        internal protected void FailedDueToException (Exception exc)
        {
            this.Success = false;
            this.ExceptionEncountered = exc;
        }

        /// <summary>
        /// Marks result gathering as complete, called to indicate process was stopped short via a cancellation
        /// </summary>
        internal protected void FailedDueToCancellation ()
        {
            this.Success = false;
            this.WasCancelled = true;
        }
    }
}
