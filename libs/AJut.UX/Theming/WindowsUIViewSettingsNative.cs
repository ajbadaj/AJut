namespace AJut.UX.Theming
{
    using System;
    using System.Runtime.InteropServices;
    using System.Runtime.InteropServices.Marshalling;
    using System.Runtime.Versioning;

    /// <summary>
    /// The Windows calls behind <see cref="WindowsTouchPreferenceSource"/>: UIViewSettings' preferred interaction mode for
    /// one window, bound by hand. See <see cref="TryGetForWindow"/> for why it is bound this way.
    /// </summary>
    [SupportedOSPlatform("windows")]
    internal static unsafe partial class WindowsUIViewSettingsNative
    {
        private const string kUIViewSettingsClassName = "Windows.UI.ViewManagement.UIViewSettings";
        private const int kModeMouse = 0;
        private const int kModeTouch = 1;

        // Vtable slots, from Windows.UI.winmd: IUnknown's three, IInspectable's three, then each interface's own methods
        private const int kGetForWindowSlot = 6;
        private const int kGetPreferredInteractionModeSlot = 6;
        private const int kAddPreferredInteractionModeChangedSlot = 7;
        private const int kRemovePreferredInteractionModeChangedSlot = 8;

        private static readonly Guid kUIViewSettingsInteropIid = new Guid("3694DBF9-8F68-44BE-8FF5-195C98EDE8A6");
        private static readonly Guid kPreferredInteractionModeIid = new Guid("426DE261-82EC-5F61-BAD4-19B2D0D3CF35");
        private static readonly Guid kPreferredInteractionModeChangedHandlerIid = new Guid("BF3E2A14-6FCE-5BB3-838E-DE9A00E11AAD");
        private static readonly StrategyBasedComWrappers g_comWrappers = new StrategyBasedComWrappers();

        // ===========[ Public Interface Methods ]===================================
        /// <summary>
        /// Windows' preferred interaction mode settings for <paramref name="windowHandle"/> (an IUIViewSettingsPreferredInteractionMode),
        /// or zero when Windows can't give them (before Windows 11 24H2, or on a thread with no COM set up). The caller owns
        /// one reference, and lets it go with <see cref="Marshal.Release"/> on the window's UI thread.
        /// </summary>
        public static nint TryGetForWindow (nint windowHandle)
        {
            // What Windows offers a desktop app for "is this being used by touch", and why it's reached this way:
            //
            // * Windows 10 had tablet mode, read through UIViewSettings.UserInteractionMode and announced with a
            //   WM_SETTINGCHANGE of "UserInteractionMode". Windows 11 removed tablet mode, and that property reports Mouse.
            // * The older WM_SETTINGCHANGE "ConvertibleSlateMode" with GetSystemMetrics(SM_CONVERTIBLESLATEMODE) is a raw
            //   hardware flag, and on its own it's wrong: a desktop with a pen tablet can report 0, which means slate.
            //   Windows only treats it as tablet posture on a device marked convertible (the SMBIOS enclosure type,
            //   DeviceForm, or ConvertibilityEnabled), and the GetSystemMetrics docs say it doesn't apply to desktops.
            // * Windows 11 24H2 (10.0.26100, UniversalApiContract 19) added UIViewSettings.GetPreferredInteractionMode
            //   and PreferredInteractionModeChanged, which make that call themselves. Where they aren't supported they
            //   say Mouse and never raise. A desktop app gets a UIViewSettings for its own window through
            //   IUIViewSettingsInterop.GetForWindow, asking for the 24H2 interface directly, so a Windows without it
            //   just answers E_NOINTERFACE.
            //
            // The C#/WinRT projection would make this a few lines, but the one the .NET SDK picks for net8 predates the
            // 24H2 members, and pinning a newer one makes every app using AJut pin it too or fail to load AJut. WPF has
            // no projection at all, and taking one on costs every WPF app a 25 MB dependency.
            //
            // So the calls are made through the vtable, with the interface ids and the method order from
            // Windows.UI.winmd, and the settings object is held as a plain COM pointer rather than a ComWrappers wrapper,
            // for two reasons found the hard way:
            // * Windows tears a window's settings object down by way of that window's UI thread. Released from the
            //   collector's finalizer thread (where a wrapper releases it), it can deadlock against the UI thread calling
            //   into the same Windows code, or waiting for finalizers. So it is released on the UI thread, in Dispose.
            // * ComObject.FinalRelease, the wrapper's way to release early, makes .NET 8 (seen with 8.0.32, x86 and x64)
            //   release a second time when the collector finalizes the wrapper, and the process dies with an access
            //   violation in ComObject.Finalize at the next collection.
            //
            // https://learn.microsoft.com/uwp/api/windows.ui.viewmanagement.uiviewsettings.getpreferredinteractionmode
            // https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-getsystemmetrics (SM_CONVERTIBLESLATEMODE)
            nint className = IntPtr.Zero;
            nint factory = IntPtr.Zero;
            try
            {
                int hr = WindowsCreateString(kUIViewSettingsClassName, (uint)kUIViewSettingsClassName.Length, out className);
                if (hr < 0)
                {
                    return LogFailure("making the UIViewSettings class name", hr);
                }

                hr = RoGetActivationFactory(className, in kUIViewSettingsInteropIid, out factory);
                if (hr < 0)
                {
                    return LogFailure("getting the UIViewSettings factory", hr);
                }

                Guid settingsIid = kPreferredInteractionModeIid;
                nint settings = IntPtr.Zero;
                var getForWindow = (delegate* unmanaged[Stdcall]<nint, nint, Guid*, nint*, int>)VtableSlot(factory, kGetForWindowSlot);
                hr = getForWindow(factory, windowHandle, &settingsIid, &settings);
                if (hr < 0)
                {
                    return LogFailure("getting the window's preferred interaction mode settings", hr);
                }

                return settings;
            }
            finally
            {
                if (factory != IntPtr.Zero)
                {
                    Marshal.Release(factory);
                }

                if (className != IntPtr.Zero)
                {
                    WindowsDeleteString(className);
                }
            }
        }

        /// <summary>Asks <paramref name="settings"/> whether Windows prefers touch over the mouse right now. False when it can't say.</summary>
        public static bool IsTouchPreferred (nint settings)
        {
            int* supportedModes = stackalloc int[] { kModeMouse, kModeTouch };
            int preferredMode = kModeMouse;
            var getPreferredInteractionMode = (delegate* unmanaged[Stdcall]<nint, uint, int*, int*, int>)VtableSlot(settings, kGetPreferredInteractionModeSlot);
            int hr = getPreferredInteractionMode(settings, 2, supportedModes, &preferredMode);
            return (hr >= 0) && (preferredMode == kModeTouch);
        }

        /// <summary>Hands <paramref name="handler"/> (from <see cref="CreateHandlerPointer"/>) to Windows' change event. Windows takes its own reference.</summary>
        public static int AddChangedHandler (nint settings, nint handler, out long token)
        {
            long registration = 0;
            var add = (delegate* unmanaged[Stdcall]<nint, nint, long*, int>)VtableSlot(settings, kAddPreferredInteractionModeChangedSlot);
            int hr = add(settings, handler, &registration);
            token = registration;
            return hr;
        }

        /// <summary>Takes a handler back from Windows' change event. Call it on the window's UI thread, once Windows' first notice has been delivered (see WindowsTouchPreferenceSource.Dispose).</summary>
        public static int RemoveChangedHandler (nint settings, long token)
        {
            var remove = (delegate* unmanaged[Stdcall]<nint, long, int>)VtableSlot(settings, kRemovePreferredInteractionModeChangedSlot);
            return remove(settings, token);
        }

        /// <summary>
        /// A pointer to <paramref name="handler"/> as the change handler interface, the way Windows is handed it and calls
        /// it. The caller owns one reference, and releases it with <see cref="Marshal.Release"/>. Zero when it can't be made.
        /// </summary>
        public static nint CreateHandlerPointer (IPreferredInteractionModeChangedHandler handler)
        {
            nint unknown = g_comWrappers.GetOrCreateComInterfaceForObject(handler, CreateComInterfaceFlags.None);
            try
            {
                return (Marshal.QueryInterface(unknown, in kPreferredInteractionModeChangedHandlerIid, out nint handlerPointer) >= 0) ? handlerPointer : IntPtr.Zero;
            }
            finally
            {
                Marshal.Release(unknown);
            }
        }

        // ===========[ Helper Methods ]===================================
        private static void* VtableSlot (nint comObject, int slot) => (*(void***)comObject)[slot];

        private static nint LogFailure (string step, int hr)
        {
            Logger.LogInfo($"[TOUCH-PREFERENCE] Windows touch preference unavailable, failed {step} (0x{hr:X8})");
            return IntPtr.Zero;
        }

        [LibraryImport("combase.dll", StringMarshalling = StringMarshalling.Utf16)]
        private static partial int WindowsCreateString (string sourceString, uint length, out nint hstring);

        [LibraryImport("combase.dll")]
        private static partial int WindowsDeleteString (nint hstring);

        [LibraryImport("combase.dll")]
        private static partial int RoGetActivationFactory (nint activatableClassId, in Guid iid, out nint factory);
    }

    /// <summary>TypedEventHandler&lt;UIViewSettings, object&gt;, which PreferredInteractionModeChanged calls.</summary>
    [GeneratedComInterface]
    [Guid("BF3E2A14-6FCE-5BB3-838E-DE9A00E11AAD")]
    internal partial interface IPreferredInteractionModeChangedHandler
    {
        [PreserveSig]
        int Invoke (nint sender, nint args);
    }

    /// <summary>IAgileObject: says an object can be called from any thread, for whatever asks through RoGetAgileReference.</summary>
    [GeneratedComInterface]
    [Guid("94EA2B94-E9CC-49E0-C0FF-EE64CA8F5B90")]
    internal partial interface IAgileObject
    {
    }
}
