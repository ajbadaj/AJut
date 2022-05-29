namespace AJut.OS.Run
{
    using System;
    using System.Diagnostics;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// A trackable run of a process which runs as outlined by it's <see cref="ProcConfiguration"/>. Set it up to track the results however you would like, then run it start it up.
    /// Results will be captured for you, and should errors occur - error reports of the results can be generated.
    /// </summary>
    /// <example>
    /// ProcConfiguration m_p4ProcConfig = new ProcConfiguration("p4");
    /// ...
    /// ProcRunner p4 = m_p4ProcConfig.BuildRunner(timeout = 300);
    /// 
    /// var result = p4.Run("edit C:\\this.txt");
    /// if(!result.Success)
    /// {
    ///     Logger.LogError(result.GenerateFailureReport());
    /// }
    /// </example>
    public class ProcRunner
    {
        public ProcRunner (string processPath, string workingDir, eProcessDisplayRunMode displayMode, int timeout, bool failIfAnyErrors, ExitCodeChecker evaluateDidProcSucceed)
        {
            this.Configuration = new ProcConfiguration(processPath)
            {
                WorkingDir = workingDir,
                DisplayMode = displayMode,
                Timeout = timeout,
                FailIfAnyErrors = failIfAnyErrors,
                EvaluateDidProcSucceed = evaluateDidProcSucceed,
            };
        }
        internal ProcRunner (ProcConfiguration config)
        {
            this.Configuration = config;
        }


        public ProcConfiguration Configuration { get; set; }

        protected internal CancellationToken CancellationToken => m_cancellor.Token;

        public event EventHandler<ProcessOutputReceivedEventArgs> OutputReceived;

        private CancellationTokenSource m_cancellor = new CancellationTokenSource();

        public void Cancel ()
        {
            m_cancellor.Cancel();
        }

        /// <summary>
        /// Run the process
        /// </summary>
        /// <param name="arguments">The arguments to pass to the process (default=<c>null</c>)</param>
        public ProcRunResults Run (string arguments = null)
        {
            arguments = arguments ?? String.Empty;
            ProcRunResults results = new ProcRunResults(this.Configuration);
            this.CancellationToken.Register(results.FailedDueToCancellation);

            try
            {
                Process proc = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = this.Configuration.ProccessPath,
                        Arguments = arguments,
                        UseShellExecute = this.Configuration.DisplayMode == eProcessDisplayRunMode.Visible,
                        CreateNoWindow = this.Configuration.DisplayMode == eProcessDisplayRunMode.Hidden,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                    },
                };
                this.CancellationToken.Register(() => proc.Kill(entireProcessTree: true));

                if (!String.IsNullOrEmpty(this.Configuration.WorkingDir))
                {
                    proc.StartInfo.WorkingDirectory = this.Configuration.WorkingDir;
                }


                StringBuilder outputBuilder = new StringBuilder();
                proc.OutputDataReceived += (s, e) =>
                {
                    if (e.Data != null)
                    {
                        this.OutputReceived?.Invoke(this, ProcessOutputReceivedEventArgs.Output(e.Data));
                        outputBuilder.AppendLine(e.Data);
                    }
                };

                StringBuilder errorBuilder = new StringBuilder();
                proc.ErrorDataReceived += (s, e) =>
                {
                    if (e.Data != null)
                    {
                        this.OutputReceived?.Invoke(this, ProcessOutputReceivedEventArgs.Error(e.Data));

                        outputBuilder.AppendLine(e.Data);
                        errorBuilder.AppendLine(e.Data);
                    }
                };

                proc.Start();

                proc.BeginOutputReadLine();
                proc.BeginErrorReadLine();

                if (this.Configuration.Timeout == ProcConfiguration.kNoTimeout)
                {
                    proc.WaitForExit();
                }
                else
                {
                    if (!proc.WaitForExit(this.Configuration.Timeout))
                    {
                        results.FailedDueToTimeout();
                        return results;
                    }
                }

                results.AllDone(outputBuilder.ToString(), errorBuilder.ToString(), proc.ExitCode);
                return results;
            }
            catch (Exception exc)
            {
                results.FailedDueToException(exc);
                return results;
            }
        }

        /// <summary>
        /// Run the process asyncronously
        /// </summary>
        /// <param name="arguments">The arguments to pass to the process (default=<c>null</c>)</param>
        public async Task<ProcRunResults> RunAsync (string arguments = null)
        {
            return await Task.Run(() => Run(arguments), this.CancellationToken);
        }
    }

}
