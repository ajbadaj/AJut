namespace AJut.Application
{
    using System;
    using System.IO;

    public static class AppDataHelper
    {
        public static readonly string kAppDataRootPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        public static string EstablishAppDataLocation(params string[] pathParts)
        {
            string appDataLocation = Path.Combine(kAppDataRootPath, Path.Combine(pathParts));
            Directory.CreateDirectory(appDataLocation);
            return appDataLocation;
        }
    }
}
