namespace AJut.UX.Controls
{
    using System;
    using System.Globalization;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Input;
    using System.Windows.Media;
    using AJut.DateAndTime;
    using DPUtils = DPUtils<TimeValueEditControl>;

    // ===========[ TimeValueEditControl ]=======================================
    // An hour and a minute, each with a button above and below to step it, and the half of the day beside them on a
    // twelve hour clock: the panel DateTimeEditIngressControl opens for its time. WPF only: the other stacks host their
    // toolkit's own time picker. Where a step lands, and what a typed hour or minute comes to, is TimeOfDayRules in
    // AJut.UX: the hour goes round inside the day, and the minute carries into the hour as a clock does, or goes round
    // inside it (MinuteStepCarriesIntoHour). The buttons repeat while held.
    //
    // The hour and the minute are text boxes, so either can be typed over: Enter or focus leaving takes it, Escape puts
    // it back, and text that isn't an hour or a minute goes back to what was there. Up and Down step the one being typed
    // in. Steps take NumericEditor's modifier tiers: Ctrl held steps big (BigHourIncrement, BigMinuteIncrement), and
    // Shift held steps the minute small (SmallMinuteIncrement), on the buttons and the arrow keys alike.
    /// <summary>An hour and minute, typed or stepped with buttons, the panel DateTimeEditIngressControl opens for its time</summary>
    [TemplatePart(Name = nameof(PART_HourTextBox), Type = typeof(TextBox))]
    [TemplatePart(Name = nameof(PART_MinuteTextBox), Type = typeof(TextBox))]
    public class TimeValueEditControl : Control
    {
        /// <summary>Steps the hour up one, or by <see cref="BigHourIncrement"/> with Ctrl held</summary>
        public static readonly RoutedCommand IncreaseHourCommand = new RoutedCommand(nameof(IncreaseHourCommand), typeof(TimeValueEditControl));

        /// <summary>Steps the hour down one, or by <see cref="BigHourIncrement"/> with Ctrl held</summary>
        public static readonly RoutedCommand DecreaseHourCommand = new RoutedCommand(nameof(DecreaseHourCommand), typeof(TimeValueEditControl));

        /// <summary>
        /// Steps the minute up by <see cref="MinuteIncrement"/>, by <see cref="BigMinuteIncrement"/> with Ctrl held, or by
        /// <see cref="SmallMinuteIncrement"/> with Shift held
        /// </summary>
        public static readonly RoutedCommand IncreaseMinuteCommand = new RoutedCommand(nameof(IncreaseMinuteCommand), typeof(TimeValueEditControl));

        /// <summary>
        /// Steps the minute down by <see cref="MinuteIncrement"/>, by <see cref="BigMinuteIncrement"/> with Ctrl held, or by
        /// <see cref="SmallMinuteIncrement"/> with Shift held
        /// </summary>
        public static readonly RoutedCommand DecreaseMinuteCommand = new RoutedCommand(nameof(DecreaseMinuteCommand), typeof(TimeValueEditControl));

        /// <summary>Moves the time to the other half of the day, morning to afternoon or back</summary>
        public static readonly RoutedCommand ToggleHalfOfDayCommand = new RoutedCommand(nameof(ToggleHalfOfDayCommand), typeof(TimeValueEditControl));

        // ===========[ Setup/Construction/Teardown ]===========================
        static TimeValueEditControl ()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(TimeValueEditControl), new FrameworkPropertyMetadata(typeof(TimeValueEditControl)));
        }

        public TimeValueEditControl ()
        {
            this.CommandBindings.Add(new CommandBinding(IncreaseHourCommand, this.OnIncreaseHourExecuted, this.OnCanEdit));
            this.CommandBindings.Add(new CommandBinding(DecreaseHourCommand, this.OnDecreaseHourExecuted, this.OnCanEdit));
            this.CommandBindings.Add(new CommandBinding(IncreaseMinuteCommand, this.OnIncreaseMinuteExecuted, this.OnCanEdit));
            this.CommandBindings.Add(new CommandBinding(DecreaseMinuteCommand, this.OnDecreaseMinuteExecuted, this.OnCanEdit));
            this.CommandBindings.Add(new CommandBinding(ToggleHalfOfDayCommand, this.OnToggleHalfOfDayExecuted, this.OnCanEdit));

            // The culture's own clock, until TimeFormat says otherwise. Working it out works out the texts shown.
            this.OnTimeFormatChanged();
        }

        public override void OnApplyTemplate ()
        {
            base.OnApplyTemplate();

            // Named handlers, so a template applied again takes the old parts' handlers off them
            if (this.PART_HourTextBox != null)
            {
                this.PART_HourTextBox.PreviewKeyDown -= this.HourTextBox_OnPreviewKeyDown;
                this.PART_HourTextBox.LostKeyboardFocus -= this.HourTextBox_OnLostKeyboardFocus;
            }

            if (this.PART_MinuteTextBox != null)
            {
                this.PART_MinuteTextBox.PreviewKeyDown -= this.MinuteTextBox_OnPreviewKeyDown;
                this.PART_MinuteTextBox.LostKeyboardFocus -= this.MinuteTextBox_OnLostKeyboardFocus;
            }

            this.PART_HourTextBox = this.GetTemplateChild(nameof(PART_HourTextBox)) as TextBox;
            this.PART_MinuteTextBox = this.GetTemplateChild(nameof(PART_MinuteTextBox)) as TextBox;

            if (this.PART_HourTextBox != null)
            {
                this.PART_HourTextBox.PreviewKeyDown += this.HourTextBox_OnPreviewKeyDown;
                this.PART_HourTextBox.LostKeyboardFocus += this.HourTextBox_OnLostKeyboardFocus;
            }

            if (this.PART_MinuteTextBox != null)
            {
                this.PART_MinuteTextBox.PreviewKeyDown += this.MinuteTextBox_OnPreviewKeyDown;
                this.PART_MinuteTextBox.LostKeyboardFocus += this.MinuteTextBox_OnLostKeyboardFocus;
            }
        }

        // ===========[ Events ]================================================
        /// <summary>Raised whenever <see cref="EditTime"/> changes, by a step or from outside</summary>
        public event EventHandler EditTimeChanged;

        // ===========[ Dependency Properties ]=================================
        public static readonly DependencyProperty EditTimeProperty = DPUtils.RegisterFP(_ => _.EditTime, (d, e) => d.OnEditTimeChanged(), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault);
        /// <summary>The time of day, counted from midnight</summary>
        public TimeSpan EditTime
        {
            get => (TimeSpan)this.GetValue(EditTimeProperty);
            set => this.SetValue(EditTimeProperty, value);
        }

        public static readonly DependencyProperty TimeFormatProperty = DPUtils.Register(_ => _.TimeFormat, (d, e) => d.OnTimeFormatChanged());
        /// <summary>
        /// The clock the time is shown on: the culture's own (the default), twelve hour with the half of the day beside it,
        /// or twenty four hour with none
        /// </summary>
        public eTimeFormat TimeFormat
        {
            get => (eTimeFormat)this.GetValue(TimeFormatProperty);
            set => this.SetValue(TimeFormatProperty, value);
        }

        private static readonly DependencyPropertyKey UsesTwelveHourClockPropertyKey = DPUtils.RegisterReadOnly(_ => _.UsesTwelveHourClock, (d, e) => d.RefreshTexts());
        public static readonly DependencyProperty UsesTwelveHourClockProperty = UsesTwelveHourClockPropertyKey.DependencyProperty;
        /// <summary>
        /// Whether the hour reads 12 and 1 to 11 with the half of the day beside it, rather than 0 to 23, by
        /// <see cref="TimeFormat"/> and the current culture
        /// </summary>
        public bool UsesTwelveHourClock
        {
            get => (bool)this.GetValue(UsesTwelveHourClockProperty);
            private set => this.SetValue(UsesTwelveHourClockPropertyKey, value);
        }

        public static readonly DependencyProperty MinuteIncrementProperty = DPUtils.Register(_ => _.MinuteIncrement, 1);
        /// <summary>How many minutes one step of the minute moves it</summary>
        public int MinuteIncrement
        {
            get => (int)this.GetValue(MinuteIncrementProperty);
            set => this.SetValue(MinuteIncrementProperty, value);
        }

        public static readonly DependencyProperty MinuteStepCarriesIntoHourProperty = DPUtils.Register(_ => _.MinuteStepCarriesIntoHour, true);
        /// <summary>
        /// Whether stepping the minute past either end of the hour moves the hour, as a clock does (10:59 steps up to
        /// 11:00), which it does unless set otherwise, or goes round inside the hour (10:59 steps up to 10:00)
        /// </summary>
        public bool MinuteStepCarriesIntoHour
        {
            get => (bool)this.GetValue(MinuteStepCarriesIntoHourProperty);
            set => this.SetValue(MinuteStepCarriesIntoHourProperty, value);
        }

        public static readonly DependencyProperty BigMinuteIncrementProperty = DPUtils.Register(_ => _.BigMinuteIncrement, 15);
        /// <summary>How many minutes one step of the minute moves it with Ctrl held, a quarter hour unless set otherwise</summary>
        public int BigMinuteIncrement
        {
            get => (int)this.GetValue(BigMinuteIncrementProperty);
            set => this.SetValue(BigMinuteIncrementProperty, value);
        }

        public static readonly DependencyProperty SmallMinuteIncrementProperty = DPUtils.Register(_ => _.SmallMinuteIncrement, 1);
        /// <summary>How many minutes one step of the minute moves it with Shift held</summary>
        public int SmallMinuteIncrement
        {
            get => (int)this.GetValue(SmallMinuteIncrementProperty);
            set => this.SetValue(SmallMinuteIncrementProperty, value);
        }

        public static readonly DependencyProperty BigHourIncrementProperty = DPUtils.Register(_ => _.BigHourIncrement, 6);
        /// <summary>How many hours one step of the hour moves it with Ctrl held</summary>
        public int BigHourIncrement
        {
            get => (int)this.GetValue(BigHourIncrementProperty);
            set => this.SetValue(BigHourIncrementProperty, value);
        }

        public static readonly DependencyProperty IsReadOnlyProperty = DPUtils.Register(_ => _.IsReadOnly, (d, e) => CommandManager.InvalidateRequerySuggested());
        /// <summary>Shows the time without letting it change: every button is off, and the hour and minute take no typing</summary>
        public bool IsReadOnly
        {
            get => (bool)this.GetValue(IsReadOnlyProperty);
            set => this.SetValue(IsReadOnlyProperty, value);
        }

        private static readonly DependencyPropertyKey HourTextPropertyKey = DPUtils.RegisterReadOnly(_ => _.HourText);
        public static readonly DependencyProperty HourTextProperty = HourTextPropertyKey.DependencyProperty;
        /// <summary>The hour as the clock shows it</summary>
        public string HourText
        {
            get => (string)this.GetValue(HourTextProperty);
            private set => this.SetValue(HourTextPropertyKey, value);
        }

        private static readonly DependencyPropertyKey MinuteTextPropertyKey = DPUtils.RegisterReadOnly(_ => _.MinuteText);
        public static readonly DependencyProperty MinuteTextProperty = MinuteTextPropertyKey.DependencyProperty;
        /// <summary>The minute, always two digits</summary>
        public string MinuteText
        {
            get => (string)this.GetValue(MinuteTextProperty);
            private set => this.SetValue(MinuteTextPropertyKey, value);
        }

        private static readonly DependencyPropertyKey HalfOfDayTextPropertyKey = DPUtils.RegisterReadOnly(_ => _.HalfOfDayText);
        public static readonly DependencyProperty HalfOfDayTextProperty = HalfOfDayTextPropertyKey.DependencyProperty;
        /// <summary>The culture's name for the half of the day the time is in ("AM", "PM")</summary>
        public string HalfOfDayText
        {
            get => (string)this.GetValue(HalfOfDayTextProperty);
            private set => this.SetValue(HalfOfDayTextPropertyKey, value);
        }

        public static readonly DependencyProperty HoverBackgroundProperty = DPUtils.Register(_ => _.HoverBackground);
        /// <summary>Behind a button while the pointer is over it</summary>
        public Brush HoverBackground
        {
            get => (Brush)this.GetValue(HoverBackgroundProperty);
            set => this.SetValue(HoverBackgroundProperty, value);
        }

        public static readonly DependencyProperty HoverForegroundProperty = DPUtils.Register(_ => _.HoverForeground);
        /// <summary>A button's glyph or text while the pointer is over it</summary>
        public Brush HoverForeground
        {
            get => (Brush)this.GetValue(HoverForegroundProperty);
            set => this.SetValue(HoverForegroundProperty, value);
        }

        // ===========[ Properties ]============================================
        private TextBox PART_HourTextBox { get; set; }
        private TextBox PART_MinuteTextBox { get; set; }

        // ===========[ Helper Methods ]========================================
        // ------ Steps
        private void OnCanEdit (object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = !this.IsReadOnly;
        }

        private void OnIncreaseHourExecuted (object sender, ExecutedRoutedEventArgs e) => this.StepHour(1);
        private void OnDecreaseHourExecuted (object sender, ExecutedRoutedEventArgs e) => this.StepHour(-1);
        private void OnIncreaseMinuteExecuted (object sender, ExecutedRoutedEventArgs e) => this.StepMinute(1);
        private void OnDecreaseMinuteExecuted (object sender, ExecutedRoutedEventArgs e) => this.StepMinute(-1);

        // A step in the direction given, sized by the modifier keys held, as NumericEditor sizes its nudges
        private void StepHour (int direction)
        {
            int hours = Keyboard.Modifiers.HasFlag(ModifierKeys.Control) ? this.BigHourIncrement : 1;
            this.EditTime = TimeOfDayRules.StepHour(this.EditTime, direction * hours);
        }

        private void StepMinute (int direction)
        {
            int increment = this.MinuteIncrement;
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                increment = this.BigMinuteIncrement;
            }
            else if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            {
                increment = this.SmallMinuteIncrement;
            }

            this.EditTime = TimeOfDayRules.StepMinute(this.EditTime, direction, increment, this.MinuteStepCarriesIntoHour);
        }

        // ------ Typed hour and minute
        private void HourTextBox_OnPreviewKeyDown (object sender, KeyEventArgs e)
        {
            this.HandleFieldKey(e, this.CommitHourText, this.StepHour);
        }

        private void MinuteTextBox_OnPreviewKeyDown (object sender, KeyEventArgs e)
        {
            this.HandleFieldKey(e, this.CommitMinuteText, this.StepMinute);
        }

        private void HourTextBox_OnLostKeyboardFocus (object sender, KeyboardFocusChangedEventArgs e)
        {
            this.CommitHourText();
        }

        private void MinuteTextBox_OnLostKeyboardFocus (object sender, KeyboardFocusChangedEventArgs e)
        {
            this.CommitMinuteText();
        }

        // Enter takes the typed text and Escape puts it back. Up and Down take what is typed first, then step from it.
        private void HandleFieldKey (KeyEventArgs e, Action commit, Action<int> step)
        {
            if (this.IsReadOnly)
            {
                return;
            }

            switch (e.Key)
            {
                case Key.Enter:
                    commit();
                    e.Handled = true;
                    break;

                case Key.Escape:
                    this.ShowTimeInFields();
                    e.Handled = true;
                    break;

                case Key.Up:
                    commit();
                    step(1);
                    e.Handled = true;
                    break;

                case Key.Down:
                    commit();
                    step(-1);
                    e.Handled = true;
                    break;
            }
        }

        /// <summary>Makes the hour typed the time's hour, or puts the hour back when what is typed isn't one</summary>
        internal void CommitHourText ()
        {
            if (!this.IsReadOnly
                && (this.PART_HourTextBox != null)
                && TimeOfDayRules.TryReadHour(this.PART_HourTextBox.Text, this.EditTime, this.UsesTwelveHourClock, out TimeSpan read))
            {
                this.EditTime = read;
            }

            this.ShowTimeInFields();
        }

        /// <summary>Makes the minute typed the time's minute, or puts the minute back when what is typed isn't one</summary>
        internal void CommitMinuteText ()
        {
            if (!this.IsReadOnly
                && (this.PART_MinuteTextBox != null)
                && TimeOfDayRules.TryReadMinute(this.PART_MinuteTextBox.Text, this.EditTime, out TimeSpan read))
            {
                this.EditTime = read;
            }

            this.ShowTimeInFields();
        }

        // The fields' text is bound to HourText and MinuteText, but typing replaces it, and a commit that changes nothing
        //  (text that isn't an hour, or "09" over "9") leaves those as they were and raises no change to put the text
        //  back. Pushing the bindings again does.
        private void ShowTimeInFields ()
        {
            this.PART_HourTextBox?.GetBindingExpression(TextBox.TextProperty)?.UpdateTarget();
            this.PART_MinuteTextBox?.GetBindingExpression(TextBox.TextProperty)?.UpdateTarget();
        }

        private void OnTimeFormatChanged ()
        {
            this.UsesTwelveHourClock = TimeOfDayRules.UsesTwelveHourClock(this.TimeFormat, CultureInfo.CurrentCulture);
            this.RefreshTexts();
        }

        private void OnToggleHalfOfDayExecuted (object sender, ExecutedRoutedEventArgs e)
        {
            this.EditTime = TimeOfDayRules.ToggleHalfOfDay(this.EditTime);
        }

        private void OnEditTimeChanged ()
        {
            this.RefreshTexts();
            this.EditTimeChanged?.Invoke(this, EventArgs.Empty);
        }

        private void RefreshTexts ()
        {
            const string kTwoDigits = "00";
            CultureInfo culture = CultureInfo.CurrentCulture;
            TimeSpan time = this.EditTime;
            bool usesTwelveHourClock = this.UsesTwelveHourClock;

            // A twenty four hour clock reads 09:05, and a twelve hour one 9:05
            int clockHour = TimeOfDayRules.GetClockHour(time, usesTwelveHourClock);
            this.HourText = usesTwelveHourClock ? clockHour.ToString(culture) : clockHour.ToString(kTwoDigits, culture);
            this.MinuteText = time.Minutes.ToString(kTwoDigits, culture);
            this.HalfOfDayText = TimeOfDayRules.IsAfternoon(time) ? culture.DateTimeFormat.PMDesignator : culture.DateTimeFormat.AMDesignator;
        }
    }
}
