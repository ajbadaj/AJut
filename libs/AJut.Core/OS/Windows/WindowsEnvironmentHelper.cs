namespace AJut.OS.Windows
{
    using System;
    using System.IO;

    public static class WindowsEnvironmentHelper
    {
        /// <summary>
        /// Returns a path relative to a special folder, and ensures all subdirectories exist
        /// </summary>
        public static string EstablishSpecialFolderLocation (Environment.SpecialFolder where, params string[] pathParts)
        {
            string appDataRootPath = Environment.GetFolderPath(where);
            string appDataLocation = Path.Combine(appDataRootPath, Path.Combine(pathParts));
            Directory.CreateDirectory(appDataLocation);
            return appDataLocation;
        }
    }
}
