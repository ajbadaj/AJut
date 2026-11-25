namespace AJut.UX.Controls
{
    using System;
    using System.Globalization;

    /// <summary>
    /// Turns what someone typed into a DateTimeEditIngressControl into a date and time, and a date and time back into the
    /// text the control shows. <see cref="DateTimeTextResolver"/> is the one used when a control is given none. Write
    /// another to read a product's own shorthand, or to show values its own way, and fall back to
    /// <see cref="DateTimeTextResolver.Default"/> for everything else.
    /// </summary>
    public interface IDateTimeTextResolver
    {
        /// <summary>
        /// Reads <paramref name="text"/> as a date and time. The text is never empty: a control settles an empty box
        /// itself. The control shapes the answer to what it edits afterwards (a date only control drops the time of day,
        /// a time only control keeps the date it already had), so a resolver can answer with a full date and time.
        /// </summary>
        DateTimeTextResolution Resolve (string text, DateTimeTextContext context);

        /// <summary>
        /// The text a control shows for <paramref name="value"/>, which is empty for no value. A person editing the value
        /// starts from this text, so <see cref="Resolve"/> should read it back, as the default resolver does.
        /// </summary>
        string Format (DateTime? value, DateTimeTextContext context);
    }

    /// <summary>What a DateTimeEditIngressControl tells its <see cref="IDateTimeTextResolver"/> about the edit</summary>
    /// <param name="Now">The moment relative text ("tomorrow", "+3d") counts from</param>
    /// <param name="Current">The value the control holds as the text is read, or null for none</param>
    /// <param name="IncludeDate">Whether the control edits the date</param>
    /// <param name="IncludeTime">Whether the control edits the time of day</param>
    /// <param name="DisplayFormat">The format the control was given to show its value in, or null for the culture's own</param>
    /// <param name="Culture">The culture dates and times are read and written in</param>
    /// <param name="TimeFormat">The clock a time is shown on when there is no display format: the culture's own, or one chosen</param>
    public readonly record struct DateTimeTextContext (DateTime Now, DateTime? Current, bool IncludeDate, bool IncludeTime, string? DisplayFormat, CultureInfo Culture, eTimeFormat TimeFormat = eTimeFormat.CurrentCulture)
    {
        /// <summary>Whether the date is part of what is edited. A control set to include neither edits both.</summary>
        public bool ShowsDate => this.IncludeDate || !this.IncludeTime;

        /// <summary>Whether the time of day is part of what is edited. A control set to include neither edits both.</summary>
        public bool ShowsTime => this.IncludeTime || !this.IncludeDate;
    }

    /// <summary>What an <see cref="IDateTimeTextResolver"/> made of some text</summary>
    /// <param name="IsResolved">Whether the text was read</param>
    /// <param name="Value">What it was read as. Null with <paramref name="IsResolved"/> means the text asks for no value.</param>
    /// <param name="FailureReason">Why the text couldn't be read, shown to whoever typed it, or null when it was</param>
    public readonly record struct DateTimeTextResolution (bool IsResolved, DateTime? Value, string? FailureReason)
    {
        /// <summary>The text was read as <paramref name="value"/></summary>
        public static DateTimeTextResolution Resolved (DateTime? value) => new DateTimeTextResolution(IsResolved: true, Value: value, FailureReason: null);

        /// <summary>The text couldn't be read, for <paramref name="reason"/></summary>
        public static DateTimeTextResolution Failed (string reason) => new DateTimeTextResolution(IsResolved: false, Value: null, FailureReason: reason);
    }
}
