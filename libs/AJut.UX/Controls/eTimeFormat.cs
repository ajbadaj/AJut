namespace AJut.UX.Controls
{
    /// <summary>
    /// Which clock a time of day is shown and picked on: the culture's own, or one chosen whatever the culture. It sets
    /// the shown text when no display format is given, and the time picker either way.
    /// </summary>
    public enum eTimeFormat
    {
        /// <summary>The clock the current culture writes its short time on: 3:05 PM in en-US, 15:05 in de-DE</summary>
        CurrentCulture,

        /// <summary>A twelve hour clock with AM and PM, as 3:05 PM, whatever the culture</summary>
        TwelveHour,

        /// <summary>A twenty four hour clock with no AM or PM, as 15:05, whatever the culture</summary>
        TwentyFourHour,
    }
}
