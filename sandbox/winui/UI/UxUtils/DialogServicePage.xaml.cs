namespace AJutShowRoomWinUI.UI.UxUtils
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using AJut.UX;
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;
    using Windows.Graphics;
    using DPUtils = AJut.UX.DPUtils<DialogServicePage>;

    /// <summary>
    /// ContentDialogService in use: a picker hidden-for-nav-back over a form, a replace, a queued notice, a tip skipped
    /// when busy, and a second window on the same thread. Every outcome lands in the log on the page.
    /// </summary>
    public sealed partial class DialogServicePage : Page
    {
        private const int kMaxOutcomesShown = 12;
        private const int kSecondWindowWidth = 560;
        private const int kSecondWindowHeight = 320;
        private static readonly TimeSpan kDelayBeforeAsking = TimeSpan.FromSeconds(3);
        private static readonly string[] kColorNames = { "Teal", "Amber", "Plum" };
        private static readonly string[] kSizeNames = { "Small", "Medium", "Large" };
        private readonly List<string> m_outcomes = new List<string>();

        // ===========[ Setup/Construction/Teardown ]===========================
        public DialogServicePage ()
        {
            this.Dialogs = ContentDialogService.For(this);
            this.InitializeComponent();
            this.Unloaded += this.OnUnloaded;
        }

        // The service lives as long as the UI thread, and the OneWay bindings to its CanPopBack keep a handler on its
        //  PropertyChanged until they stop tracking
        private void OnUnloaded (object? sender, RoutedEventArgs e)
        {
            this.Bindings.StopTracking();
        }

        // ===========[ Events ]================================================
        private async void OpenForm_OnClick (object? sender, RoutedEventArgs e)
        {
            this.Report("Form", await this.Dialogs.ShowAsync(this.FormDialog));
        }

        private async void PickColor_OnClick (object? sender, RoutedEventArgs e)
        {
            eDialogOutcome outcome = await this.Dialogs.ShowAsync(this.ColorPickerDialog, eDialogOverlapResponse.HideForNavBack);
            this.Report("Color picker", outcome);
            if (outcome == eDialogOutcome.Primary)
            {
                this.PickedChoice = $"Color: {kColorNames[this.ColorIndex]}";
            }
        }

        private async void SwapForSizePicker_OnClick (object? sender, RoutedEventArgs e)
        {
            eDialogOutcome outcome = await this.Dialogs.ShowAsync(this.SizePickerDialog, eDialogOverlapResponse.Replace);
            this.Report("Size picker", outcome);
            if (outcome == eDialogOutcome.Primary)
            {
                this.PickedChoice = $"Size: {kSizeNames[this.SizeIndex]}";
            }
        }

        private void Back_OnClick (object? sender, RoutedEventArgs e) => this.Dialogs.PopBack();

        private async void QueueNotice_OnClick (object? sender, RoutedEventArgs e)
        {
            await Task.Delay(kDelayBeforeAsking);
            this.Report("Notice", await this.Dialogs.ShowAsync(this.NoticeDialog, eDialogOverlapResponse.Queue));
        }

        private async void DelayedTip_OnClick (object? sender, RoutedEventArgs e)
        {
            await Task.Delay(kDelayBeforeAsking);
            this.Report("Tip", await this.Dialogs.ShowAsync(this.TipDialog, eDialogOverlapResponse.SkipIfBusy));
        }

        // A Hide closes with None, the same as Cancel or Escape, so refusing None is what refusing to be hidden looks like
        private void FormDialog_OnClosing (ContentDialog sender, ContentDialogClosingEventArgs args)
        {
            if (this.FormRefusesToHide && args.Result == ContentDialogResult.None)
            {
                args.Cancel = true;
            }
        }

        private void OpenSecondWindow_OnClick (object? sender, RoutedEventArgs e)
        {
            var secondWindow = new Window
            {
                Title = "Dialog service: a second window",
                Content = new DialogServiceSecondWindowPage(this.Report),
            };

            secondWindow.AppWindow.Resize(new SizeInt32(kSecondWindowWidth, kSecondWindowHeight));
            secondWindow.Activate();
        }

        // ===========[ Dependency Properties ]=================================
        public static readonly DependencyProperty FormRefusesToHideProperty = DPUtils.Register(_ => _.FormRefusesToHide);
        public bool FormRefusesToHide
        {
            get => (bool)this.GetValue(FormRefusesToHideProperty);
            set => this.SetValue(FormRefusesToHideProperty, value);
        }

        public static readonly DependencyProperty FormNameProperty = DPUtils.Register(_ => _.FormName, string.Empty);
        public string FormName
        {
            get => (string)this.GetValue(FormNameProperty);
            set => this.SetValue(FormNameProperty, value);
        }

        public static readonly DependencyProperty PickedChoiceProperty = DPUtils.Register(_ => _.PickedChoice, "Nothing picked yet");
        public string PickedChoice
        {
            get => (string)this.GetValue(PickedChoiceProperty);
            set => this.SetValue(PickedChoiceProperty, value);
        }

        public static readonly DependencyProperty ColorIndexProperty = DPUtils.Register(_ => _.ColorIndex, 0);
        public int ColorIndex
        {
            get => (int)this.GetValue(ColorIndexProperty);
            set => this.SetValue(ColorIndexProperty, value);
        }

        public static readonly DependencyProperty SizeIndexProperty = DPUtils.Register(_ => _.SizeIndex, 0);
        public int SizeIndex
        {
            get => (int)this.GetValue(SizeIndexProperty);
            set => this.SetValue(SizeIndexProperty, value);
        }

        public static readonly DependencyProperty OutcomeLogProperty = DPUtils.Register(_ => _.OutcomeLog, "Nothing has ended yet");
        public string OutcomeLog
        {
            get => (string)this.GetValue(OutcomeLogProperty);
            set => this.SetValue(OutcomeLogProperty, value);
        }

        // ===========[ Properties ]============================================
        /// <summary>This thread's dialog service, which every dialog on the page goes through</summary>
        public ContentDialogService Dialogs { get; }

        public string[] ColorNames => kColorNames;
        public string[] SizeNames => kSizeNames;

        public string ServiceCode { get; } = @"// One service per UI thread, found from anything on it, so a library control and the app share it
ContentDialogService dialogs = ContentDialogService.For(this);

// The form asks with the default, Queue: it waits for any dialog already showing
eDialogOutcome formOutcome = await dialogs.ShowAsync(this.FormDialog);

// From inside the form: hide the form, show the picker, and the form comes back after
eDialogOutcome pickerOutcome = await dialogs.ShowAsync(this.ColorPickerDialog, eDialogOverlapResponse.HideForNavBack);

// A back button inside the picker
<Button Content=""Back"" IsEnabled=""{x:Bind Dialogs.CanPopBack, Mode=OneWay}"" Click=""Back_OnClick""/>
private void Back_OnClick (object sender, RoutedEventArgs e) => this.Dialogs.PopBack();

// A dialog made in code names its window through its XamlRoot, which WinUI3 needs anyway
var saved = new ContentDialog { XamlRoot = this.XamlRoot, Title = ""Saved"", CloseButtonText = ""OK"" };
await dialogs.ShowAsync(saved, eDialogOverlapResponse.SkipIfBusy);";

        // ===========[ Helper Methods ]========================================
        private void Report (string dialogName, eDialogOutcome outcome)
        {
            m_outcomes.Insert(0, $"{dialogName}: {outcome}");
            if (m_outcomes.Count > kMaxOutcomesShown)
            {
                m_outcomes.RemoveAt(m_outcomes.Count - 1);
            }

            this.OutcomeLog = String.Join(Environment.NewLine, m_outcomes);
        }
    }
}
