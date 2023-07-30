namespace AJut.UX.Docking
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Windows;
    using AJut.IO;
    using AJut.Text.AJson;
    using AJut.TypeManagement;
    using AJut.UX.AttachedProperties;

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

        public static bool SerializeStateTo (string filePath, DockingManager manager)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(filePath));

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
            try
            {
                Logger.LogInfo($"Docking: Loading state for file '{filePath ?? "<null>"}'");
                bool result = false;
                if (Uri.TryCreate(filePath, UriKind.RelativeOrAbsolute, out var uri))
                {
                    using (Stream stream = FileHelpers.GetStreamForFileUri(uri))
                    {
                        result = ResetFromState(stream, manager);
                    }
                }
                else
                {
                    Logger.LogError($"Invalid path provided - unable to interpret, path was '{filePath ?? "<null>"}'");
                    result = false;
                }

                Logger.LogInfo($"Docking: Loading state for file '{filePath ?? "<null>"}' complete.");
                return result;
            }
            catch
            {
                return false;
            }
        }

        public static bool ResetFromState (Stream stream, DockingManager manager)
        {
            try
            {
                manager.IsLoadingFromLayout = true;

                if (stream == null)
                {
                    Logger.LogError("Dock Loading Error: File stream indicated was null");
                    return false;
                }

                DockZoneViewModel defaultLoadZone = manager.GetFallbackRootZone();
                if (defaultLoadZone == null)
                {
                    Logger.LogError("Dock Loading Error: Load from state failed as NO ROOT ZONES EXIST for provided manager");
                    return false;
                }

                var json = JsonHelper.ParseFile(stream);
                if (json.HasErrors)
                {
                    Logger.LogError("Dock Loading Error: Failed to read docking manager state");
                    Logger.LogError(json.GetErrorReport());
                    return false;
                }

                var state = JsonHelper.BuildObjectForJson<SerializationInfo>(json.Data);
                if (state == null)
                {
                    Logger.LogError("Dock Loading Error: Failed to read docking manager state");
                    return false;
                }

                manager.ClearForLoadingFromState();
                if (state.Core != null)
                {
                    foreach (string groupId in state.Core.ZoneInfoByRoot.Keys)
                    {
                        DockZoneViewModel zone = manager.GetRootZone(groupId);
                        if (zone == null)
                        {
                            Logger.LogError($"Dock Loading Error: No zone for group id '{groupId}' found, possibly one of that name used to be registered but now is not - falling back to default to avoid data loss.");
                            zone = defaultLoadZone;
                        }

                        _SetZoneDataToZone(zone, state.Core.ZoneInfoByRoot[groupId]);
                    }

                    foreach (var ancillaryWindowData in state.Ancillary)
                    {
                        var zone = new DockZoneViewModel(manager);
                        _SetZoneDataToZone(zone, ancillaryWindowData.State);
                        var windowResult = manager.CreateAndStockTearoffWindow(zone, ancillaryWindowData.WindowLocation, ancillaryWindowData.WindowSize);
                        if (windowResult.HasErrors)
                        {
                            Logger.LogError("Dock Loading Error: Failed to intepret ancillary window state for docking");
                            Logger.LogError(windowResult.GetErrorReport());
                            continue;
                        }

                        if (ancillaryWindowData.WindowIsFullscreened)
                        {
                            WindowXTA.SetIsFullscreen(windowResult.Value, true);
                        }
                        else
                        {
                            windowResult.Value.WindowState = ancillaryWindowData.WindowState;
                        }
                    }
                }

                manager.CleanZoneLayoutHierarchies();
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError("Error in docking serialization", ex);
                return false;
            }
            finally
            {
                manager.IsLoadingFromLayout = false;
            }

            void _SetZoneDataToZone (DockZoneViewModel _zone, ZoneData _data)
            {
                List<Size> sizes = _data.ChildZones.Select(d => d.SizeOnParent).ToList();
                _zone.Configure(_data.Orientation);
                _zone.StorePassAlongUISize(_data.SizeOnParent);

                if (_data.ChildZones.IsNotNullOrEmpty())
                {
                    foreach (ZoneData childData in _data.ChildZones)
                    {
                        var child = new DockZoneViewModel(manager);
                        _SetZoneDataToZone(child, childData);
                        _zone.AddChild(child);
                    }
                }
                else if (_data.DisplayState.IsNotNullOrEmpty())
                {
                    foreach (DisplayData data in _data.DisplayState)
                    {
                        IDockableDisplayElement display = manager.BuildNewDisplayElement(data.TypeId);
                        if (display != null)
                        {
                            if (data.State != null)
                            {
                                display.ApplyState(data.State);
                            }

                            _zone.AddDockedContent(display.DockingAdapter);
                        }
                        else
                        {
                            Logger.LogError($"Dock Loading Error: Failed to intepret zone display element with type id: '{data.TypeId}'");
                        }
                    }

                    _zone.SelectedIndex = _data.SelectedIndex;
                }
            }
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
            public Size SizeOnParent { get; set; }
            public eDockOrientation Orientation { get; set; }
            public DisplayData[] DisplayState { get; set; }
            public int SelectedIndex { get; set; }
        }

        public class DisplayData
        {
            public string TypeId { get; set; }

            [JsonRuntimeTypeEval]
            public object State { get; set; }
        }

        public class SerializationInfo
        {
            public CoreStorageData Core { get; set; }
            public WindowStorageData[] Ancillary { get; set; }

        }
    }
}
