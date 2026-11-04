namespace AJutShowRoomUIKit
{
    using AJut;
    using AJut.UX;
    using AJut.UX.Theming;
    using Foundation;
    using UIKit;

    [Register(nameof(AppDelegate))]
    public class AppDelegate : UIApplicationDelegate
    {
        public override bool FinishedLaunching (UIApplication application, NSDictionary launchOptions)
        {
            ApplicationUtilities.RunOnetimeSetup(new ApplicationSetupConfig("UIKit_ShowRoom")
            {
                SharedProjectName = "AJut.ShowRoom",
                OnExceptionReceived = LogException,
            });
            Logger.LogInfo("Starting up AJut Show Room for UIKit");

            // The showroom runs with the AJut theme, as the WinUI3 one does; the theme bar at the top switches it off to
            // check the unthemed defaults. It has to be set before the first AJut control is made.
            ThemedColors.IsThemeLoaded = true;
            return true;
        }

        public override UISceneConfiguration GetConfiguration (UIApplication application, UISceneSession connectingSceneSession, UISceneConnectionOptions options)
        {
            // Matches the configuration Info.plist declares, which names SceneDelegate as the scene's delegate
            return new UISceneConfiguration("Default Configuration", connectingSceneSession.Role);
        }

        // Setup has logged it already; returning false lets the app go down the way it would have
        private static bool LogException (UnhandledExceptionReport report) => false;
    }
}
