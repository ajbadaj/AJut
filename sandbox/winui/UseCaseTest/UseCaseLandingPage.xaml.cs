namespace AJutShowRoomWinUI.UseCaseTest
{
    using System;
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;
    using Microsoft.UI.Xaml.Navigation;

    // ===========[ UseCaseLandingPage ]==========================================
    // Entry page for the use-case leak test. Navigates to the editor by type (no page caching,
    // so leaving the editor releases it) and hosts the leak probe that reports whether prior
    // editor teardowns fully collected. The host Window arrives as the navigation parameter and
    // is passed along to the editor so its DockingManager roots to the real window.
    public sealed partial class UseCaseLandingPage : Page
    {
        private Window m_hostWindow;

        public UseCaseLandingPage ()
        {
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo (NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            m_hostWindow = e.Parameter as Window;
        }

        private void OnOpenEditorClicked (object sender, RoutedEventArgs e)
        {
            this.Frame?.Navigate(typeof(UseCaseEditorPage), m_hostWindow);
        }

        private async void OnRunProbeClicked (object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button != null) { button.IsEnabled = false; }
            try
            {
                string report = await UseCaseLeakRegistry.SettleAndReportAsync();
                this.AppendOutput(report);
            }
            finally
            {
                if (button != null) { button.IsEnabled = true; }
            }
        }

        private void OnResetProbeClicked (object sender, RoutedEventArgs e)
        {
            UseCaseLeakRegistry.Reset();
            this.AppendOutput("Probe history cleared.");
        }

        private void AppendOutput (string body)
        {
            string stamp = DateTime.Now.ToString("HH:mm:ss");
            string entry = $"[{stamp}] {body}";
            this.ProbeOutput.Text = this.ProbeOutput.Text.Length == 0
                ? entry
                : entry + "\n\n" + this.ProbeOutput.Text;
        }
    }
}
