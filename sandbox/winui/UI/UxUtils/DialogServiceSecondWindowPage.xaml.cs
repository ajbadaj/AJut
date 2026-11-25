namespace AJutShowRoomWinUI.UI.UxUtils
{
    using System;
    using AJut.UX;
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;

    /// <summary>
    /// The content of the dialog service page's second window: a dialog asked for here, on the same UI thread as the main
    /// window, shares the main window's dialog service. Outcomes are reported to the page that opened it.
    /// </summary>
    public sealed partial class DialogServiceSecondWindowPage : Page
    {
        private readonly Action<string, eDialogOutcome> m_report;

        // ===========[ Setup/Construction/Teardown ]===========================
        /// <param name="report">Takes each outcome; it is the opening page's log, held until this window closes</param>
        public DialogServiceSecondWindowPage (Action<string, eDialogOutcome> report)
        {
            m_report = report;
            this.InitializeComponent();
        }

        // ===========[ Events ]================================================
        private async void AskQueued_OnClick (object? sender, RoutedEventArgs e)
        {
            m_report("Second window, queued", await ContentDialogService.For(this).ShowAsync(this.HereDialog, eDialogOverlapResponse.Queue));
        }

        private async void AskHidingTheOpenOne_OnClick (object? sender, RoutedEventArgs e)
        {
            m_report("Second window, hiding the open one", await ContentDialogService.For(this).ShowAsync(this.HereDialog, eDialogOverlapResponse.HideForNavBack));
        }
    }
}
