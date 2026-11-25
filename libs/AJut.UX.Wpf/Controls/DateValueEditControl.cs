namespace AJut.UX.Controls
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Controls.Primitives;
    using System.Windows.Input;
    using System.Windows.Media;
    using DPUtils = DPUtils<DateValueEditControl>;

    // ===========[ DateValueEditControl ]=======================================
    // A month calendar to pick a date from, the panel DateTimeEditIngressControl opens for its date. WPF only: the
    // other stacks host their toolkit's own calendar. It shows six weeks around DisplayMonth, laid out by
    // CalendarMonthGrid in AJut.UX, with buttons to step a month either way and one to come back to today's month.
    // The month's name opens a menu of every month, and the year opens a year picker: a NumericEditor to type one, over
    // a list of every year a DateTime holds to pick from, with a check to show the year chosen and an X to leave it. A
    // year double clicked in the list is chosen and shown at once, as picking it and pressing the check does.
    // Neither picks a date, they only move the calendar. The list is a plain virtualizing ListBox, so only the rows on
    // screen are ever made, out of nearly ten thousand.
    // Picking a day sets EditDate; the time of day on EditDate is never looked at or kept, that is the ingress's job.
    /// <summary>A month calendar that picks a date, the panel DateTimeEditIngressControl opens for its date</summary>
    [TemplatePart(Name = nameof(PART_YearPopup), Type = typeof(Popup))]
    [TemplatePart(Name = nameof(PART_YearList), Type = typeof(ListBox))]
    public class DateValueEditControl : Control
    {
        /// <summary>Shows the month before <see cref="DisplayMonth"/></summary>
        public static readonly RoutedCommand ShowPreviousMonthCommand = new RoutedCommand(nameof(ShowPreviousMonthCommand), typeof(DateValueEditControl));

        /// <summary>Shows the month after <see cref="DisplayMonth"/></summary>
        public static readonly RoutedCommand ShowNextMonthCommand = new RoutedCommand(nameof(ShowNextMonthCommand), typeof(DateValueEditControl));

        /// <summary>Shows the month today is in, without picking anything</summary>
        public static readonly RoutedCommand ShowTodayCommand = new RoutedCommand(nameof(ShowTodayCommand), typeof(DateValueEditControl));

        /// <summary>Picks the <see cref="Day"/> given as the command's parameter</summary>
        public static readonly RoutedCommand PickDayCommand = new RoutedCommand(nameof(PickDayCommand), typeof(DateValueEditControl));

        /// <summary>Shows the month (1 to 12, an int) given as the command's parameter, in the year shown</summary>
        public static readonly RoutedCommand ShowMonthCommand = new RoutedCommand(nameof(ShowMonthCommand), typeof(DateValueEditControl));

        /// <summary>Shows <see cref="YearPickerYear"/>, in the month shown, and closes the year picker</summary>
        public static readonly RoutedCommand ApplyYearPickCommand = new RoutedCommand(nameof(ApplyYearPickCommand), typeof(DateValueEditControl));

        /// <summary>Closes the year picker, leaving the year shown as it was</summary>
        public static readonly RoutedCommand CancelYearPickCommand = new RoutedCommand(nameof(CancelYearPickCommand), typeof(DateValueEditControl));

        /// <summary>Every year a DateTime holds, 1 to 9999, which the year picker lists</summary>
        public static readonly IReadOnlyList<int> kPickableYears = BuildPickableYears();

        // ===========[ Setup/Construction/Teardown ]===========================
        static DateValueEditControl ()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(DateValueEditControl), new FrameworkPropertyMetadata(typeof(DateValueEditControl)));
        }

        public DateValueEditControl ()
        {
            this.CommandBindings.Add(new CommandBinding(ShowPreviousMonthCommand, this.OnShowPreviousMonthExecuted));
            this.CommandBindings.Add(new CommandBinding(ShowNextMonthCommand, this.OnShowNextMonthExecuted));
            this.CommandBindings.Add(new CommandBinding(ShowTodayCommand, this.OnShowTodayExecuted));
            this.CommandBindings.Add(new CommandBinding(PickDayCommand, this.OnPickDayExecuted, this.OnCanPickDay));
            this.CommandBindings.Add(new CommandBinding(ShowMonthCommand, this.OnShowMonthExecuted, this.OnCanShowMonth));
            this.CommandBindings.Add(new CommandBinding(ApplyYearPickCommand, this.OnApplyYearPickExecuted));
            this.CommandBindings.Add(new CommandBinding(CancelYearPickCommand, this.OnCancelYearPickExecuted));
            this.MonthNames = CalendarMonthGrid.GetMonthNames(CultureInfo.CurrentCulture);
            // Setting the month works out the days
            this.SetCurrentValue(DisplayMonthProperty, CalendarMonthGrid.ClampDisplayMonth(DateTime.Today));
        }

        public override void OnApplyTemplate ()
        {
            base.OnApplyTemplate();

            // Named handlers, so a template applied again takes the old parts' handlers off them
            if (this.PART_YearPopup != null)
            {
                this.PART_YearPopup.Opened -= this.YearPopup_OnOpened;
            }

            if (this.PART_YearList != null)
            {
                this.PART_YearList.MouseDoubleClick -= this.YearList_OnMouseDoubleClick;
            }

            this.PART_YearPopup = this.GetTemplateChild(nameof(PART_YearPopup)) as Popup;
            this.PART_YearList = this.GetTemplateChild(nameof(PART_YearList)) as ListBox;
            if (this.PART_YearPopup != null)
            {
                this.PART_YearPopup.Opened += this.YearPopup_OnOpened;
            }

            if (this.PART_YearList != null)
            {
                this.PART_YearList.MouseDoubleClick += this.YearList_OnMouseDoubleClick;
            }
        }

        // ===========[ Events ]================================================
        /// <summary>Raised whenever <see cref="EditDate"/> changes, by a pick or from outside</summary>
        public event EventHandler EditDateChanged;

        // ===========[ Dependency Properties ]=================================
        public static readonly DependencyProperty EditDateProperty = DPUtils.RegisterFP(_ => _.EditDate, (d, e) => d.OnEditDateChanged(), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault);
        /// <summary>The date picked, or null for none. Setting it shows its month.</summary>
        public DateTime? EditDate
        {
            get => (DateTime?)this.GetValue(EditDateProperty);
            set => this.SetValue(EditDateProperty, value);
        }

        public static readonly DependencyProperty DisplayMonthProperty = DPUtils.Register(_ => _.DisplayMonth, (d, e) => d.RebuildDays());
        /// <summary>The month shown: any day in it. Stepping the months moves this and leaves <see cref="EditDate"/> alone.</summary>
        public DateTime DisplayMonth
        {
            get => (DateTime)this.GetValue(DisplayMonthProperty);
            set => this.SetValue(DisplayMonthProperty, value);
        }

        public static readonly DependencyProperty DisplayYearProperty = DPUtils.RegisterFP(_ => _.DisplayYear, (d, e) => d.OnDisplayYearChanged(), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault);
        /// <summary>
        /// The year shown, which follows <see cref="DisplayMonth"/>. Setting it shows the same month in that year, as typing
        /// one in the year picker does.
        /// </summary>
        public int DisplayYear
        {
            get => (int)this.GetValue(DisplayYearProperty);
            set => this.SetValue(DisplayYearProperty, value);
        }

        public static readonly DependencyProperty IsYearPickerOpenProperty = DPUtils.Register(_ => _.IsYearPickerOpen, (d, e) => d.OnIsYearPickerOpenChanged());
        /// <summary>
        /// Whether the year picker, opened from the year over the days, is open. Opening it starts
        /// <see cref="YearPickerYear"/> on the year shown.
        /// </summary>
        public bool IsYearPickerOpen
        {
            get => (bool)this.GetValue(IsYearPickerOpenProperty);
            set => this.SetValue(IsYearPickerOpenProperty, value);
        }

        public static readonly DependencyProperty YearPickerYearProperty = DPUtils.RegisterFP(_ => _.YearPickerYear, (d, e) => d.OnYearPickerYearChanged(), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault);
        /// <summary>
        /// The year chosen in the year picker, typed or picked from its list, which <see cref="ApplyYearPickCommand"/>
        /// shows
        /// </summary>
        public int YearPickerYear
        {
            get => (int)this.GetValue(YearPickerYearProperty);
            set => this.SetValue(YearPickerYearProperty, value);
        }

        public static readonly DependencyProperty IsReadOnlyProperty = DPUtils.Register(_ => _.IsReadOnly, (d, e) => CommandManager.InvalidateRequerySuggested());
        /// <summary>Shows the date without letting another be picked. The months can still be stepped through.</summary>
        public bool IsReadOnly
        {
            get => (bool)this.GetValue(IsReadOnlyProperty);
            set => this.SetValue(IsReadOnlyProperty, value);
        }

        private static readonly DependencyPropertyKey DaysPropertyKey = DPUtils.RegisterReadOnly(_ => _.Days);
        public static readonly DependencyProperty DaysProperty = DaysPropertyKey.DependencyProperty;
        /// <summary>The days shown, six weeks of them in order, for the template's grid of day buttons</summary>
        public IReadOnlyList<Day> Days
        {
            get => (IReadOnlyList<Day>)this.GetValue(DaysProperty);
            private set => this.SetValue(DaysPropertyKey, value);
        }

        private static readonly DependencyPropertyKey DayNamesPropertyKey = DPUtils.RegisterReadOnly(_ => _.DayNames);
        public static readonly DependencyProperty DayNamesProperty = DayNamesPropertyKey.DependencyProperty;
        /// <summary>The names over the day columns, starting on the culture's first day of the week</summary>
        public IReadOnlyList<string> DayNames
        {
            get => (IReadOnlyList<string>)this.GetValue(DayNamesProperty);
            private set => this.SetValue(DayNamesPropertyKey, value);
        }

        private static readonly DependencyPropertyKey MonthNamesPropertyKey = DPUtils.RegisterReadOnly(_ => _.MonthNames);
        public static readonly DependencyProperty MonthNamesProperty = MonthNamesPropertyKey.DependencyProperty;
        /// <summary>The twelve month names, January first, as the culture writes them, for the month menu</summary>
        public IReadOnlyList<string> MonthNames
        {
            get => (IReadOnlyList<string>)this.GetValue(MonthNamesProperty);
            private set => this.SetValue(MonthNamesPropertyKey, value);
        }

        private static readonly DependencyPropertyKey DisplayMonthNamePropertyKey = DPUtils.RegisterReadOnly(_ => _.DisplayMonthName);
        public static readonly DependencyProperty DisplayMonthNameProperty = DisplayMonthNamePropertyKey.DependencyProperty;
        /// <summary>The name of the month shown ("November"), over the days</summary>
        public string DisplayMonthName
        {
            get => (string)this.GetValue(DisplayMonthNameProperty);
            private set => this.SetValue(DisplayMonthNamePropertyKey, value);
        }

        public static readonly DependencyProperty SelectedDayBackgroundProperty = DPUtils.Register(_ => _.SelectedDayBackground);
        /// <summary>Behind the picked day</summary>
        public Brush SelectedDayBackground
        {
            get => (Brush)this.GetValue(SelectedDayBackgroundProperty);
            set => this.SetValue(SelectedDayBackgroundProperty, value);
        }

        public static readonly DependencyProperty SelectedDayForegroundProperty = DPUtils.Register(_ => _.SelectedDayForeground);
        /// <summary>The picked day's number</summary>
        public Brush SelectedDayForeground
        {
            get => (Brush)this.GetValue(SelectedDayForegroundProperty);
            set => this.SetValue(SelectedDayForegroundProperty, value);
        }

        public static readonly DependencyProperty TodayBorderBrushProperty = DPUtils.Register(_ => _.TodayBorderBrush);
        /// <summary>The ring around today</summary>
        public Brush TodayBorderBrush
        {
            get => (Brush)this.GetValue(TodayBorderBrushProperty);
            set => this.SetValue(TodayBorderBrushProperty, value);
        }

        public static readonly DependencyProperty PopupBackgroundProperty = DPUtils.Register(_ => _.PopupBackground);
        /// <summary>Behind the month menu and the year picker</summary>
        public Brush PopupBackground
        {
            get => (Brush)this.GetValue(PopupBackgroundProperty);
            set => this.SetValue(PopupBackgroundProperty, value);
        }

        public static readonly DependencyProperty HoverBackgroundProperty = DPUtils.Register(_ => _.HoverBackground);
        /// <summary>Behind a day, or one of the buttons over the days, while the pointer is over it</summary>
        public Brush HoverBackground
        {
            get => (Brush)this.GetValue(HoverBackgroundProperty);
            set => this.SetValue(HoverBackgroundProperty, value);
        }

        public static readonly DependencyProperty HoverForegroundProperty = DPUtils.Register(_ => _.HoverForeground);
        /// <summary>A day's number, or a button's glyph, while the pointer is over it</summary>
        public Brush HoverForeground
        {
            get => (Brush)this.GetValue(HoverForegroundProperty);
            set => this.SetValue(HoverForegroundProperty, value);
        }

        // ===========[ Properties ]============================================
        private Popup PART_YearPopup { get; set; }
        private ListBox PART_YearList { get; set; }

        // ===========[ Helper Methods ]========================================
        private void OnShowPreviousMonthExecuted (object sender, ExecutedRoutedEventArgs e)
        {
            this.DisplayMonth = CalendarMonthGrid.MoveDisplayMonth(this.DisplayMonth, -1);
        }

        private void OnShowNextMonthExecuted (object sender, ExecutedRoutedEventArgs e)
        {
            this.DisplayMonth = CalendarMonthGrid.MoveDisplayMonth(this.DisplayMonth, 1);
        }

        private void OnShowTodayExecuted (object sender, ExecutedRoutedEventArgs e)
        {
            this.DisplayMonth = CalendarMonthGrid.ClampDisplayMonth(DateTime.Today);
        }

        private void OnCanShowMonth (object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = e.Parameter is int;
        }

        private void OnShowMonthExecuted (object sender, ExecutedRoutedEventArgs e)
        {
            if (e.Parameter is int monthOfYear)
            {
                this.DisplayMonth = CalendarMonthGrid.GetDisplayMonth(this.DisplayMonth.Year, monthOfYear);
            }
        }

        // ------ Year picker
        private void OnApplyYearPickExecuted (object sender, ExecutedRoutedEventArgs e) => this.ApplyYearPick();

        // A double click on a year is that year picked and the check pressed. One on the list's scroll bar or empty space
        //  picks nothing.
        private void YearList_OnMouseDoubleClick (object sender, MouseButtonEventArgs e)
        {
            ListBoxItem row = (e.OriginalSource as DependencyObject)?.GetFirstParentOf<ListBoxItem>();
            if (row?.Content is int year)
            {
                this.PickYearFromList(year);
                e.Handled = true;
            }
        }

        /// <summary>A year double clicked in the year picker's list: it is chosen, shown, and the picker closes</summary>
        internal void PickYearFromList (int year)
        {
            this.YearPickerYear = year;
            this.ApplyYearPick();
        }

        private void ApplyYearPick ()
        {
            this.DisplayYear = this.YearPickerYear;
            this.IsYearPickerOpen = false;
        }

        private void OnCancelYearPickExecuted (object sender, ExecutedRoutedEventArgs e)
        {
            this.IsYearPickerOpen = false;
        }

        private void OnIsYearPickerOpenChanged ()
        {
            if (this.IsYearPickerOpen)
            {
                this.YearPickerYear = this.DisplayYear;
            }
        }

        // A year typed in the year picker brings that year into view in its list
        private void OnYearPickerYearChanged ()
        {
            if (this.IsYearPickerOpen)
            {
                this.PART_YearList?.ScrollIntoView(this.YearPickerYear);
            }
        }

        // The popup's content is laid out by the time it says it opened, so the list knows how many rows it shows and
        //  the year chosen can be put in the middle of them. A virtualizing ListBox scrolls by item, so its offset is a
        //  row index and its viewport a count of rows.
        private void YearPopup_OnOpened (object sender, EventArgs e)
        {
            if (this.PART_YearList == null)
            {
                return;
            }

            this.PART_YearList.UpdateLayout();
            ScrollViewer scroller = this.PART_YearList.GetFirstChildOf<ScrollViewer>();
            int index = this.YearPickerYear - kPickableYears[0];
            if (scroller != null)
            {
                scroller.ScrollToVerticalOffset(Math.Max(0.0, index - (scroller.ViewportHeight / 2.0)));
            }
        }

        private static IReadOnlyList<int> BuildPickableYears ()
        {
            int count = DateTime.MaxValue.Year - DateTime.MinValue.Year + 1;
            var years = new int[count];
            for (int index = 0; index < count; ++index)
            {
                years[index] = DateTime.MinValue.Year + index;
            }

            return years;
        }

        private void OnDisplayYearChanged ()
        {
            // The year can be set past what a calendar shows (a year typed as 0, or 10000), and the month shown is brought
            //  to the nearest there is. RebuildDays then sets the year back to the one actually shown.
            DateTime shown = CalendarMonthGrid.GetDisplayMonth(this.DisplayYear, this.DisplayMonth.Month);
            if (shown != this.DisplayMonth)
            {
                this.DisplayMonth = shown;
            }
            else if (shown.Year != this.DisplayYear)
            {
                this.SetCurrentValue(DisplayYearProperty, shown.Year);
            }
        }

        private void OnCanPickDay (object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = !this.IsReadOnly && (e.Parameter is Day);
        }

        private void OnPickDayExecuted (object sender, ExecutedRoutedEventArgs e)
        {
            if (!this.IsReadOnly && (e.Parameter is Day day))
            {
                this.EditDate = day.Date;
            }
        }

        private void OnEditDateChanged ()
        {
            // The picked date's month comes into view. A day picked from the rows of the month before or after moves the
            //  calendar on to that month, as the stock calendars do.
            if (this.EditDate != null)
            {
                this.DisplayMonth = CalendarMonthGrid.ClampDisplayMonth(this.EditDate.Value);
            }

            this.RebuildDays();
            this.EditDateChanged?.Invoke(this, EventArgs.Empty);
        }

        // Works out the days, their names and the title again. The days are replaced whole: there are only six weeks of
        //  them, and each one is fixed once made.
        private void RebuildDays ()
        {
            CultureInfo culture = CultureInfo.CurrentCulture;
            DayOfWeek firstDayOfWeek = culture.DateTimeFormat.FirstDayOfWeek;
            DateTime month = CalendarMonthGrid.ClampDisplayMonth(this.DisplayMonth);
            DateTime today = DateTime.Today;
            DateTime? picked = this.EditDate?.Date;

            DateTime[] dates = CalendarMonthGrid.GetDays(month, firstDayOfWeek);
            var days = new Day[dates.Length];
            for (int index = 0; index < dates.Length; ++index)
            {
                DateTime date = dates[index];
                days[index] = new Day(date, isInDisplayMonth: date.Month == month.Month, isToday: date == today, isPicked: date == picked);
            }

            this.Days = days;
            this.DayNames = CalendarMonthGrid.GetDayNames(culture, firstDayOfWeek);
            this.DisplayMonthName = culture.DateTimeFormat.GetMonthName(month.Month);
            if (this.DisplayYear != month.Year)
            {
                this.SetCurrentValue(DisplayYearProperty, month.Year);
            }
        }

        // ===========[ Subclasses/structs ]====================================
        /// <summary>One day on the calendar, as the template's day button shows it</summary>
        public class Day
        {
            internal Day (DateTime date, bool isInDisplayMonth, bool isToday, bool isPicked)
            {
                this.Date = date;
                this.IsInDisplayMonth = isInDisplayMonth;
                this.IsToday = isToday;
                this.IsPicked = isPicked;
            }

            /// <summary>The day, at midnight</summary>
            public DateTime Date { get; }

            /// <summary>The day of the month, which is what the button shows</summary>
            public int DayOfMonth => this.Date.Day;

            /// <summary>False for the days of the months before and after that fill out the six weeks, which are drawn fainter</summary>
            public bool IsInDisplayMonth { get; }

            /// <summary>Whether this is today</summary>
            public bool IsToday { get; }

            /// <summary>Whether this is the control's <see cref="DateValueEditControl.EditDate"/></summary>
            public bool IsPicked { get; }
        }
    }
}
