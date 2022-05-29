namespace AJut.OS.Windows
{
    using System;
    using System.IO;

    /// <summary>
    /// Utilities for establishing locations in the appdata folder
    /// </summary>
    public static class AppDataHelper
    {
        public static readonly string kAppDataRootPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        /// <summary>
        /// Returns a path relative to the app data root folder
        /// </summary>
        public static string EstablishAppDataLocation(params string[] pathParts)
        {
            string appDataLocation = Path.Combine(kAppDataRootPath, Path.Combine(pathParts));
            Directory.CreateDirectory(appDataLocation);
            return appDataLocation;
        }
    }
}
