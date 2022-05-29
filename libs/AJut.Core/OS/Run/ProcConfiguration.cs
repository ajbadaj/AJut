namespace AJut.OS.Run
{
    /*
    ProcConfiguration m_p4ProcConfig = new ProcConfiguration("p4");
    ProcRunner m_p4 = m_p4ProcConfig.BuildRunner(timeout=300);
    
    var result = m_p4.Run("edit C:\\this.txt");
    if(!result.Success)
    {
        Logger.LogError(result.GenerateFailureReport());
    }
    
    */

    /// <summary>
    /// A configuration to run a process
    /// </summary>
    public class ProcConfiguration
    {
        public const int kNoTimeout = -1;
        public static readonly ExitCodeChecker kDefaultExitCodeChecker = (code) => code == 0;

        public ProcConfiguration (string processPath)
        {
            this.ProccessPath = processPath;
        }

        /// <summary>
        /// The path or name of the process being run
        /// </summary>
        public string ProccessPath { get; set; }


        /// <summary>
        /// The working directory to run the process out of (default=<c>null</c> - not set in process)
        /// </summary>
        public string WorkingDir { get; set; }

        /// <summary>
        /// Either <see cref="eProcessDisplayRunMode.Visible"/> to show the process window, or <see cref="eProcessDisplayRunMode.Hidden"/> to run with process window in the hidden state (default=<see cref="eProcessDisplayRunMode.Hidden"/>)
        /// </summary>
        public eProcessDisplayRunMode DisplayMode { get; set; } = eProcessDisplayRunMode.Hidden;

        /// <summary>
        /// How long to wait before killing the process. (default=<see cref="kNoTimeout"/>)
        /// </summary>
        public int Timeout { get; set; } = kNoTimeout;

        /// <summary>
        /// Makes it so the <see cref="ProcRunResults"/> is in a failure result if any redirected error text is received (default=<c>true</c>)
        /// </summary>
        public bool FailIfAnyErrors { get; set; } = true;

        /// <summary>
        /// A function to run that validates the exit code of the process (default will resolve return of 0 as success and anything else failure)
        /// </summary>
        public ExitCodeChecker EvaluateDidProcSucceed { get; set; } = kDefaultExitCodeChecker;

        /// <summary>
        /// Build a <see cref="ProcRunner"/> to run and manage capturing output for the process described in this config
        /// </summary>
        /// <returns></returns>
        public ProcRunner BuildRunner () => new ProcRunner(this);

        /// <summary>
        /// Build a <see cref="ProcRunner"/> to run and manage capturing output for the process described in this config.
        /// </summary>
        /// <remarks>
        /// Note: This will generate a duplicate <see cref="ProcConfiguration"/> with the provided overrides!
        /// </remarks>
        /// <param name="workingDir">The working directory to run the process out of (default=<c>null</c> - not set in process)</param>
        /// <param name="displayMode">Either <see cref="eProcessDisplayRunMode.Visible"/> to show the process window, or <see cref="eProcessDisplayRunMode.Hidden"/> to run with process window in the hidden state (default=<see cref="eProcessDisplayRunMode.Hidden"/>)</param>
        /// <param name="timeout">How long to wait before killing the process. (default=<see cref="kNoTimeout"/>)</param>
        /// <param name="failIfAnyErrors">Makes it so the <see cref="ProcRunResults"/> is in a failure result if any redirected error text is received (default=<c>true</c>)</param>
        /// <param name="evaluateDidProcSucceed">A function to run that validates the exit code of the process (default will resolve return of 0 as success and anything else failure)</param>
        /// <returns>A <see cref="ProcRunner"/> built with the specified settings</returns>
        public ProcRunner BuildRunnerWithOverrides (string workingDir = null, eProcessDisplayRunMode? displayMode = null, int? timeout = null, bool? failIfAnyErrors = null, ExitCodeChecker evaluateDidProcSucceed = null)
        {
            return new ProcRunner(
                new ProcConfiguration(this.ProccessPath)
                {
                    WorkingDir = workingDir ?? this.WorkingDir,
                    DisplayMode = displayMode ?? this.DisplayMode,
                    Timeout = timeout ?? this.Timeout,
                    FailIfAnyErrors = failIfAnyErrors ?? this.FailIfAnyErrors,
                    EvaluateDidProcSucceed = evaluateDidProcSucceed ?? this.EvaluateDidProcSucceed
                }
            );
        }
    }

}
