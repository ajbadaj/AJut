namespace AJut.Application
{
    using System;
    using System.Diagnostics;
    using System.Text;
    using System.Threading.Tasks;
    using AJut.Text;
    using System.Threading;

    /// <summary>
    /// Provides a way to validate exit codes.
    /// </summary>
    /// <param name="exitCode">The exit code to test</param>
    /// <returns><c>true</c> for successful exit code, <c>false</c> for failure exit code</returns>
    public delegate bool ExitCodeChecker(int exitCode);

    public enum eOutputType {  Output, Error };
    public class ProcessOutputReceivedEventArgs : EventArgs
    {
        public eOutputType OutputType { get; private set; }
        public string OutputText { get; private set; }

        public static ProcessOutputReceivedEventArgs Output(string text)
        {
            return new ProcessOutputReceivedEventArgs
                {
                    OutputType = eOutputType.Output,
                    OutputText = text
                };
        }
        public static ProcessOutputReceivedEventArgs Error(string text)
        {
            return new ProcessOutputReceivedEventArgs
            {
                OutputType = eOutputType.Error,
                OutputText = text
            };
        }
    }

    public class ProcConfiguration
    {
        public string Proc { get; private set; }

        public ProcConfiguration(string proc)
        {
            this.Proc = proc;
        }

        /// <summary>
        /// Builds a runner to execute
        /// </summary>
        /// <param name="workingDir">The working directory (default=<c>null</c> - not set in process)</param>
        /// <param name="displayStyle">Either <see cref="eDisplayStyle.Visible"/> to show a cmd window, or <see cref="eDisplayStyle.Hidden"/> to run with no cmd window (default=<see cref="eDisplayStyle.Hidden"/>)</param>
        /// <param name="timeout">How long to wait before killing the process. (default=<see cref="ProcRunner.kNoTimeout"/>)</param>
        /// <param name="failIfAnyErrors">Makes it so the <see cref="ProcRunResults"/> is a failure result if any redirected error text is received (default=<c>true</c>)</param>
        /// <param name="exitCodeChecker">A function to run that validates the exit code of the process (default=<c>null</c> - Makes 0 success and anything else failure)</param>
        /// <returns>A <see cref="ProcRunner"/> built with the specified settings</returns>
        public ProcRunner BuildRunner(string workingDir = null, eDisplayStyle displayStyle = eDisplayStyle.Hidden, int timeout = ProcRunner.kNoTimeout, bool failIfAnyErrors = true, ExitCodeChecker exitCodeChecker = null)
        {
            return new ProcRunner(this, workingDir, displayStyle, timeout, failIfAnyErrors, exitCodeChecker);
        }
    }


    /*
    ProcConfiguration m_p4ProcConfig = new ProcConfiguration("p4");
    ProcRunner m_p4 = m_p4ProcConfig.BuildRunner(timeout=300);
    
    var result = m_p4.Run("edit C:\\this.txt");
    if(!result.Success)
    {
        Logger.LogError(result.GenerateFailureReport());
    }
    
    */




    public class ProcRunner
    {
        public const int kNoTimeout = -1;
        private static readonly ExitCodeChecker kDefaultExitCodeChecker = (code) => code == 0;

        public int Timeout { get; private set; }
        public bool FailIfAnyErrors { get; private set; }
        public ExitCodeChecker ExitCodeValidator { get; private set; }
        public ProcConfiguration Configuration { get; private set; }

        public string WorkingDir { get; private set; }

        public eDisplayStyle DisplayStyle { get; private set; }

        internal CancellationToken CancellationToken
        {
            get
            {
                return m_cancellor.Token;
            }
        }

        public event EventHandler<ProcessOutputReceivedEventArgs> OutputReceived;

        private CancellationTokenSource m_cancellor = new CancellationTokenSource();

        internal ProcRunner(ProcConfiguration owner, string workingDir, eDisplayStyle displayStyle, int timeout, bool failIfAnyErrors, ExitCodeChecker exitCodeChecker)
        {
            this.Configuration = owner;
            this.WorkingDir = workingDir;
            this.DisplayStyle = displayStyle;
            this.Timeout = timeout;
            this.FailIfAnyErrors = failIfAnyErrors;
            this.ExitCodeValidator = exitCodeChecker;
        }

        public void Cancel()
        {
            m_cancellor.Cancel();
        }


        public async Task<ProcRunResults> RunAsync(string argsFormat, params object[] args)
        {
            return await Task.Run(() => Run(argsFormat, args), this.CancellationToken);
        }

        public ProcRunResults Run(string argsFormat, params object[] args)
        {
            string procArgs = argsFormat.ApplyFormatArgs(args) ?? String.Empty;
            ProcRunResults results = new ProcRunResults(this.Configuration.Proc, procArgs, this.Timeout, this.DisplayStyle, this.WorkingDir);
            this.CancellationToken.Register(results.FailedDueToCancellation);

            try
            {
                Process proc = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = this.Configuration.Proc,
                        Arguments = procArgs,
                        UseShellExecute = this.DisplayStyle == eDisplayStyle.Visible,
                        CreateNoWindow = this.DisplayStyle == eDisplayStyle.Hidden,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                    },
                };
                this.CancellationToken.Register(proc.Kill);

                if (!String.IsNullOrEmpty(this.WorkingDir))
                {
                    proc.StartInfo.WorkingDirectory = this.WorkingDir;
                }


                StringBuilder outputBuilder = new StringBuilder();
                proc.OutputDataReceived += (s, e) =>
                {
                    if (e.Data != null)
                    {
                        this.OutputReceived.Execute(this, ProcessOutputReceivedEventArgs.Output(e.Data));
                        outputBuilder.AppendLine(e.Data);
                    }
                };

                StringBuilder errorBuilder = new StringBuilder();
                proc.ErrorDataReceived += (s, e) =>
                {
                    if (e.Data != null)
                    {
                        this.OutputReceived.Execute(this, ProcessOutputReceivedEventArgs.Error(e.Data));

                        outputBuilder.AppendLine(e.Data);
                        errorBuilder.AppendLine(e.Data);
                    }
                };

                proc.Start();

                proc.BeginOutputReadLine();
                proc.BeginErrorReadLine();

                if (this.Timeout == kNoTimeout)
                {
                    proc.WaitForExit();
                }
                else
                {
                    if (!proc.WaitForExit(this.Timeout))
                    {
                        results.FailedDueToTimeout();
                        return results;
                    }
                }

                results.Complete(outputBuilder.ToString(), errorBuilder.ToString(), proc.ExitCode, this.ExitCodeValidator, this.FailIfAnyErrors);
                return results;
            }
            catch (Exception exc)
            {
                results.FailedDueToException(exc);
                return results;
            }
        }
    }

    public class ProcRunResults
    {
        /// <summary>
        /// The process that was run (could be a name or a full path)
        /// </summary>
        public string ProcRun { get; private set; }

        public string ArgsUsed { get; private set; }

        public string WorkingDir { get; private set; }

        public int TimeoutProvided { get; private set; }

        public eDisplayStyle DisplayStyle { get; private set; }

        public string OutputText { get; private set; }
        public string ErrorText { get; private set; }
        public int ExitCode { get; private set; }

        public bool WasCancelled { get; private set; }

        public bool Success { get; private set; }

        public bool IsFailureDueToTimeout { get; private set; }

        public Exception ExceptionEncountered { get; private set; }

        public string GenerateFailureReport()
        {
            if(this.Success)
            {
                return null;
            }

            if (this.IsFailureDueToTimeout)
            {
                return "Timeout of {0} reached!".ApplyFormatArgs(this.TimeoutProvided);
            }

            if(this.ExceptionEncountered != null)
            {
                return "Exception encountered: " + this.ExceptionEncountered;
            }

            if(this.WasCancelled)
            {
                return "Operation cancelled.";
            }

            return "Non-specific error (probably in startup)";
        }

        internal ProcRunResults(string proc, string args, int timeout, eDisplayStyle displayStyle, string workingDir)
        {
            this.ProcRun = proc;
            this.ArgsUsed = args;
            this.TimeoutProvided = timeout;
            this.DisplayStyle = displayStyle;
            this.WorkingDir = workingDir;
            this.Success = true;
        }

        internal void Complete(string output, string failures, int exitCode, ExitCodeChecker exitCodeChecker, bool failIfAnyErrors)
        {
            this.OutputText = output;
            this.ErrorText = failures;
            this.ExitCode = exitCode;

            if ( (failIfAnyErrors && !String.IsNullOrWhiteSpace(this.ErrorText)) 
                    || (exitCodeChecker != null && !exitCodeChecker(this.ExitCode)) )
            {
                this.Success = false;
            }
        }

        internal void FailedNonSpecific()
        {
            this.Success = false;
        }

        internal void FailedDueToTimeout()
        {
            this.Success = false;
            this.IsFailureDueToTimeout = true;
        }

        internal void FailedDueToException(Exception exc)
        {
            this.Success = false;
            this.ExceptionEncountered = exc;
        }
        internal void FailedDueToCancellation()
        {
            this.Success = false;
            this.WasCancelled = true;
        }
    }

    public enum eDisplayStyle
    {
        Hidden,
        Visible
    }
}
