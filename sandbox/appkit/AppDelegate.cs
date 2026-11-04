namespace AJutShowRoomAppKit
{
    using AJut;
    using AJut.UX;
    using AJut.UX.Theming;
    using AJutShowRoomAppKit.UI;
    using AppKit;
    using Foundation;
    using ObjCRuntime;

    public sealed class AppDelegate : NSApplicationDelegate
    {
        private const string kUseThemesKey = "AJutShowRoom_UseThemes";

        private ShowroomWindow m_window;

        // ===========[ Properties ]============================================
        /// <summary>Light, dark or follow the OS, saved between runs</summary>
        public static AppThemeManager ThemeManager { get; private set; }

        /// <summary>
        /// Whether the AJut palette is loaded. Off shows every control unthemed (macOS system colors),
        /// which is the other half of what each control page has to look right in.
        /// </summary>
        public static bool UseThemes
        {
            get => ThemedColors.IsThemeLoaded;
            set
            {
                if (value)
                {
                    ThemedColors.LoadTheme();
                }
                else
                {
                    ThemedColors.UnloadTheme();
                }

                NSUserDefaults.StandardUserDefaults.SetBool(value, kUseThemesKey);
            }
        }

        // ===========[ Public Interface Methods ]==============================
        public override void DidFinishLaunching (NSNotification notification)
        {
            // 0. App setup: the app data root (shared with the other showrooms), logging and exception reporting
            ApplicationUtilities.RunOnetimeSetup(new ApplicationSetupConfig("AppKit_ShowRoom")
            {
                SharedProjectName = "AJut.ShowRoom",
                OnExceptionReceived = LogException,
            });
            Logger.LogInfo("Starting up AJut Show Room for AppKit");

            // 1. Theming: themes on unless the last run turned them off
            NSUserDefaults defaults = NSUserDefaults.StandardUserDefaults;
            bool useThemes = defaults[kUseThemesKey] == null || defaults.BoolForKey(kUseThemesKey);
            UseThemes = useThemes;
            ThemeManager = new AppThemeManager();

            // 2. Menu bar, the Edit menu is what makes copy and paste work in text fields
            NSApplication.SharedApplication.MainMenu = BuildMainMenu();

            // 3. The window
            m_window = new ShowroomWindow();
            m_window.Center();
            m_window.MakeKeyAndOrderFront(this);
            NSApplication.SharedApplication.Activate();
        }

        public override bool ApplicationShouldTerminateAfterLastWindowClosed (NSApplication sender) => true;

        public override void WillTerminate (NSNotification notification)
        {
            ThemeManager?.Dispose();
            ThemeManager = null;
        }

        // ===========[ Helper Methods ]========================================
        // Setup has already logged it, and the showroom keeps going where the platform lets it, as the WinUI3 one does
        private static bool LogException (UnhandledExceptionReport report) => true;

        // Menu items with a selector and no target go down the responder chain, which is how the
        // standard items find the focused text field (copy:, paste:) or the app (terminate:)
        private static NSMenu BuildMainMenu ()
        {
            var bar = new NSMenu();

            var appMenu = new NSMenu();
            appMenu.AddItem(new NSMenuItem("Quit AJut Showroom", new Selector("terminate:"), "q"));
            bar.AddItem(new NSMenuItem { Submenu = appMenu });

            var fileMenu = new NSMenu("File");
            fileMenu.AddItem(new NSMenuItem("Close Window", new Selector("performClose:"), "w"));
            bar.AddItem(new NSMenuItem { Submenu = fileMenu });

            var editMenu = new NSMenu("Edit");
            editMenu.AddItem(new NSMenuItem("Undo", new Selector("undo:"), "z"));
            editMenu.AddItem(new NSMenuItem("Redo", new Selector("redo:"), "Z"));
            editMenu.AddItem(NSMenuItem.SeparatorItem);
            editMenu.AddItem(new NSMenuItem("Cut", new Selector("cut:"), "x"));
            editMenu.AddItem(new NSMenuItem("Copy", new Selector("copy:"), "c"));
            editMenu.AddItem(new NSMenuItem("Paste", new Selector("paste:"), "v"));
            editMenu.AddItem(new NSMenuItem("Select All", new Selector("selectAll:"), "a"));
            bar.AddItem(new NSMenuItem { Submenu = editMenu });

            return bar;
        }
    }
}
