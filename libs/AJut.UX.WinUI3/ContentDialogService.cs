namespace AJut.UX
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.UI;
    using Microsoft.UI.Content;
    using Microsoft.UI.Dispatching;
    using Microsoft.UI.Windowing;
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;

    // ===========[ ContentDialogService ]=======================================
    // WinUI3's ContentDialog allows one open per thread. Calling ShowAsync while another ContentDialog is open on the same
    // thread throws, even when the two are in separate windows (the ContentDialog remarks on Microsoft Learn:
    // https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.ui.xaml.controls.contentdialog). Any
    // app with more than one thing that can raise a dialog (a picker inside a form, an error from a background save) can
    // hit that, and the throw lands in whichever code lost the race. This service owns showing them, one UI thread at a
    // time, and every rule for what happens on overlap is DialogServiceBase's.
    //
    // What it relies on in ContentDialog:
    //  - Hide() ends the dialog's ShowAsync with ContentDialogResult.None, the same as its close button or Escape. That is
    //    why the service gives its own outcome type rather than ContentDialogResult: a None from the service hiding a
    //    dialog for nav back is not the person's answer, and the base never reports it as one.
    //  - Hide() raises Closing first, and a dialog that cancels it stays open. The base copes: whatever asked to show over
    //    it waits until it really closes.
    //  - The next ShowAsync can't start until the previous dialog's ShowAsync has completed, since it is still open while
    //    it closes. The base never shows the next dialog before then.
    //  - A ContentDialog can be shown again once it has closed, and keeps its content, so a dialog hidden for nav back comes
    //    back as it was left.
    //
    // One service per UI thread, found through For(...) from anything on that thread, so a library control and the app
    // share it. Each dialog names its window through its XamlRoot, which WinUI3 needs set before a dialog is shown anyway.
    // The service follows each window it has held a dialog for through AppWindow.Destroying, found from the XamlRoot's
    // ContentIslandEnvironment.AppWindowId, and ends that window's dialogs when it goes. Window.Closed can't be reached
    // from a XamlRoot, and Unloaded is not reliably raised when a window closes, which is why it is AppWindow. The service
    // itself lives as long as its thread's dispatcher queue, so a binding to CanPopBack stays good, and leaves the registry
    // when that queue shuts down.
    //
    // When not to use it: a dialog shown directly with ShowAsync is invisible to the service and still collides with the
    // ones it shows (the service's then ends as NotShown, and is logged). Every ContentDialog in an app should go through
    // it.
    /// <summary>
    /// Shows ContentDialogs one at a time for a UI thread, so asking for a dialog while another is open never throws.
    /// What happens on overlap is the caller's choice (<see cref="eDialogOverlapResponse"/>): wait in line, hide the open
    /// dialog to come back to it (<see cref="DialogServiceBase{TDialog}.PopBack"/>,
    /// <see cref="DialogServiceBase{TDialog}.CanPopBack"/>), replace it, or skip. Get the service with <see cref="For"/>.
    /// </summary>
    /// <remarks>
    /// WinUI3 allows one open ContentDialog per thread, across every window on it, so one service covers all of a thread's
    /// windows: they share its nav back stack and <see cref="DialogServiceBase{TDialog}.CanPopBack"/>. A dialog's window
    /// is its <see cref="UIElement.XamlRoot"/>, which must be set before it is shown. When a window closes, its dialogs end
    /// as <see cref="eDialogOutcome.WindowClosed"/>, and other windows' dialogs carry on.
    /// </remarks>
    /// <example>
    /// <code>
    /// m_datePicker.XamlRoot = this.XamlRoot;
    /// eDialogOutcome outcome = await ContentDialogService.For(this).ShowAsync(m_datePicker, eDialogOverlapResponse.HideForNavBack);
    /// </code>
    /// </example>
    public sealed class ContentDialogService : DialogServiceBase<ContentDialog>
    {
        private static readonly object g_registryLock = new object();
        private static readonly Dictionary<DispatcherQueue, ContentDialogService> g_servicesByQueue = new Dictionary<DispatcherQueue, ContentDialogService>();
        private readonly DispatcherQueue m_dispatcherQueue;
        private readonly Dictionary<WindowId, AppWindow> m_followedWindows = new Dictionary<WindowId, AppWindow>();

        // ===========[ Setup/Construction/Teardown ]===========================
        private ContentDialogService (DispatcherQueue dispatcherQueue)
        {
            m_dispatcherQueue = dispatcherQueue;
            m_dispatcherQueue.ShutdownStarting += this.DispatcherQueue_OnShutdownStarting;
        }

        /// <summary>
        /// The dialog service for the UI thread <paramref name="onTheThreadOf"/> belongs to: pass the element a dialog is
        /// shown over, or the dialog itself. Made the first time a thread asks. Can be called from any thread.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="onTheThreadOf"/> is null</exception>
        /// <exception cref="ArgumentException"><paramref name="onTheThreadOf"/> belongs to no UI thread</exception>
        public static ContentDialogService For (DependencyObject onTheThreadOf)
        {
            if (onTheThreadOf == null)
            {
                throw new ArgumentNullException(nameof(onTheThreadOf));
            }

            DispatcherQueue dispatcherQueue = onTheThreadOf.DispatcherQueue;
            if (dispatcherQueue == null)
            {
                throw new ArgumentException("It belongs to no UI thread (its DispatcherQueue is null), so there is no dialog service for it", nameof(onTheThreadOf));
            }

            // Keyed by the dispatcher queue's projection, which is the same object every time it is asked for while it is
            //  alive, and this keeps it alive. The lock is for apps with windows on more than one UI thread.
            lock (g_registryLock)
            {
                if (!g_servicesByQueue.TryGetValue(dispatcherQueue, out ContentDialogService service))
                {
                    service = new ContentDialogService(dispatcherQueue);
                    g_servicesByQueue.Add(dispatcherQueue, service);
                }

                return service;
            }
        }

        private void DispatcherQueue_OnShutdownStarting (DispatcherQueue sender, DispatcherQueueShutdownStartingEventArgs args)
        {
            sender.ShutdownStarting -= this.DispatcherQueue_OnShutdownStarting;
            foreach (AppWindow appWindow in m_followedWindows.Values)
            {
                appWindow.Destroying -= this.AppWindow_OnDestroying;
            }

            m_followedWindows.Clear();
            this.OnWindowClosed(_ => true);
            lock (g_registryLock)
            {
                g_servicesByQueue.Remove(sender);
            }
        }

        // ===========[ Events ]================================================
        private void AppWindow_OnDestroying (AppWindow sender, object args)
        {
            sender.Destroying -= this.AppWindow_OnDestroying;
            WindowId closedWindowId = sender.Id;
            m_followedWindows.Remove(closedWindowId);

            // A dialog whose window is too far gone to say which it was is taken to be in this one, since every other
            //  window is still standing
            this.OnWindowClosed(_IsInClosedWindow);

            bool _IsInClosedWindow (ContentDialog dialog)
            {
                return !TryGetWindowId(dialog.XamlRoot, out WindowId windowId) || windowId.Value == closedWindowId.Value;
            }
        }

        // ===========[ Toolkit Hooks ]=========================================
        /// <summary>Refuses a dialog with no XamlRoot, and starts following the dialog's window</summary>
        protected override void OnHoldingDialog (ContentDialog dialog)
        {
            if (dialog.XamlRoot == null)
            {
                throw new ArgumentException("A ContentDialog needs its XamlRoot set before it is shown: set it to the XamlRoot of the element it is shown over", nameof(dialog));
            }

            if (!TryGetWindowId(dialog.XamlRoot, out WindowId windowId) || m_followedWindows.ContainsKey(windowId))
            {
                return;
            }

            AppWindow appWindow = AppWindow.GetFromWindowId(windowId);
            if (appWindow == null)
            {
                Logger.LogInfo("[WARNING] [ContentDialogService] Found no AppWindow for a dialog's XamlRoot, so its dialogs won't end as WindowClosed when it closes");
                return;
            }

            m_followedWindows.Add(windowId, appWindow);
            appWindow.Destroying += this.AppWindow_OnDestroying;
        }

        protected override async Task<eDialogOutcome> ShowDialogAsync (ContentDialog dialog)
        {
            ContentDialogResult result = await dialog.ShowAsync();
            switch (result)
            {
                case ContentDialogResult.Primary:
                    return eDialogOutcome.Primary;

                case ContentDialogResult.Secondary:
                    return eDialogOutcome.Secondary;

                default:
                    return eDialogOutcome.None;
            }
        }

        protected override void HideDialog (ContentDialog dialog) => dialog.Hide();

        protected override bool IsOnDialogThread => m_dispatcherQueue.HasThreadAccess;

        protected override bool TryRunOnDialogThread (Action action) => m_dispatcherQueue.TryEnqueue(new DispatcherQueueHandler(action));

        // ===========[ Helper Methods ]========================================
        private static bool TryGetWindowId (XamlRoot root, out WindowId windowId)
        {
            ContentIslandEnvironment environment = root?.ContentIslandEnvironment;
            if (environment == null)
            {
                windowId = default;
                return false;
            }

            windowId = environment.AppWindowId;
            return true;
        }
    }
}
