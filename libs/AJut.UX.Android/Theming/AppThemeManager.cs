namespace AJut.UX.Theming
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using AJut;
    using Android.App;
    using Android.Content;
    using Android.Content.Res;
    using Android.OS;
    using Android.Views;

    // ===========[ AppThemeManager ]============================================
    // Light, dark or follow the OS for an Android app, on AJut.UX's AppThemeResolver (the same
    // resolver the WinUI3, WPF and AppKit AppThemeManagers wrap). The resolver decides which theme is
    // in effect; this class reads the OS theme, keeps the choice in the app's shared preferences, and
    // applies it to each activity.
    //
    // Android picks light or dark resources (values-night, the dark half of a DayNight theme, and the
    // stock widgets that follow it) from the night bits of a context's configuration, and AJut's
    // ThemeColors reads the same bits. So a theme is applied by putting the night bits into an
    // activity's configuration as an override configuration, which is the route AppCompat's own night
    // mode takes for an activity. AJut.UX.Android takes no package beyond AJut's own, so this does it
    // directly rather than through AppCompat.
    // https://developer.android.com/reference/android/view/ContextThemeWrapper#applyOverrideConfiguration(android.content.res.Configuration)
    //
    // UiModeManager.SetApplicationNightMode looks like the simpler route and is not used: it only exists
    // from Android 12 (this package runs from Android 8), and the system keeps it across launches, which
    // takes the choice out of the app's hands.
    //
    // An override has to go in before anything reads the activity's resources, and only once, so it is
    // applied from the activity's AttachBaseContext (ApplyTo), and a later change reaches an activity by
    // recreating it, the same as any other Android configuration change does. The manager remembers what
    // it gave each activity until that activity is destroyed, and recreates the ones a new choice
    // changes. Under UseSameAsOS an activity gets no override at all, so the device's own light or dark
    // comes through, and Android recreates the activity on an OS switch by itself.
    //
    // The OS theme is the application context's night bits, which no activity's override touches,
    // followed through the application's configuration changes.
    //
    // Each activity it themes also has its stock widgets (check boxes, radio buttons, switches and
    // drop-downs) given the AJut palette as it starts, through StockControlTint, which does nothing
    // with no AJut theme loaded.
    public sealed class AppThemeManager : IDisposable
    {
        private const string kPreferencesName = "AJut_AppThemeManager";
        private const string kStoreKey = "AJut_AppThemeConfiguration";

        private readonly Application m_application;
        private readonly OSThemeSource m_osSource;
        private readonly AppThemeResolver m_resolver;
        private readonly ActivityWatcher m_activityWatcher;
        private readonly List<AppliedActivity> m_appliedActivities = new List<AppliedActivity>();
        private bool m_isDisposed;

        // ===========[ Setup/Construction/Teardown ]===========================
        /// <summary>
        /// Creates the manager for the app <paramref name="context"/> belongs to. Make it before the first activity is
        /// created (in an Application subclass's OnCreate, or in the first activity's AttachBaseContext from the context
        /// handed in), and keep it for the life of the app. The choice is loaded from the app's shared preferences when
        /// <paramref name="persistInSharedPreferences"/> is set and one was saved, otherwise <paramref name="fallback"/>
        /// is used.
        /// </summary>
        public AppThemeManager (Context context, eAppThemeConfiguration fallback = eAppThemeConfiguration.UseSameAsOS, bool persistInSharedPreferences = true)
        {
            m_application = (context.ApplicationContext as Application) ?? throw new ArgumentException("The context belongs to no application", nameof(context));
            m_osSource = new OSThemeSource(m_application);
            m_resolver = new AppThemeResolver(m_osSource, persistInSharedPreferences ? new SharedPreferencesStore(m_application) : null, fallback);
            m_resolver.ThemeChanged += this.OnResolverThemeChanged;
            m_resolver.PropertyChanged += this.OnResolverPropertyChanged;

            m_activityWatcher = new ActivityWatcher(this);
            m_application.RegisterActivityLifecycleCallbacks(m_activityWatcher);
        }

        /// <summary>Stops following the OS and the app's activities. Activities already given a theme keep it.</summary>
        public void Dispose ()
        {
            if (m_isDisposed)
            {
                return;
            }

            m_isDisposed = true;
            m_application.UnregisterActivityLifecycleCallbacks(m_activityWatcher);
            m_activityWatcher.Dispose();
            m_appliedActivities.Clear();

            m_resolver.ThemeChanged -= this.OnResolverThemeChanged;
            m_resolver.PropertyChanged -= this.OnResolverPropertyChanged;
            m_resolver.Dispose();
            m_osSource.Dispose();
        }

        // ===========[ Events ]================================================
        /// <summary>Raised when the theme in effect changes. Activities a new choice changes are recreated with it.</summary>
        public event EventHandler<EventArgs<eAppTheme>>? ThemeChanged;

        private void OnResolverThemeChanged (object? sender, EventArgs<eAppTheme> e)
        {
            this.ThemeChanged?.Invoke(this, e);
        }

        // A new choice can leave the theme as it was (Dark to UseSameAsOS while the device is dark), and still has to
        // reach each activity: from pinned to dark to following the device
        private void OnResolverPropertyChanged (object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(AppThemeResolver.ThemeConfiguration))
            {
                this.RecreateActivitiesGivenOtherThan(m_resolver.ThemeConfiguration);
            }
        }

        // ===========[ Properties ]============================================
        /// <summary>The resolver underneath, for binding to its INotifyPropertyChanged properties</summary>
        public AppThemeResolver Resolver => m_resolver;

        /// <summary>
        /// The user's choice: dark, light, or follow the OS. Setting it saves it, and recreates every activity the manager
        /// applied a different choice to.
        /// </summary>
        public eAppThemeConfiguration ThemeConfiguration
        {
            get => m_resolver.ThemeConfiguration;
            set => m_resolver.ThemeConfiguration = value;
        }

        /// <summary>The theme in effect</summary>
        public eAppTheme CurrentTheme => m_resolver.CurrentTheme;

        /// <summary>
        /// Whether the AJut palette is painted in its touch variant: lifted button surfaces and no button borders, for touch
        /// with no hover. Off by default, as on every stack. This is <see cref="ThemeColors.UsesTouchVariant"/>, which is
        /// where Android switches the palette; it is here too so an app sets it beside its theme choice, as on every other
        /// stack. Setting it to something new recreates every activity the manager applied a theme to, since Android views
        /// read their colors as they are made.
        /// </summary>
        public bool UsesTouchVariant
        {
            get => ThemeColors.UsesTouchVariant;
            set
            {
                if (ThemeColors.UsesTouchVariant == value)
                {
                    return;
                }

                ThemeColors.UsesTouchVariant = value;
                foreach (AppliedActivity applied in m_appliedActivities.ToArray())
                {
                    applied.Recreate();
                }
            }
        }

        // ===========[ Public Interface Methods ]==============================
        /// <summary>
        /// Gives <paramref name="target"/> the theme in effect. Call it from an activity's AttachBaseContext, right after
        /// base.AttachBaseContext and before anything reads the activity's resources (Android allows it only then, and only
        /// once). An activity is remembered until it is destroyed, and recreated whenever a new choice would give it
        /// something else. Any other ContextThemeWrapper (a dialog's themed context, say) is given the theme in effect now
        /// and is not followed after.
        /// </summary>
        /// <remarks>
        /// AJut's controls follow the night mode in any activity. Android's own widgets follow it only when the activity's
        /// theme is a DayNight one (Theme.DeviceDefault.DayNight, from Android 10), since a fixed light or dark theme picks
        /// the same look in both.
        /// </remarks>
        public void ApplyTo (ContextThemeWrapper target)
        {
            eAppThemeConfiguration configuration = m_resolver.ThemeConfiguration;
            UiMode? nightMode = ToNightMode(configuration);
            if (nightMode.HasValue)
            {
                // The override is a delta laid over the context's own configuration, so only the night bits are given.
                // Every other field is left undefined, which keeps the device's (a configuration made with
                // new Configuration() starts that way), apart from the font scale: some Android versions start it at 1,
                // which would pin the text size and stop the user's font size setting from reaching the app. AppCompat
                // clears it to 0 for the same reason before laying its own night mode over an activity.
                // https://android.googlesource.com/platform/frameworks/support/+/refs/heads/androidx-main/appcompat/appcompat/src/main/java/androidx/appcompat/app/AppCompatDelegateImpl.java
                target.ApplyOverrideConfiguration(new Configuration { FontScale = 0f, UiMode = nightMode.Value });
            }

            if (target is Activity activity)
            {
                this.Forget(activity);
                m_appliedActivities.Add(new AppliedActivity(activity, configuration));
            }
        }

        // ===========[ Helper Methods ]========================================
        // The night bits a choice puts into an activity's configuration; none under UseSameAsOS, which leaves the device's
        private static UiMode? ToNightMode (eAppThemeConfiguration configuration)
        {
            switch (configuration)
            {
                case eAppThemeConfiguration.Dark: return UiMode.NightYes;
                case eAppThemeConfiguration.Light: return UiMode.NightNo;
                default: return null;
            }
        }

        private static bool IsNight (Configuration? configuration)
        {
            return (configuration != null)
                && ((configuration.UiMode & UiMode.NightMask) == UiMode.NightYes);
        }

        private void RecreateActivitiesGivenOtherThan (eAppThemeConfiguration configuration)
        {
            foreach (AppliedActivity applied in m_appliedActivities.ToArray())
            {
                if (applied.Configuration != configuration)
                {
                    applied.Recreate();
                }
            }
        }

        private void Forget (Activity activity)
        {
            m_appliedActivities.RemoveAll(applied => ReferenceEquals(applied.Activity, activity));
        }

        // An activity this gave a theme to has its content by the time it starts, so its stock widgets take the AJut
        // palette then (StockControlTint, which does nothing with no theme loaded)
        private void OnActivityStarted (Activity activity)
        {
            if (m_appliedActivities.Exists(applied => ReferenceEquals(applied.Activity, activity)) && (activity.Window?.DecorView is View content))
            {
                StockControlTint.Apply(content);
            }
        }

        // ===========[ Subclasses/structs ]====================================
        // An activity the manager gave a theme to, and the choice it gave
        private sealed class AppliedActivity
        {
            private bool m_isRecreating;

            public AppliedActivity (Activity activity, eAppThemeConfiguration configuration)
            {
                this.Activity = activity;
                this.Configuration = configuration;
            }

            public Activity Activity { get; }

            public eAppThemeConfiguration Configuration { get; }

            // A recreate is asked for once: the activity is destroyed soon after, and its replacement applies the theme
            // in effect then, so a second change before that needs nothing more
            public void Recreate ()
            {
                if (m_isRecreating || this.Activity.IsFinishing || this.Activity.IsDestroyed)
                {
                    return;
                }

                m_isRecreating = true;
                this.Activity.Recreate();
            }
        }

        // The OS theme: the application's night bits, which no activity's override reaches, kept current from the
        // application's configuration changes (raised on the main thread, as IOSThemeSource asks)
        private sealed class OSThemeSource : IOSThemeSource, IDisposable
        {
            private readonly Application m_application;
            private ConfigurationWatcher? m_watcher;
            private EventHandler? m_osThemeChanged;
            private bool m_isDark;

            public OSThemeSource (Application application)
            {
                m_application = application;
                m_isDark = IsNight(application.Resources?.Configuration);
                m_watcher = new ConfigurationWatcher(this);
                m_application.RegisterComponentCallbacks(m_watcher);
            }

            public void Dispose ()
            {
                if (m_watcher != null)
                {
                    m_application.UnregisterComponentCallbacks(m_watcher);
                    m_watcher.Dispose();
                    m_watcher = null;
                }
            }

            public event EventHandler OSThemeChanged
            {
                add => m_osThemeChanged += value;
                remove => m_osThemeChanged -= value;
            }

            public eAppTheme GetOSTheme () => m_isDark ? eAppTheme.Dark : eAppTheme.Light;

            public void OnConfigurationChanged (Configuration newConfiguration)
            {
                bool isDark = IsNight(newConfiguration);
                if (isDark != m_isDark)
                {
                    m_isDark = isDark;
                    m_osThemeChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        // The application holds this for as long as it is registered, which is until its OSThemeSource is disposed
        private sealed class ConfigurationWatcher : Java.Lang.Object, IComponentCallbacks
        {
            private readonly OSThemeSource m_owner;

            public ConfigurationWatcher (OSThemeSource owner)
            {
                m_owner = owner;
            }

            public void OnConfigurationChanged (Configuration newConfig) => m_owner.OnConfigurationChanged(newConfig);

            public void OnLowMemory ()
            {
            }
        }

        // A destroyed activity is forgotten, so the manager never holds one past its life, and a started one has its stock
        // widgets tinted. The application holds this for as long as it is registered, which is until the manager is
        // disposed.
        private sealed class ActivityWatcher : Java.Lang.Object, Application.IActivityLifecycleCallbacks
        {
            private readonly AppThemeManager m_owner;

            public ActivityWatcher (AppThemeManager owner)
            {
                m_owner = owner;
            }

            public void OnActivityDestroyed (Activity activity) => m_owner.Forget(activity);

            public void OnActivityCreated (Activity activity, Bundle? savedInstanceState)
            {
            }

            public void OnActivityPaused (Activity activity)
            {
            }

            public void OnActivityResumed (Activity activity)
            {
            }

            public void OnActivitySaveInstanceState (Activity activity, Bundle outState)
            {
            }

            public void OnActivityStarted (Activity activity) => m_owner.OnActivityStarted(activity);

            public void OnActivityStopped (Activity activity)
            {
            }
        }

        // The choice under one key in the app's own shared preferences, written as the choice's name
        private sealed class SharedPreferencesStore : IAppThemeConfigurationStore
        {
            private readonly ISharedPreferences m_preferences;

            public SharedPreferencesStore (Context context)
            {
                m_preferences = context.GetSharedPreferences(kPreferencesName, FileCreationMode.Private)!;
            }

            public bool TryLoad (out eAppThemeConfiguration configuration)
            {
                string? stored = m_preferences.GetString(kStoreKey, null);
                if ((stored != null)
                    && Enum.TryParse(stored, out configuration)
                    && Enum.IsDefined(configuration))
                {
                    return true;
                }

                configuration = default;
                return false;
            }

            public void Save (eAppThemeConfiguration configuration)
            {
                m_preferences.Edit()!.PutString(kStoreKey, configuration.ToString())!.Apply();
            }
        }
    }
}
