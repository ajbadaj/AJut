// AJut.Core leaves nullable reference types off, and this code is written with them on
#nullable enable

namespace AJut.DateAndTime
{
    using System;

    // ===========[ DateTimeEditRules ]==========================================
    // How typed text and picker choices become a date and time value: what typed text comes to, the text a value is
    // shown as, and what a date or a time of day chosen in a picker does to the value already held. Every stack's
    // DateTimeEditIngressControl settles its value here, then only shows the result and hosts the pickers, so the same
    // text gets the same value on every stack, and anything else that reads a typed date gets it too.
    //
    // Typed text is settled in this order:
    //  1. Empty text is no value, which is fine unless empty is to be treated as invalid
    //  2. The resolver reads it: the one given, or DateTimeTextResolver.Default when there is none
    //  3. What it read is shaped to what is edited: a date only edit drops the time of day, and a time only edit keeps
    //     the date already held (today when there is none)
    /// <summary>
    /// The rules that turn typed text and picker choices into a date and time value, the same for every stack's
    /// DateTimeEditIngressControl and for anything else that reads a typed date
    /// </summary>
    public static class DateTimeEditRules
    {
        private const string kEmptyTextReason = "No date or time given";

        // ===========[ Public Interface Methods ]==============================
        /// <summary>
        /// Settles typed <paramref name="text"/>: whether it can be used (and if not, why not), and the value it comes to
        /// </summary>
        /// <param name="text">The text, as the user typed it</param>
        /// <param name="resolver">The resolver to read it with, or null for <see cref="DateTimeTextResolver.Default"/></param>
        /// <param name="context">What is edited, the value held, and the moment relative text counts from</param>
        /// <param name="treatEmptyAsInvalid">Whether empty text is invalid rather than just no value</param>
        public static Evaluation Evaluate (string? text, IDateTimeTextResolver? resolver, DateTimeTextContext context, bool treatEmptyAsInvalid)
        {
            if (String.IsNullOrWhiteSpace(text))
            {
                return treatEmptyAsInvalid ? Invalid(kEmptyTextReason) : new Evaluation(IsTextValid: true, Value: null, InvalidTextReason: null);
            }

            DateTimeTextResolution resolution = (resolver ?? DateTimeTextResolver.Default).Resolve(text, context);
            if (!resolution.IsResolved)
            {
                return Invalid(resolution.FailureReason ?? $"'{text.Trim()}' could not be read");
            }

            // A resolver can read text as asking for no value ("none"), which is the same as empty text
            if (resolution.Value == null)
            {
                return treatEmptyAsInvalid ? Invalid(kEmptyTextReason) : new Evaluation(IsTextValid: true, Value: null, InvalidTextReason: null);
            }

            return new Evaluation(IsTextValid: true, Value: ShapeToWhatIsEdited(resolution.Value.Value, context), InvalidTextReason: null);
        }

        /// <summary>The text <paramref name="value"/> is shown as, from <paramref name="resolver"/> or the default one</summary>
        public static string Format (DateTime? value, IDateTimeTextResolver? resolver, DateTimeTextContext context)
        {
            return (resolver ?? DateTimeTextResolver.Default).Format(value, context) ?? String.Empty;
        }

        /// <summary>
        /// The value after <paramref name="pickedDate"/> is chosen in a calendar: that date, at the time of day
        /// <paramref name="current"/> holds, or midnight when there is no value yet
        /// </summary>
        public static DateTime WithPickedDate (DateTime? current, DateTime pickedDate)
        {
            return pickedDate.Date + (current?.TimeOfDay ?? TimeSpan.Zero);
        }

        /// <summary>
        /// The value after <paramref name="pickedTimeOfDay"/> is chosen in a time picker: that time, on the date
        /// <paramref name="current"/> holds, or on <paramref name="now"/>'s date when there is no value yet
        /// </summary>
        public static DateTime WithPickedTimeOfDay (DateTime? current, TimeSpan pickedTimeOfDay, DateTime now)
        {
            return (current ?? now).Date + pickedTimeOfDay;
        }

        // ===========[ Helper Methods ]========================================
        private static Evaluation Invalid (string reason) => new Evaluation(IsTextValid: false, Value: null, InvalidTextReason: reason);

        private static DateTime ShapeToWhatIsEdited (DateTime resolved, DateTimeTextContext context)
        {
            if (!context.ShowsTime)
            {
                return resolved.Date;
            }

            if (!context.ShowsDate)
            {
                return WithPickedTimeOfDay(context.Current, resolved.TimeOfDay, context.Now);
            }

            return resolved;
        }

        // ===========[ Subclasses/structs ]====================================
        /// <summary>What <see cref="Evaluate"/> decided about typed text</summary>
        /// <param name="IsTextValid">Whether the text can be used: empty text counts, unless it's to be treated as invalid</param>
        /// <param name="Value">The value the text comes to, or null for none (and whenever the text can't be used)</param>
        /// <param name="InvalidTextReason">Why the text can't be used, or null when it can</param>
        public readonly record struct Evaluation (bool IsTextValid, DateTime? Value, string? InvalidTextReason);
    }
}
