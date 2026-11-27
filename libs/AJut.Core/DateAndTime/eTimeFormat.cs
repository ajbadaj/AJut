namespace AJut.DateAndTime
{
    /// <summary>
    /// Which clock a time of day is shown on: the culture's own, or one chosen whatever the culture.
    /// <see cref="TimeOfDayRules.GetShortTimePattern"/> gives the short time pattern each one comes to in a culture.
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
