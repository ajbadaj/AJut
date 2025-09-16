namespace AJutShowRoomWinUI
{
    using AJut;
    using AJut.UX;
    using AJut.UX.Theming;
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Media;
    using System.Diagnostics;

    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        private readonly WindowManager m_windowManager = new WindowManager();

        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App ()
        {
            Instance = this;
            this.InitializeComponent();

            var assembly = typeof(Logger).Assembly;
            var versionInfo = FileVersionInfo.GetVersionInfo(assembly.Location);
            this.AJut_Core_Version = versionInfo.FileVersion?.ToString() ?? "unknown version";

            assembly = typeof(ApplicationUtilities).Assembly;
            versionInfo = FileVersionInfo.GetVersionInfo(assembly.Location);
            this.AJut_UX_WinUI_Version = versionInfo.FileVersion?.ToString() ?? "unknown version";
        }

        /// <summary>
        /// Invoked when the application is launched.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override void OnLaunched (Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            ApplicationUtilities.RunOnetimeSetup("WinUI_ShowRoom", this, sharedProjectName: "AJut.ShowRoom", onExceptionRecieved: LogException);
            Logger.FlushToFileAfterEach = true;
            Logger.LogInfo("Starting up AJut Show Room for WinUI");
            Logger.LogInfo($"Using AJut.Core version #{this.AJut_Core_Version}");
            Logger.LogInfo($"Using AJut.UX.WinUI version #{this.AJut_UX_WinUI_Version}");

            m_window = new MainWindow(m_windowManager, this.ThemeManager);
            m_window.Activate();
            this.ThemeManager.Setup(this, m_windowManager);
        }

        private bool LogException (object exceptionObject)
        {
            return true;
        }

        private Window? m_window;

        public string AJut_Core_Version { get; }
        public string AJut_UX_WinUI_Version { get; }
        public static App? Instance { get; private set; }

        public AppThemeManager ThemeManager { get; } = new AppThemeManager();
        public WindowManager Windows => m_windowManager;
    }
}
