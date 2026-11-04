namespace AJut.UX.Theming
{
    using System;
    using System.Runtime.InteropServices;
    using System.Runtime.InteropServices.Marshalling;
    using System.Runtime.Versioning;
    using System.Threading;

    /// <summary>
    /// Whether Windows prefers touch or the mouse for a window right now, and word when that may have changed: Windows 11
    /// 24H2's preferred interaction mode, which follows a 2-in-1 between its laptop and tablet postures (a keyboard taken
    /// off or folded back). Windows has no such signal for a desktop app before 24H2, and there
    /// <see cref="TryCreateForWindow"/> returns null.
    /// </summary>
    /// <remarks>
    /// Make it and dispose it on the window's UI thread. Windows raises the change on that thread as a rule, but nothing
    /// promises it, so <see cref="PreferenceMayHaveChanged"/> can arrive on any thread. Each UI package's
    /// TouchPreferenceWatcher follows one of these on a window's UI thread, which is the easy way to use it.
    /// </remarks>
    [SupportedOSPlatform("windows")]
    public sealed partial class WindowsTouchPreferenceSource : ITouchPreferenceSource, IDisposable
    {
        // How long a disposed source waits for Windows' first notice before taking its handler back anyway (see Dispose)
        private static readonly TimeSpan kFirstNoticeWait = TimeSpan.FromSeconds(1);

        // Windows' settings object for the window, one reference of which is this source's until it cleans up
        private nint m_settings;
        private long m_changedToken;
        private bool m_isSubscribed;
        private volatile bool m_isDisposed;
        private volatile bool m_hasHeardFromWindows;
        private int m_isCleanUpPosted;
        private Timer? m_firstNoticeTimeout;

        // The UI thread's way back, for cleanup that can't be done where it's noticed (see Dispose and the finalizer)
        private readonly SynchronizationContext? m_creationContext;

        // ===========[ Setup/Construction/Teardown ]===================================
        private WindowsTouchPreferenceSource (nint settings)
        {
            m_settings = settings;
            m_creationContext = SynchronizationContext.Current;
        }

        /// <summary>
        /// Follows Windows' touch preference for the window <paramref name="windowHandle"/> (an HWND). Call it on the
        /// window's UI thread. Null when Windows doesn't offer one: before Windows 11 24H2, or when it can't be reached
        /// from this thread.
        /// </summary>
        public static WindowsTouchPreferenceSource? TryCreateForWindow (nint windowHandle)
        {
            if (windowHandle == IntPtr.Zero)
            {
                throw new ArgumentException("A window handle is needed; the window must have been created", nameof(windowHandle));
            }

            if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 26100))
            {
                return null;
            }

            nint settings = WindowsUIViewSettingsNative.TryGetForWindow(windowHandle);
            if (settings == IntPtr.Zero)
            {
                return null;
            }

            var source = new WindowsTouchPreferenceSource(settings);
            if (!source.TrySubscribe())
            {
                source.Dispose();
                return null;
            }

            return source;
        }

        /// <summary>
        /// Stops following Windows: nothing is raised from here on, and what was held from Windows is let go, soon after
        /// when Windows hasn't yet sent its first notice. Call it on the window's UI thread. Safe to call more than once.
        /// </summary>
        public void Dispose ()
        {
            if (m_isDisposed)
            {
                return;
            }

            m_isDisposed = true;
            this.PreferenceMayHaveChanged = null;
            GC.SuppressFinalize(this);

            // Windows sends a first notice soon after a handler is added, and taking the handler back before that notice
            // has been delivered can deadlock inside Windows (the C#/WinRT projection does the same). So until it has
            // been, the cleanup waits for it, going back to this thread when it comes, or after a second at most. With no
            // way back to this thread, the cleanup is skipped and what was held from Windows is kept, rather than risk a
            // deadlock.
            if (!m_isSubscribed || m_hasHeardFromWindows)
            {
                this.CleanUp();
            }
            else if (m_creationContext != null)
            {
                m_firstNoticeTimeout = new Timer(this.OnFirstNoticeTimeout, null, kFirstNoticeWait, Timeout.InfiniteTimeSpan);
            }
            else
            {
                Logger.LogInfo("[TOUCH-PREFERENCE] Disposed before Windows' first notice with no way back to the UI thread; keeping the Windows settings object rather than risk a deadlock");
            }
        }

        // Only reached for a source that was never disposed. Windows' settings object is only safe to call or let go of
        // on the window's UI thread (WindowsUIViewSettingsNative.TryGetForWindow says why), and this is the finalizer
        // thread, so the cleanup goes back to the thread the source was made on. With no way back it is skipped, which
        // keeps the settings object and the handler, rather than risk a deadlock.
        ~WindowsTouchPreferenceSource ()
        {
            nint settings = Interlocked.Exchange(ref m_settings, IntPtr.Zero);
            if ((settings != IntPtr.Zero) && (m_creationContext != null))
            {
                m_creationContext.Post(LateCleanUp.Run, new LateCleanUp(settings, m_changedToken, m_isSubscribed));
            }
        }

        // ===========[ Events ]===================================
        /// <summary>
        /// Raised when Windows says the preference may have changed, which can be on a thread of Windows' own. Read
        /// <see cref="IsTouchPreferred"/> again to find out.
        /// </summary>
        public event EventHandler? PreferenceMayHaveChanged;

        // ===========[ Properties ]===================================
        /// <summary>True when Windows prefers touch for the window right now. Asks Windows on each read; false once disposed.</summary>
        public bool IsTouchPreferred
        {
            get
            {
                nint settings = m_settings;
                return !m_isDisposed && (settings != IntPtr.Zero) && WindowsUIViewSettingsNative.IsTouchPreferred(settings);
            }
        }

        /// <summary>True while Windows holds this source's change handler</summary>
        internal bool IsSubscribed => m_isSubscribed;

        // ===========[ Helper Methods ]===================================
        private bool TrySubscribe ()
        {
            nint handlerPointer = WindowsUIViewSettingsNative.CreateHandlerPointer(new ChangedHandler(this));
            if (handlerPointer == IntPtr.Zero)
            {
                Logger.LogError("[TOUCH-PREFERENCE] Could not make the interaction mode change handler");
                return false;
            }

            try
            {
                // Windows takes its own reference to the handler, released when it is removed
                int hr = WindowsUIViewSettingsNative.AddChangedHandler(m_settings, handlerPointer, out m_changedToken);
                if (hr < 0)
                {
                    Logger.LogError($"[TOUCH-PREFERENCE] Windows refused the interaction mode change handler (0x{hr:X8})");
                    return false;
                }

                m_isSubscribed = true;
                return true;
            }
            finally
            {
                Marshal.Release(handlerPointer);
            }
        }

        private void OnWindowsChangeNotice ()
        {
            m_hasHeardFromWindows = true;
            if (m_isDisposed)
            {
                this.PostCleanUp();
                return;
            }

            this.PreferenceMayHaveChanged?.Invoke(this, EventArgs.Empty);
        }

        private void OnFirstNoticeTimeout (object? state)
        {
            this.PostCleanUp();
        }

        // Asks the UI thread to clean up, once
        private void PostCleanUp ()
        {
            if ((m_creationContext != null) && (Interlocked.Exchange(ref m_isCleanUpPosted, 1) == 0))
            {
                m_creationContext.Post(RunCleanUp, this);
            }
        }

        private static void RunCleanUp (object? source)
        {
            ((WindowsTouchPreferenceSource)source!).CleanUp();
        }

        // Takes the handler back from Windows and lets go of the settings object. Runs on the UI thread, at most once.
        private void CleanUp ()
        {
            m_firstNoticeTimeout?.Dispose();
            m_firstNoticeTimeout = null;

            nint settings = Interlocked.Exchange(ref m_settings, IntPtr.Zero);
            if (settings == IntPtr.Zero)
            {
                return;
            }

            if (m_isSubscribed)
            {
                int hr = WindowsUIViewSettingsNative.RemoveChangedHandler(settings, m_changedToken);
                if (hr < 0)
                {
                    Logger.LogError($"[TOUCH-PREFERENCE] Windows would not drop the interaction mode change handler (0x{hr:X8})");
                }

                m_isSubscribed = false;
            }

            Marshal.Release(settings);
        }

        // ===========[ Subclasses/structs ]===================================
        // What a source that was never disposed still owes Windows, done back on the UI thread
        private sealed record class LateCleanUp (nint Settings, long ChangedToken, bool IsSubscribed)
        {
            public static void Run (object? state)
            {
                var cleanUp = (LateCleanUp)state!;
                if (cleanUp.IsSubscribed)
                {
                    WindowsUIViewSettingsNative.RemoveChangedHandler(cleanUp.Settings, cleanUp.ChangedToken);
                }

                Marshal.Release(cleanUp.Settings);
            }
        }

        /// <summary>
        /// What Windows calls when the preferred interaction mode may have changed. It holds its source weakly: Windows
        /// keeps the handler for as long as it is subscribed, and a strong hold would keep a source that was never
        /// disposed alive for the life of the app, along with whatever is subscribed to it, where held weakly the
        /// source's finalizer gets to clean up.
        /// </summary>
        [GeneratedComClass]
        internal sealed partial class ChangedHandler : IPreferredInteractionModeChangedHandler, IAgileObject
        {
            private readonly WeakReference<WindowsTouchPreferenceSource> m_source;

            public ChangedHandler (WindowsTouchPreferenceSource source)
            {
                m_source = new WeakReference<WindowsTouchPreferenceSource>(source);
            }

            public int Invoke (nint sender, nint args)
            {
                // Windows calls this, from the window's UI thread as a rule, and nothing thrown here may reach it
                try
                {
                    if (m_source.TryGetTarget(out WindowsTouchPreferenceSource? source))
                    {
                        source.OnWindowsChangeNotice();
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError("[TOUCH-PREFERENCE] Handling a change in Windows' touch preference failed", ex);
                }

                return 0;
            }
        }
    }
}
