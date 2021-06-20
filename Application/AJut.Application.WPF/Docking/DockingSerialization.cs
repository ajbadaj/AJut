namespace AJut.Application.Docking
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using AJut.Application.Controls;
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

        public static bool SerializeStateTo(string filePath, IList<DockZone> zones)
        {
//#error TODO: Consider maybe do some name matching for the root level's sake
            ZoneData[] state = zones.Select(z => z.GenerateSerializationState()).ToArray();
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
            catch { return false; }
        }

        public static bool ResetFromState (string filePath, IList<DockZone> zones)
        {
            throw new NotImplementedException();
        }

        public class ZoneData
        {
            public ZoneData AnteriorZone { get; set; }
            public ZoneData PosteriorZone { get; set; }
            public double AnteriorSize { get; set; }
            public eDockOrientation DockOrientation { get; set; }
            public DisplayData[] DisplayState { get; set; }
        }

        public class DisplayData
        {
            public string TypeId { get; set; }
            public object State { get; set; }
        }

    }
}
