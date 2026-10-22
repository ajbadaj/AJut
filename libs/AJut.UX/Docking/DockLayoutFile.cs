namespace AJut.UX.Docking
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using AJut.IO;
    using AJut.Text.AJson;

    // ===========[ DockLayoutFile ]=============================================
    // A docking layout file: the root window's zones by group id ("Core"), then each tearoff
    // window's place, size, maximized or full screen state, and zones ("Ancillary"). The zone
    // trees are DockZoneSerializationData. Positions and sizes are in the units the toolkit places
    // windows by; WinUI3 wrote this shape first, in physical pixels from the top left.
    internal static class DockLayoutFile
    {
        // ===========[ Public Interface Methods ]==============================
        /// <summary>Writes <paramref name="contents"/> to <paramref name="filePath"/>, creating its folder; false (and logged) on any failure</summary>
        public static bool Write (string? filePath, Contents contents)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Docking serialization: failed to create layout directory for '{filePath ?? "<null>"}'", ex);
                return false;
            }

            var json = JsonHelper.BuildJsonForObject(contents);
            if (json.HasErrors)
            {
                Logger.LogError(json.GetErrorReport());
                return false;
            }

            try
            {
                File.WriteAllText(filePath!, json.ToString());
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to write layout to '{filePath ?? "<null>"}'", ex);
                return false;
            }
        }

        /// <summary>Opens a layout file for reading, or gives null (and logs) when the path is not a valid local path</summary>
        public static Stream? OpenRead (string? filePath)
        {
            if (!Uri.TryCreate(filePath, UriKind.RelativeOrAbsolute, out Uri? uri))
            {
                Logger.LogError($"Docking: Invalid layout path '{filePath ?? "<null>"}'");
                return null;
            }

            return FileHelpers.GetStreamFromLocalFileUri(uri);
        }

        /// <summary>Parses a layout file's contents, or gives null (and logs why)</summary>
        public static Contents? Read (Stream stream)
        {
            var json = JsonHelper.ParseFile(stream);
            if (json.HasErrors)
            {
                Logger.LogError("Dock Loading Error: Failed to parse layout JSON");
                Logger.LogError(json.GetErrorReport());
                return null;
            }

            var contents = JsonHelper.BuildObjectForJson<Contents>(json.Data);
            if (contents == null)
            {
                Logger.LogError("Dock Loading Error: Failed to deserialize SerializationInfo");
                return null;
            }

            return contents;
        }

        // ===========[ Subclasses/structs ]====================================
        /// <summary>A whole layout file</summary>
        [OptimizeAJson]
        public class Contents
        {
            public CoreLayout? Core { get; set; }
            public TearoffLayout[]? Ancillary { get; set; }
        }

        /// <summary>The root window's zone trees, by the group id each root zone was registered under</summary>
        [OptimizeAJson]
        public class CoreLayout
        {
            public Dictionary<string, DockZoneSerializationData> ZoneInfoByRoot { get; set; } = new Dictionary<string, DockZoneSerializationData>();
        }

        /// <summary>One tearoff window: its place, size and state, and its zone tree</summary>
        [OptimizeAJson]
        public class TearoffLayout
        {
            public int WindowX { get; set; }
            public int WindowY { get; set; }
            public int WindowWidth { get; set; }
            public int WindowHeight { get; set; }
            public bool IsMaximized { get; set; }
            public bool IsFullscreen { get; set; }
            public DockZoneSerializationData? State { get; set; }
        }
    }
}
