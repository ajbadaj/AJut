namespace AJut
{
    /// <summary>
    /// Optional verbosity level for individual <see cref="Logger.LogInfo"/> calls.
    /// Integer values are shared with <see cref="eLogVerbositySetting"/> - gate check: (int)callVerbosity &lt;= (int)EffectiveSetting.
    /// </summary>
    public enum eLogVerbosity
    {
        /// <summary>Bypasses ALL verbosity filtering, including the None gate. Always logged.</summary>
        Force    = 0,
        Normal   = 2,
        Detailed = 3,
        Verbose  = 4,
    }
}
