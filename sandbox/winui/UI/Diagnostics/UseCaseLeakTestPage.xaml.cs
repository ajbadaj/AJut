namespace AJutShowRoomWinUI.UI.Diagnostics
{
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;

    /// <summary>
    /// Hosts the use-case leak harness in its own Frame, rooted to the main window so the editor's DockingManager
    /// has a real root window to work against.
    /// </summary>
    public sealed partial class UseCaseLeakTestPage : Page
    {
        private bool m_useCaseInitialized = false;

        public UseCaseLeakTestPage ()
        {
            this.InitializeComponent();
        }

        private void UseCaseFrame_OnLoaded (object sender, RoutedEventArgs e)
        {
            if (m_useCaseInitialized)
            {
                return;
            }

            m_useCaseInitialized = true;

            // Pass the main window through as the nav parameter so the editor's DockingManager roots to
            // the real root window (the editor lives inside this Frame, like a host editor page). The
            // landing page reads it as a Window, so it has to stay one.
            Window hostWindow = App.Instance.MainWindow;
            this.UseCaseFrame.Navigate(typeof(UseCaseTest.UseCaseLandingPage), hostWindow);
        }
    }
}
