namespace AJutShowRoom
{
    using AJut;
    using AJut.TypeManagement;
    using AJut.UX;
    using System;
    using System.Windows;

    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public App()
        {
            TypeIdRegistrar.RegisterAllTypeIds(typeof(App).Assembly);
            ApplicationUtilities.RunOnetimeSetup("AJut.TestApp", onExceptionRecieved: UnhandledExceptionProcessor);
            Logger.FlushAfterEach = true;
        }

        private static bool UnhandledExceptionProcessor (Exception e)
        {
            Logger.LogError(e);
            var result = MessageBox.Show($"Whoopsie daisies!!!\n\nException Detected: {e.Message}\n\nWould you like to mark it as handled?", "Exception caught", MessageBoxButton.YesNo);
            return result == MessageBoxResult.Yes;
        }

        public static string AppDataPath(string pathEnd)
        {
            return System.IO.Path.Combine(ApplicationUtilities.AppDataRoot, pathEnd);
        }
    }
}
