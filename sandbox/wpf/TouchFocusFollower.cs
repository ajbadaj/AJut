namespace TheAJutShowRoom
{
    using System;
    using System.Windows;
    using AJut;
    using AJut.UX.Theming;

    /// <summary>
    /// The showroom's "follow Windows touch" switch: while it's on, a TouchPreferenceWatcher on the main window sets the
    /// theme's touch focus from Windows' touch preference, the way an app opts in to following it.
    /// </summary>
    public sealed class TouchFocusFollower : NotifyPropertyChanged
    {
        private readonly AppThemeManager m_themes;
        private TouchPreferenceWatcher? m_watcher;

        public TouchFocusFollower (AppThemeManager themes)
        {
            m_themes = themes;
        }

        /// <summary>Whether the theme's touch focus follows Windows' touch preference for the main window</summary>
        public bool FollowsWindows
        {
            get => m_watcher != null;
            set
            {
                if (value == (m_watcher != null))
                {
                    return;
                }

                if (value)
                {
                    this.StartFollowing();
                }
                else
                {
                    this.StopFollowing();
                }

                this.RaisePropertiesChanged(nameof(FollowsWindows), nameof(WindowsPreferenceText));
            }
        }

        /// <summary>What Windows says, for the drawer</summary>
        public string WindowsPreferenceText
        {
            get
            {
                if (m_watcher == null)
                {
                    return "Not following Windows";
                }

                if (!m_watcher.IsAvailable)
                {
                    return "Windows offers no touch preference before Windows 11 24H2";
                }

                return m_watcher.IsTouchPreferred ? "Windows prefers touch" : "Windows prefers the mouse";
            }
        }

        private void StartFollowing ()
        {
            m_watcher = new TouchPreferenceWatcher(Application.Current.MainWindow);
            m_watcher.IsTouchPreferredChanged += this.OnWindowsTouchPreferredChanged;
            if (m_watcher.IsAvailable)
            {
                m_themes.IsTouchFocused = m_watcher.IsTouchPreferred;
            }
        }

        private void StopFollowing ()
        {
            m_watcher!.IsTouchPreferredChanged -= this.OnWindowsTouchPreferredChanged;
            m_watcher.Dispose();
            m_watcher = null;
        }

        private void OnWindowsTouchPreferredChanged (object? sender, EventArgs<bool> e)
        {
            m_themes.IsTouchFocused = e.Value;
            this.RaisePropertiesChanged(nameof(WindowsPreferenceText));
        }
    }
}
