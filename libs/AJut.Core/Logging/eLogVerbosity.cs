namespace AJut
{
    /// <summary>
    /// Optional verbosity level for individual <see cref="Logger.LogInfo"/> calls.
    /// Integer values are shared with <see cref="eLogVerbositySetting"/> - gate check: (int)callVerbosity &lt;= (int)EffectiveSetting.
    /// </summary>
    public enum eLogVerbosity
    {
        Normal   = 2,
        Detailed = 3,
        Verbose  = 4,
    }
}
