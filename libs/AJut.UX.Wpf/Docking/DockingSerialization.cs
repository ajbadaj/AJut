namespace AJut.UX.Docking
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Windows;
    using AJut.UX.Controls;
    using AJut.IO;
    using AJut.Text.AJson;
    using AJut.TypeManagement;

    internal static class DockingSerialization
    {
        public static string CreateApplicationPath (string uniqueManagerId)
        {
            uniqueManagerId = PathHelpers.SanitizeFileName(uniqueManagerId);
            return Path.Combine(ApplicationUtilities.AppDataRoot, "Layouts", $"{uniqueManagerId}.layout");
        }

        public static IDockableDisplayElement BuildDisplayElement (DockingManager manager, DisplayData s)
        {
            IDockableDisplayElement display = null;
            if (TypeIdRegistrar.TryGetType(s.TypeId, out Type type))
            {
                display = manager.BuildNewDisplayElement(type);
            }
            else
            {
                type = Type.GetType(s.TypeId);
                if (type != null)
                {
                    display = manager.BuildNewDisplayElement(type);
                }
            }

            if (display != null)
            {
                display.ApplyState(s.State);
            }

            return display;
        }

        public static bool SerializeStateTo(string filePath, DockingManager manager)
        {
            var state = new SerializationInfo
            {
                Core = manager.BuildSerializationInfoForRoot(),
                Ancillary = manager.BuildSerializationInfoForAncillaryWindows().ToArray()
            };

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

        public static bool ResetFromState (string filePath, DockingManager manager)
        {
            throw new NotImplementedException();
        }

        public class CoreStorageData
        {
            public Dictionary<string, ZoneData> ZoneInfoByRoot { get; set; } = new Dictionary<string, ZoneData>();
        }

        public class WindowStorageData
        {
            public Size WindowSize { get; set; }
            public WindowState WindowState { get; set; }
            public ZoneData State { get; set; }
            public Point WindowLocation { get; set; }
            public bool WindowIsFullscreened { get; set; }
        }

        public class ZoneData
        {
            public ZoneData () { }
            internal ZoneData (eDockOrientation orientation)
            {
                this.Orientation = orientation;
                this.ChildZones = new List<ZoneData>();
            }

            public List<ZoneData> ChildZones { get; set; }
            public double SizeOnParent { get; set; }
            public eDockOrientation Orientation { get; set; }
            public DisplayData[] DisplayState { get; set; }
        }

        public class DisplayData
        {
            public string TypeId { get; set; }
            public object State { get; set; }
        }


        public class SerializationInfo
        {
            public CoreStorageData Core { get; set; }
            public WindowStorageData[] Ancillary { get; set; }

        }

    }
}
