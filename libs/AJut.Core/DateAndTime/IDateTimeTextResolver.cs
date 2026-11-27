// AJut.Core leaves nullable reference types off, and this code is written with them on
#nullable enable

namespace AJut.DateAndTime
{
    using System;
    using System.Globalization;

    /// <summary>
    /// Turns text someone typed into a date and time, and a date and time back into text to show.
    /// <see cref="DateTimeTextResolver"/> is the one used when no other is given, by <see cref="DateTimeEditRules"/> and
    /// so by every DateTimeEditIngressControl given none. Write another to read a product's own shorthand, or to show
    /// values its own way, and fall back to <see cref="DateTimeTextResolver.Default"/> for everything else.
    /// </summary>
    public interface IDateTimeTextResolver
    {
        /// <summary>
        /// Reads <paramref name="text"/> as a date and time. <see cref="DateTimeEditRules.Evaluate"/> settles empty text
        /// itself and never hands it on, and it shapes the answer to what is edited afterwards (a date only edit drops the
        /// time of day, a time only edit keeps the date it already had), so a resolver can answer with a full date and
        /// time.
        /// </summary>
        DateTimeTextResolution Resolve (string text, DateTimeTextContext context);

        /// <summary>
        /// The text <paramref name="value"/> is shown as, which is empty for no value. A person editing the value starts
        /// from this text, so <see cref="Resolve"/> should read it back, as the default resolver does.
        /// </summary>
        string Format (DateTime? value, DateTimeTextContext context);
    }

    /// <summary>What an <see cref="IDateTimeTextResolver"/> is told about the edit it reads or writes text for</summary>
    /// <param name="Now">The moment relative text ("tomorrow", "+3d") counts from</param>
    /// <param name="Current">The value held as the text is read, or null for none</param>
    /// <param name="IncludeDate">Whether the date is edited</param>
    /// <param name="IncludeTime">Whether the time of day is edited</param>
    /// <param name="DisplayFormat">The format the value is shown in, or null for the culture's own</param>
    /// <param name="Culture">The culture dates and times are read and written in</param>
    /// <param name="TimeFormat">The clock a time is shown on when there is no display format: the culture's own, or one chosen</param>
    public readonly record struct DateTimeTextContext (DateTime Now, DateTime? Current, bool IncludeDate, bool IncludeTime, string? DisplayFormat, CultureInfo Culture, eTimeFormat TimeFormat = eTimeFormat.CurrentCulture)
    {
        /// <summary>Whether the date is part of what is edited. Including neither edits both.</summary>
        public bool ShowsDate => this.IncludeDate || !this.IncludeTime;

        /// <summary>Whether the time of day is part of what is edited. Including neither edits both.</summary>
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
