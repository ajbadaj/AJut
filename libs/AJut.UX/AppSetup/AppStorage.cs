namespace AJut.UX
{
    using System;
    using System.IO;
    using System.Linq;
    using AJut.OS.Windows;

    // ===========[ AppStorage ]=================================================
    // Where an app may write: the one app data root everything that writes files builds on
    // (docking layouts, logs, settings), the same idea on every UX package. Each package's setup
    // works out its platform's root and hands it to SetAppDataRoot:
    //  - WPF and WinUI3: a special folder with the project name appended (DetermineSpecialFolderRoot),
    //    or the config's override taken verbatim.
    //  - macOS: ~/Library/Application Support/<app id>. iOS: the app container's
    //    Library/Application Support. Android: the app's private files folder.
    // An app's install folder is never the root, since it is read only on mobile.
    public static class AppStorage
    {
        // ===========[ Properties ]============================================
        /// <summary>
        /// The folder this app may write to, set by its UX package's setup; null until setup runs. Build paths under it with
        /// <see cref="BuildAppDataPath"/>.
        /// </summary>
        public static string? AppDataRoot { get; private set; }

        // ===========[ Public Interface Methods ]==============================
        /// <summary>Sets <see cref="AppDataRoot"/>. A UX package's setup calls this once it has worked out its platform's root.</summary>
        public static void SetAppDataRoot (string appDataRoot)
        {
            AppDataRoot = appDataRoot;
        }

        /// <summary>A path under <see cref="AppDataRoot"/>, from the given parts</summary>
        public static string BuildAppDataPath (params string[] pathParts)
        {
            return Path.Combine(AppDataRoot!, Path.Combine(pathParts));
        }

        /// <summary>
        /// Works out a Windows style app data root from the setup config, ensuring the folder exists either way: the
        /// config's override verbatim when it has one, otherwise its special folder (or <paramref name="defaultStorageRoot"/>,
        /// which differs per stack) with the storage root project name appended, unless package virtualization already
        /// isolates the special folder.
        /// </summary>
        public static string DetermineSpecialFolderRoot (ApplicationSetupConfig config, Environment.SpecialFolder defaultStorageRoot)
        {
            // An override is the root, exactly as given. Appending the project name the way the special folder path
            //  below does would defeat the entire point of setting one - you would end up in a subfolder of the root
            //  you were handed, and quietly disagree with whoever handed it to you.
            if (config.StorageRootOverride != null)
            {
                Directory.CreateDirectory(config.StorageRootOverride);
                return config.StorageRootOverride;
            }

            Environment.SpecialFolder specialFolder = config.ApplicationStorageRoot ?? defaultStorageRoot;

            // Packaging alone does not isolate the special folder. GetFolderPath hands a packaged app the plain shared
            //  folder, and only MSIX write virtualization makes it app specific, so the project folder goes on unless the
            //  config says virtualization is on. Nothing detects that - the config states it.
            if (config.StorageRootIsolation == eStorageRootIsolation.PackageVirtualization)
            {
                return WindowsEnvironmentHelper.EstablishSpecialFolderLocation(specialFolder);
            }

            return WindowsEnvironmentHelper.EstablishSpecialFolderLocation(specialFolder, config.StorageRootProjectName);
        }

        /// <summary>
        /// Works out where logs go under the app data root: a folder per project whenever that root is shared, straight
        /// into Logs otherwise.
        /// </summary>
        public static string DetermineLogsDirectory (string appDataRoot, string projectName, string? sharedProjectName, eStorageRootIsolation storageRootIsolation)
        {
            // Projects sharing a root each get their own logs folder. A virtualized package root is shared by everything
            //  in the package (and by every app on the machine if the setting is wrong), so it gets one too.
            if ((sharedProjectName != null)
                || (storageRootIsolation == eStorageRootIsolation.PackageVirtualization))
            {
                return Path.Combine(appDataRoot, "Logs", projectName);
            }

            return Path.Combine(appDataRoot, "Logs");
        }

        /// <summary>Deletes every file in <paramref name="logsDirectory"/> last written longer than <paramref name="age"/> ago</summary>
        public static void PurgeLogsOlderThan (TimeSpan age, string logsDirectory)
        {
            var logsFolder = new DirectoryInfo(logsDirectory);
            foreach (FileInfo file in logsFolder.EnumerateFiles().ToList())
            {
                if (DateTime.Now - file.LastWriteTime > age)
                {
                    file.Delete();
                }
            }
        }
    }
}
