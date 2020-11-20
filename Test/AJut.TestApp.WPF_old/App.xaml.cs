namespace AJut.TestApp.WPF
{
    using AJut.Application;
    using System;
    using System.Windows;

    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public App()
        {
            ApplicationUtilities.RunOnetimeSetup("AJut.TestApp", onExceptionRecieved: UnhandledExceptionProcessor);
            Logger.SetFlushAfterEachLog(true);
        }

        private static bool UnhandledExceptionProcessor (Exception e)
        {
            Logger.LogError(e);
            var result = MessageBox.Show($"Whoopsie daisies!!!\n\nException Detected: {e.Message}\n\nWould you like to mark it as handled?", "Exception caught", MessageBoxButton.YesNo);
            return result == MessageBoxResult.Yes;
        }
    }
}
