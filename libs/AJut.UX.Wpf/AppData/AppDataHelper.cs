namespace AJut.OS.Windows
{
    using System;

    /// <summary>
    /// Utilities for establishing locations in the appdata folder
    /// </summary>
    public static class AppDataHelper
    {
        public static string EstablishAppDataLocation (params string[] pathParts)
        {
            return WindowsEnvironmentHelper.EstablishSpecialFolderLocation(Environment.SpecialFolder.ApplicationData, pathParts);
        }

        public static string EstablishLocalAppDataLocation (params string[] pathParts)
        {
            return WindowsEnvironmentHelper.EstablishSpecialFolderLocation(Environment.SpecialFolder.LocalApplicationData, pathParts);
        }
    }
}
