namespace TheAJutShowRoom.UI.Controls
{
    using System;
    using System.Windows;
    using System.Windows.Controls;
    using AJut.DateAndTime;
    using AJut.UX.Controls;

    /// <summary>
    /// The DateTimeEditIngressControl demos: one driven by the check boxes above it, one with a resolver that reads
    /// quarters, and the calendar and time editor it opens, on their own.
    /// </summary>
    public partial class DateTimeEditIngressControlExample : UserControl
    {
        public DateTimeEditIngressControlExample ()
        {
            this.InitializeComponent();
            this.Main.EditDateTime = DateTime.Now;
            this.Quarters.TextResolver = new QuarterResolver();
        }

        // Each suggestion button shows the format it sets
        private void DisplayFormatSuggestion_OnClick (object sender, RoutedEventArgs e)
        {
            this.DisplayFormat.Text = (string)((Button)sender).Content;
        }

        /// <summary>
        /// Reads q1 to q4 (and "q1 2027") as the first day of that quarter, shows any value as its quarter, and leaves
        /// everything else typed to the default resolver
        /// </summary>
        private class QuarterResolver : IDateTimeTextResolver
        {
            private const int kMonthsPerQuarter = 3;

            public DateTimeTextResolution Resolve (string text, DateTimeTextContext context)
            {
                // q1 to q4, alone for this year, or with a year after it, which is how a value shows ("Q4 2026"), so the
                //  shown text can be edited and read back
                string[] words = text.Trim().ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if ((words.Length is 1 or 2) && (words[0] is "q1" or "q2" or "q3" or "q4"))
                {
                    int year = context.Now.Year;
                    if ((words.Length == 2)
                        && (!Int32.TryParse(words[1], out year) || (year < DateTime.MinValue.Year) || (year > DateTime.MaxValue.Year)))
                    {
                        return DateTimeTextResolution.Failed($"'{words[1]}' isn't a year");
                    }

                    int quarter = words[0][1] - '0';
                    return DateTimeTextResolution.Resolved(new DateTime(year, ((quarter - 1) * kMonthsPerQuarter) + 1, 1));
                }

                return DateTimeTextResolver.Default.Resolve(text, context);
            }

            public string Format (DateTime? value, DateTimeTextContext context)
            {
                return (value == null) ? String.Empty : $"Q{((value.Value.Month - 1) / kMonthsPerQuarter) + 1} {value.Value.Year}";
            }
        }
    }
}
