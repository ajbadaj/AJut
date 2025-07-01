namespace AJutShowRoomWinUI
{
    using System.Diagnostics;
    using AJut;
    using AJut.UX;
    using Microsoft.UI.Xaml;

    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App ()
        {
            this.InitializeComponent();


            var assembly = typeof(Logger).Assembly;
            var versionInfo = FileVersionInfo.GetVersionInfo(assembly.Location);
            AJut_Core_Version = versionInfo.FileVersion?.ToString() ?? "unknown version";

            assembly = typeof(ApplicationUtilities).Assembly;
            versionInfo = FileVersionInfo.GetVersionInfo(assembly.Location);
            AJut_UX_WinUI_Version = versionInfo.FileVersion?.ToString() ?? "unknown version";

        }

        /// <summary>
        /// Invoked when the application is launched.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override void OnLaunched (Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            ApplicationUtilities.RunOnetimeSetup("WinUI_ShowRoom", sharedProjectName: "AJut.ShowRoom", onExceptionRecieved: LogException);
            Logger.FlushToFileAfterEach = true;
            Logger.LogInfo("Starting up AJut Show Room for WinUI");
            Logger.LogInfo($"Using AJut.Core version #{AJut_Core_Version}");
            Logger.LogInfo($"Using AJut.UX.Wpf version #{AJut_UX_WinUI_Version}");

            m_window = new MainWindow();
            m_window.Activate();
        }

        private void LogException (object exceptionObject, bool isTerminating)
        {
            Logger.LogError($"Exception received: {exceptionObject} - {(isTerminating ? "is" : "not")} terminating");
        }

        private Window? m_window;

        public string AJut_Core_Version { get; }
        public string AJut_UX_WinUI_Version { get; }
    }
}
