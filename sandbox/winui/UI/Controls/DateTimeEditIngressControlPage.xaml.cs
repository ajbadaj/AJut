namespace AJutShowRoomWinUI.UI.Controls
{
    using System;
    using AJut.UX.Controls;
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;

    /// <summary>
    /// The DateTimeEditIngressControl demos: one field to try the settings on, a date, a time, each clock, both in a format of its own, a resolver that reads quarters,
    /// empty treated as invalid, and read-only.
    /// </summary>
    public sealed partial class DateTimeEditIngressControlPage : Page
    {
        public DateTimeEditIngressControlPage ()
        {
            this.InitializeComponent();
            this.DateOnly.EditDateTime = DateTime.Today;
            this.TimeOnly.EditDateTime = DateTime.Now;
            this.DateAndTime.EditDateTime = DateTime.Now;
            this.TwentyFourHour.EditDateTime = DateTime.Now;
            this.TwelveHour.EditDateTime = DateTime.Now;
            this.ReadOnlyDate.EditDateTime = DateTime.Now;
            this.Quarters.TextResolver = new QuarterResolver();
        }

        // Each suggestion button shows the format it sets
        private void DisplayFormatSuggestion_OnClick (object sender, RoutedEventArgs e)
        {
            this.DisplayFormatBox.Text = (string)((Button)sender).Content;
        }

        public string DemoCode { get; } = @"<!-- A date, typed or picked from a calendar -->
<ajut:DateTimeEditIngressControl EditDateTime=""{Binding Due, Mode=TwoWay}"" UnsetTextPrompt=""No date""/>

<!-- A time alone -->
<ajut:DateTimeEditIngressControl EditDateTime=""{Binding Alarm, Mode=TwoWay}"" IncludeDate=""False"" IncludeTime=""True""/>

<!-- Both, on a twenty four hour clock whatever the culture (TwelveHour, or CurrentCulture, the default) -->
<ajut:DateTimeEditIngressControl EditDateTime=""{Binding Starts, Mode=TwoWay}"" IncludeTime=""True"" TimeFormat=""TwentyFourHour""/>

<!-- Both, shown in a format of its own -->
<ajut:DateTimeEditIngressControl EditDateTime=""{Binding Starts, Mode=TwoWay}"" IncludeTime=""True""
    DisplayFormat=""ddd, MMM d yyyy 'at' h:mm tt""/>

<!-- A resolver of its own, an IDateTimeTextResolver, reads the text and writes the value -->
<ajut:DateTimeEditIngressControl EditDateTime=""{Binding Quarter, Mode=TwoWay}"" TextResolver=""{Binding QuarterResolver}""/>";

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
