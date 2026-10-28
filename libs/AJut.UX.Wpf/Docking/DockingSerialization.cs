namespace AJut.UX.Docking
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Windows;
    using AJut.IO;
    using AJut.Text.AJson;

    // ===========[ DockingSerialization ]=======================================
    // WPF's own docking layout file: the root window's zones by group id ("Core"), then each tearoff window's
    // place and size in device independent pixels, its WindowState and full screen flag, and its zones
    // ("Ancillary"). A layout with no tearoffs is the same shape as the shared DockLayoutFile; the tearoff
    // entries are where the two differ. DockingManager reads and writes this one until WPF moves onto the
    // shared format.
    internal static class DockingSerialization
    {
        // ===========[ Public Interface Methods ]==============================
        /// <summary>Writes <paramref name="state"/> to <paramref name="filePath"/>, creating its folder; false (and logged) when it can't be written</summary>
        public static bool WriteFile (string filePath, SerializationInfo state)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(filePath));

            var json = JsonHelper.BuildJsonForObject(state);
            if (json.HasErrors)
            {
                Logger.LogError(json.GetErrorReport());
                return false;
            }

            try
            {
                File.WriteAllText(filePath, json.ToString());
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to write to '{filePath ?? "<null>"}', exception encountered.", ex);
                return false;
            }
        }

        /// <summary>Reads a layout file, or gives null (and logs why) when the path is invalid or the file can't be read</summary>
        public static SerializationInfo ReadFile (string filePath)
        {
            try
            {
                if (!Uri.TryCreate(filePath, UriKind.RelativeOrAbsolute, out var uri))
                {
                    Logger.LogError($"Invalid path provided - unable to interpret, path was '{filePath ?? "<null>"}'");
                    return null;
                }

                using (Stream stream = FileHelpers.GetStreamFromLocalFileUri(uri))
                {
                    return Read(stream);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex);
                return null;
            }
        }

        /// <summary>Parses a layout file's contents, or gives null (and logs why)</summary>
        public static SerializationInfo Read (Stream stream)
        {
            if (stream == null)
            {
                Logger.LogError("Dock Loading Error: File stream indicated was null");
                return null;
            }

            var json = JsonHelper.ParseFile(stream);
            if (json.HasErrors)
            {
                Logger.LogError("Dock Loading Error: Failed to read docking manager state");
                Logger.LogError(json.GetErrorReport());
                return null;
            }

            var state = JsonHelper.BuildObjectForJson<SerializationInfo>(json.Data);
            if (state == null)
            {
                Logger.LogError("Dock Loading Error: Failed to read docking manager state");
            }

            return state;
        }

        // ===========[ Subclasses/structs ]====================================
        [OptimizeAJson]
        public class CoreStorageData
        {
            public Dictionary<string, DockZoneSerializationData> ZoneInfoByRoot { get; set; } = new();
        }

        [OptimizeAJson]
        public class WindowStorageData
        {
            public Size WindowSize { get; set; }
            public WindowState WindowState { get; set; }
            public DockZoneSerializationData State { get; set; }
            public Point WindowLocation { get; set; }
            public bool WindowIsFullscreened { get; set; }
        }

        [OptimizeAJson]
        public class SerializationInfo
        {
            public CoreStorageData Core { get; set; }
            public WindowStorageData[] Ancillary { get; set; }
        }
    }
}
