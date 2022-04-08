namespace AJut.OS.Run
{
    /// <summary>
    /// Provides a way to validate exit codes.
    /// </summary>
    /// <param name="exitCode">The exit code to test</param>
    /// <returns><c>true</c> for successful exit code, <c>false</c> for failure exit code</returns>
    public delegate bool ExitCodeChecker (int exitCode);

    /// <summary>
    /// Qualifier of process output type
    /// </summary>
    public enum eProcOutputType { Output, Error };

    public enum eProcessDisplayRunMode
    {
        Hidden,
        Visible
    }
}
