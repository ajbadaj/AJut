namespace AJutShowRoomAndroid
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using AJut.UX;
    using AJut.UX.CodeBinding;
    using AJut.UX.Controls;
    using AJut.UX.Helpers;
    using AJut.UX.Theming;
    using Android.App;
    using Android.Content;
    using Android.Content.Res;
    using Android.OS;
    using Android.Views;
    using Android.Widget;
    using Android.Window;
    using eThemeMode = ShowroomSettings.eThemeMode;

    /// <summary>
    /// The showroom shell: one activity holding a stack of pages. Opening a page pushes it, back pops it, and the
    /// page being left has everything it bound undone before the next one shows. The page title and the way back
    /// live in the activity's action bar, and the theme bar under it picks the look on every page.
    /// </summary>
    [Activity(Name = "dev.ajut.showroom.android.MainActivity", Label = "AJut Showroom", MainLauncher = true, Exported = true)]
    public sealed class MainActivity : Activity
    {
        private const string kSavedPageKey = "page";

        private readonly List<ShowroomPage> m_pages = new List<ShowroomPage>();
        private FrameLayout? m_content;
        private BindingSet? m_pageBinder;
        private BindingSet? m_themeBarBinder;
        private BackCallback? m_backCallback;
        private bool m_isBackCallbackRegistered;
        private eThemeMode m_appliedThemeMode = eThemeMode.System;

        // ===========[ Setup/Construction/Teardown ]===================================
        protected override void AttachBaseContext (Context? @base)
        {
            base.AttachBaseContext(@base);

            // Forcing dark or light on this activity alone. Android picks light or dark resources (and so the dark or
            // light half of the DayNight theme, the action bar and the stock widgets) from the configuration's night
            // mode, and AJut's ThemeColors reads the same bit. AppCompat's AppCompatDelegate.setDefaultNightMode does
            // this for AppCompat activities, but this is a plain Activity with no AppCompat. UiModeManager's
            // SetApplicationNightMode works without it, but only from Android 12 (this app runs from 10), and it
            // persists across launches, which would stop "adb shell cmd uimode night" from picking the look for a
            // screenshot run.
            //
            // So the night mode goes in as an override configuration instead, the mechanism AppCompat itself falls back
            // on (ContextThemeWrapper.applyOverrideConfiguration). It has to be applied here, before anything reads
            // the activity's resources or theme, so a switch takes effect through a recreate, which every theme
            // change here does anyway. A configuration made with new Configuration() starts with every field
            // undefined, so it overrides the night bits and nothing else. System applies nothing, and the device's own
            // light or dark comes through, a change to it included.
            // https://developer.android.com/reference/android/view/ContextThemeWrapper#applyOverrideConfiguration(android.content.res.Configuration)
            m_appliedThemeMode = ShowroomSettings.Instance.ThemeMode;
            UiMode? nightMode = m_appliedThemeMode switch
            {
                eThemeMode.Dark => UiMode.NightYes,
                eThemeMode.Light => UiMode.NightNo,
                _ => null,
            };

            if (nightMode != null)
            {
                this.ApplyOverrideConfiguration(new Configuration { UiMode = nightMode.Value });
            }
        }

        protected override void OnCreate (Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            // Once per process: a recreate runs OnCreate again, and the setup ignores the second call
            ApplicationUtilities.RunOnetimeSetup(this, new ApplicationSetupConfig("Android_ShowRoom")
            {
                SharedProjectName = "AJut.ShowRoom",
                OnExceptionReceived = LogException,
            });

            // Only on a fresh launch: a recreate keeps the launch intent, and the extras would undo the switch that
            // asked for it
            if (savedInstanceState == null)
            {
                this.ApplyLaunchExtras();
            }

            this.SetContentView(Resource.Layout.activity_main);
            View root = this.FindViewById(Resource.Id.shell_root)!;
            root.SetOnApplyWindowInsetsListener(new SystemBarsPadding());
            m_content = this.FindViewById<FrameLayout>(Resource.Id.shell_content)!;

            // Themed, the theme bar and the pages sit on the AJut panel background, the way a themed WinUI3 window does.
            if (ThemeColors.IsThemeLoaded
                && AJutPalette.For(ThemeColors.IsDark(this), ThemeColors.IsTouchFocused).TryGetColor("AJut_Brush_PanelBackground", out ColorArgb panel))
            {
                root.SetBackgroundColor(panel.ToAndroidColor());
            }

            if (OperatingSystem.IsAndroidVersionAtLeast(33))
            {
                m_backCallback = new BackCallback(this);
            }

            this.BindThemeBar();
            this.Navigate(new HomePage());

            // The page that was showing before a recreate comes back, and on a fresh launch the string extra "page"
            // opens that page straight away, so a page can be brought up and captured through adb alone:
            // adb shell am start -n <activity> --es page BrushButton
            string? startPage = (savedInstanceState != null)
                ? savedInstanceState.GetString(kSavedPageKey)
                : this.Intent?.GetStringExtra("page");
            if (startPage != null)
            {
                ShowroomMap.TryOpen(this, startPage);
            }

            // A light or dark asked for by the launch extras is only applied as the activity attaches, so it takes one
            // recreate to show it.
            if (ShowroomSettings.Instance.ThemeMode != m_appliedThemeMode)
            {
                this.Recreate();
            }
        }

        // A theme switch recreates the activity, so the page showing comes back with it. The map is home, then an area,
        // then a page, so the top page's title is enough to rebuild the way to it.
        protected override void OnSaveInstanceState (Bundle outState)
        {
            base.OnSaveInstanceState(outState);
            if (m_pages.Count > 1)
            {
                outState.PutString(kSavedPageKey, m_pages[m_pages.Count - 1].Title);
            }
        }

        protected override void OnDestroy ()
        {
            this.UnbindCurrentPage();
            m_themeBarBinder?.Dispose();
            m_themeBarBinder = null;
            this.SetBackCallbackRegistered(false);
            base.OnDestroy();
        }

        // ===========[ Public Interface Methods ]===================================
        /// <summary>Opens <paramref name="page"/> on top of the current one.</summary>
        public void Navigate (ShowroomPage page)
        {
            this.UnbindCurrentPage();
            m_pages.Add(page);
            this.ShowTopPage();
        }

        /// <summary>Returns to the page under the current one. Returns false at the home page, which has nothing under it.</summary>
        public bool GoBack ()
        {
            if (m_pages.Count <= 1)
            {
                return false;
            }

            this.UnbindCurrentPage();
            m_pages.RemoveAt(m_pages.Count - 1);
            this.ShowTopPage();
            return true;
        }

        // Before Android 13 the system back gesture arrives here; from 13 on it goes to the registered callback.
        public override void OnBackPressed ()
        {
            if (!this.GoBack())
            {
#pragma warning disable CA1422 // The obsolete path is the point: this override only runs where it is not obsolete
                base.OnBackPressed();
#pragma warning restore CA1422
            }
        }

        // The action bar's up arrow.
        public override bool OnOptionsItemSelected (IMenuItem item)
        {
            if (item.ItemId == Android.Resource.Id.Home)
            {
                return this.GoBack();
            }

            return base.OnOptionsItemSelected(item);
        }

        // ===========[ Helper Methods ]===================================
        // Setup has logged it already; returning false lets the app go down the way it would have
        private static bool LogException (UnhandledExceptionReport report) => false;

        // Launching with string extras picks the look first, so any look can be captured through adb alone, as in
        // adb shell am start -n <activity> --es theme ajut --es mode dark --es touch off
        //  - theme: ajut or plain, the AJut theme or plain defaults
        //  - mode: dark, light or system
        //  - touch: on or off, the AJut theme's touch variant or its desktop palette
        private void ApplyLaunchExtras ()
        {
            ShowroomSettings settings = ShowroomSettings.Instance;
            string? theme = this.Intent?.GetStringExtra("theme");
            if (theme != null)
            {
                settings.UseAJutTheme = string.Equals(theme, "ajut", StringComparison.OrdinalIgnoreCase);
            }

            string? mode = this.Intent?.GetStringExtra("mode");
            if ((mode != null) && Enum.TryParse(mode, ignoreCase: true, out eThemeMode parsedMode))
            {
                settings.ThemeMode = parsedMode;
            }

            string? touch = this.Intent?.GetStringExtra("touch");
            if (touch != null)
            {
                // A touch choice only applies under Dark or Light, so with no mode asked for it keeps the light or dark
                // the device is showing, and the touch choice takes effect as it did before there was a mode.
                if ((mode == null) && (settings.ThemeMode == eThemeMode.System))
                {
                    settings.ThemeMode = ThemeColors.IsDark(this) ? eThemeMode.Dark : eThemeMode.Light;
                }

                settings.IsTouchFocused = string.Equals(touch, "on", StringComparison.OrdinalIgnoreCase);
            }
        }

        private void BindThemeBar ()
        {
            ShowroomSettings settings = ShowroomSettings.Instance;
            m_themeBarBinder = new BindingSet();
            m_themeBarBinder.BindSelectedValue(
                this.FindViewById<EnumToggleStrip>(Resource.Id.theme_bar_mode)!,
                settings, nameof(ShowroomSettings.ThemeMode),
                () => settings.ThemeMode,
                this.SwitchThemeMode
            );

            var touch = this.FindViewById<Switch>(Resource.Id.theme_bar_touch)!;
            m_themeBarBinder.BindChecked(
                touch,
                settings, nameof(ShowroomSettings.IsTouchFocused),
                () => settings.IsTouchFocused,
                this.SwitchTouchFocused
            );
            m_themeBarBinder.BindEnabled(touch, settings, nameof(ShowroomSettings.CanChooseTouch), () => settings.CanChooseTouch);

            m_themeBarBinder.BindChecked(
                this.FindViewById<Switch>(Resource.Id.theme_bar_ajut)!,
                settings, nameof(ShowroomSettings.UseAJutTheme),
                () => settings.UseAJutTheme,
                this.SwitchUseAJutTheme
            );
        }

        // Each switch recreates the activity, since views read the theme as they are made
        private void SwitchThemeMode (eThemeMode mode)
        {
            if (ShowroomSettings.Instance.ThemeMode != mode)
            {
                ShowroomSettings.Instance.ThemeMode = mode;
                this.Recreate();
            }
        }

        private void SwitchTouchFocused (bool isTouchFocused)
        {
            ShowroomSettings settings = ShowroomSettings.Instance;
            bool before = settings.IsTouchFocused;
            settings.IsTouchFocused = isTouchFocused;
            if (settings.IsTouchFocused != before)
            {
                this.Recreate();
            }
        }

        private void SwitchUseAJutTheme (bool useAJutTheme)
        {
            if (ShowroomSettings.Instance.UseAJutTheme != useAJutTheme)
            {
                ShowroomSettings.Instance.UseAJutTheme = useAJutTheme;
                this.Recreate();
            }
        }

        private void ShowTopPage ()
        {
            ShowroomPage page = m_pages[m_pages.Count - 1];
            LayoutInflater inflater = this.LayoutInflater;
            m_content!.RemoveAllViews();

            View pageView = inflater.Inflate(page.LayoutId, m_content, false)!;
            if (page.MarkupSourceId != 0)
            {
                View frame = inflater.Inflate(Resource.Layout.page_frame, m_content, false)!;
                frame.FindViewById<FrameLayout>(Resource.Id.page_content)!.AddView(pageView);
                frame.FindViewById<TextView>(Resource.Id.page_markup)!.Text = this.ReadRawText(page.MarkupSourceId);
                m_content.AddView(frame);
            }
            else
            {
                m_content.AddView(pageView);
            }

            m_pageBinder = new BindingSet();
            page.Bind(pageView, this, m_pageBinder);

            bool canGoBack = m_pages.Count > 1;
            if (this.ActionBar != null)
            {
                this.ActionBar.Title = page.Title;
                this.ActionBar.SetDisplayHomeAsUpEnabled(canGoBack);
            }

            this.SetBackCallbackRegistered(canGoBack);
        }

        private void UnbindCurrentPage ()
        {
            if (m_pages.Count > 0)
            {
                m_pages[m_pages.Count - 1].Unbind();
            }

            m_pageBinder?.Dispose();
            m_pageBinder = null;
        }

        // Registered only while there is a page to go back to, so back at the home page leaves the app as usual.
        private void SetBackCallbackRegistered (bool shouldRegister)
        {
            if (!OperatingSystem.IsAndroidVersionAtLeast(33) || (m_backCallback == null) || (shouldRegister == m_isBackCallbackRegistered))
            {
                return;
            }

            if (shouldRegister)
            {
                this.OnBackInvokedDispatcher.RegisterOnBackInvokedCallback(0, m_backCallback);
            }
            else
            {
                this.OnBackInvokedDispatcher.UnregisterOnBackInvokedCallback(m_backCallback);
            }

            m_isBackCallbackRegistered = shouldRegister;
        }

        private string ReadRawText (int rawResourceId)
        {
            using Stream stream = this.Resources!.OpenRawResource(rawResourceId);
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd().TrimEnd();
        }

        // ===========[ Subclasses/structs ]===================================
        private sealed class BackCallback : Java.Lang.Object, IOnBackInvokedCallback
        {
            private readonly MainActivity m_owner;

            public BackCallback (MainActivity owner)
            {
                m_owner = owner;
            }

            public void OnBackInvoked () => m_owner.GoBack();
        }

        // From Android 15 an app draws edge to edge whether it asks to or not, so the shell pads itself in from
        // the system bars it overlaps (the action bar already sits below the status bar). Before that the window
        // does it and this sees no insets.
        private sealed class SystemBarsPadding : Java.Lang.Object, View.IOnApplyWindowInsetsListener
        {
            public WindowInsets OnApplyWindowInsets (View view, WindowInsets insets)
            {
                if (OperatingSystem.IsAndroidVersionAtLeast(30))
                {
                    Android.Graphics.Insets bars = insets.GetInsets(WindowInsets.Type.SystemBars());
                    view.SetPadding(bars.Left, bars.Top, bars.Right, bars.Bottom);
                    return WindowInsets.Consumed!;
                }

                return insets;
            }
        }
    }
}
