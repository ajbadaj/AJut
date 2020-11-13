namespace AJut.Application
{
#if false
    /*
Looks like I can remake this with:
    dotnet add package Microsoft.Win32.Registry
        This contains types as:

        Microsoft.Win32.RegistryKey
        Microsoft.Win32.Registry
        Microsoft.Win32.RegistryValueKind
        Microsoft.Win32.RegistryHive
        Microsoft.Win32.RegistryView

    using (RegistryKey key = Registry.CurrentUser.CreateSubKey("--PATH--"))
    {
        key.SetValue("key", "value");
    }
    */


    using Microsoft.Win32;
    using System;
    using System.Management;
    using System.Security.Principal;

    public delegate void HandleRegistryValueChange(string keyPath, string valueName);

    public class RegistryHelper
    {
        public static string Read(string keyPath, string valueName)
        {
            try
            {
                return (string)Registry.GetValue(keyPath, valueName, String.Empty);
            }
            catch (Exception exc)
            {
                Console.WriteLine("Failed to get root path from registry!\nException: {0}", exc);
                return String.Empty;
            }
        }
        public static void WatchRegistry(string originalHive, string originalKeyPath, string originalValueName, HandleRegistryValueChange changeHandler)
        {
            try
            {
                string hive = originalHive;
                string keyPath = originalKeyPath.Replace("\\", "\\\\");
                string valueName = originalValueName;

                var currentUser = WindowsIdentity.GetCurrent();

                if (hive == "HKEY_CURRENT_USER")
                {
                    keyPath = keyPath.Replace(hive, currentUser.User.Value);
                    hive = "HKEY_USERS";
                }
                else
                {
                    keyPath = keyPath.Replace(hive, String.Empty).TrimStart('\\');
                }
                string queryString = string.Format(
                "SELECT * FROM RegistryValueChangeEvent WHERE Hive='{0}' AND KeyPath='{1}' AND ValueName='{2}'",
                hive, keyPath, valueName);

                var query = new WqlEventQuery(queryString);
                var watcher = new ManagementEventWatcher(query);
                watcher.EventArrived += (sender, args) => changeHandler(originalKeyPath, originalValueName);
                watcher.Start();
            }
            catch (Exception exc)
            {
                Console.WriteLine(exc);
            }
        }
    }

#endif
}