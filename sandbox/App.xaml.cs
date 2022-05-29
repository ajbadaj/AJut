namespace TheAJutShowRoom
{
    using System;
    using System.Windows;
    using AJut;
    using AJut.TypeManagement;
    using AJut.UX;

    public partial class App : Application
    {
        public App()
        {
            // Go through all types and find type id registrations, this will allow automatic discovery and propogation of type matching
            TypeIdRegistrar.RegisterAllTypeIds(typeof(App).Assembly);

            // Run a one time setup which will establish an appdata location, project name, logging, seed obfuscation, and optionally apply root exception handling
            ApplicationUtilities.RunOnetimeSetup("AJut.ShowRoom", onExceptionRecieved: UnhandledExceptionProcessor);

            // Add an entry to the log so we know we got this far!
            Logger.LogInfo("Starting up AJut Show Room");
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
