namespace AJutShowRoomWinUI
{
    using AJut;
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;
    using DPUtils = AJut.UX.DPUtils<NativeCrashFilterCheck>;

    /// <summary>
    /// A manual check that AJut's native crash filter chains to one the host installed first, and runs it first.
    /// </summary>
    public sealed partial class NativeCrashFilterCheck : UserControl
    {
        public NativeCrashFilterCheck ()
        {
            this.InitializeComponent();
            this.LogFilePath = Logger.LogFilePath ?? "(no log file - logging was not set up)";
        }

        public static readonly DependencyProperty LogFilePathProperty = DPUtils.Register(_ => _.LogFilePath);
        public string LogFilePath
        {
            get => (string)this.GetValue(LogFilePathProperty);
            set => this.SetValue(LogFilePathProperty, value);
        }

        private void OnCrashNatively_OnClick (object sender, RoutedEventArgs e)
        {
            Logger.LogInfo("[CRASH-CHECK] Starting a native thread that access violates");
            NativeCrashCheckInterop.StartThreadThatAccessViolates();
        }
    }
}
