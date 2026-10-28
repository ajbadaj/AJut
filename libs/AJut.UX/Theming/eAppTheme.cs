namespace AJut.UX.Theming
{
    /// <summary>A theme an app can be in</summary>
    public enum eAppTheme
    {
        /// <summary>Light text on dark backgrounds</summary>
        Dark,

        /// <summary>Dark text on light backgrounds</summary>
        Light,
    }

    /// <summary>The theme a user picks: one of the themes, or whatever the OS is set to</summary>
    public enum eAppThemeConfiguration
    {
        /// <summary>Always dark</summary>
        Dark,

        /// <summary>Always light</summary>
        Light,

        /// <summary>Follows the OS's light or dark setting, including when it changes while the app runs</summary>
        UseSameAsOS,
    }
}
