namespace AJut
{
    /// <summary>
    /// Controls what the <see cref="Logger"/> allows through. Used on the <see cref="LogVerbosityManager"/>.
    /// </summary>
    public enum eLogVerbositySetting
    {
        None       = 0,
        ErrorsOnly = 1,
        Normal     = 2,
        Detailed   = 3,
        Verbose    = 4,
    }
}
