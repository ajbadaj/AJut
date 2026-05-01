namespace AJut.UX.Docking
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using AJut;
    using AJut.IO;
    using AJut.Text.AJson.Legacy;
    using AJut.TypeManagement;
    using AJut.UX;
    using Microsoft.UI.Windowing;
    using Microsoft.UI.Xaml;

    // ===========[ DockingSerialization ]=====================================
    // WinUI3-specific serialization for DockingManager.
    // Conceptually identical to the WPF version (save/load zone tree + content
    // state as JSON) but uses WinUI3 window types for ancillary window data.
    //
    // The zone tree itself is serialized via the shared DockZoneSerializationData
    // type in AJut.UX - no WinUI3-specific code in those structures.
    //
    // WindowStorageData stores position/size as integers (AppWindow.Position /
    // AppWindow.Size use Windows.Graphics.PointInt32 / SizeInt32) and boolean
    // flags for maximized / fullscreen state.

    internal static class DockingSerialization
    {
        public static string CreateApplicationPath (string uniqueManagerId)
        {
            uniqueManagerId = PathHelpers.SanitizeFileName(uniqueManagerId);
            return Path.Combine(ApplicationUtilities.AppDataRoot, "Layouts", $"{uniqueManagerId}.layout");
        }

        public static bool SerializeStateTo (string filePath, DockingManager manager)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(filePath));
            }
            catch (Exception ex)
            {
                Logger.LogError($"Docking serialization: failed to create layout directory for '{filePath ?? "<null>"}'", ex);
                return false;
            }

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
                Logger.LogError($"Failed to write layout to '{filePath ?? "<null>"}'", ex);
                return false;
            }
        }

        public static bool ResetFromState (string filePath, DockingManager manager)
        {
            try
            {
                Logger.LogInfo($"Docking: Loading state from '{filePath ?? "<null>"}'");

                if (!Uri.TryCreate(filePath, UriKind.RelativeOrAbsolute, out var uri))
                {
                    Logger.LogError($"Docking: Invalid layout path '{filePath ?? "<null>"}'");
                    return false;
                }

                using Stream stream = FileHelpers.GetStreamFromLocalFileUri(uri);
                bool result = ResetFromState(stream, manager);
                Logger.LogInfo($"Docking: Load state complete, result={result}");
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
                    Logger.LogError("Dock Loading Error: File stream was null");
                    return false;
                }

                DockZoneViewModel defaultLoadZone = manager.GetFallbackRootZone();
                if (defaultLoadZone == null)
                {
                    Logger.LogError("Dock Loading Error: No root zones are registered");
                    return false;
                }

                var json = JsonHelper.ParseFile(stream);
                if (json.HasErrors)
                {
                    Logger.LogError("Dock Loading Error: Failed to parse layout JSON");
                    Logger.LogError(json.GetErrorReport());
                    return false;
                }

                var state = JsonHelper.BuildObjectForJson<SerializationInfo>(json.Data);
                if (state == null)
                {
                    Logger.LogError("Dock Loading Error: Failed to deserialize SerializationInfo");
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
                            Logger.LogError($"Dock Loading Error: No zone for group id '{groupId}' - falling back to default zone");
                            zone = defaultLoadZone;
                        }

                        _ApplyZoneData(zone, state.Core.ZoneInfoByRoot[groupId]);
                    }

                    foreach (var windowData in state.Ancillary ?? Array.Empty<WindowStorageData>())
                    {
                        if (windowData.State == null)
                        {
                            continue;
                        }

                        var zone = new DockZoneViewModel(manager);
                        _ApplyZoneData(zone, windowData.State);

                        var origin = new Windows.Foundation.Point(windowData.WindowX, windowData.WindowY);
                        var size = new Windows.Foundation.Size(windowData.WindowWidth, windowData.WindowHeight);
                        var windowResult = manager.CreateAndStockTearoffWindow(zone, origin, size);

                        if (windowResult.HasErrors)
                        {
                            Logger.LogError("Dock Loading Error: Failed to create ancillary window from state");
                            Logger.LogError(windowResult.GetErrorReport());
                            continue;
                        }

                        if (windowData.IsFullscreen || windowData.IsMaximized)
                        {
                            var appWindow = windowResult.Value.AppWindow;
                            if (windowData.IsFullscreen)
                            {
                                appWindow.SetPresenter(AppWindowPresenterKind.FullScreen);
                            }
                            else
                            {
                                (appWindow.Presenter as OverlappedPresenter)?.Maximize();
                            }
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

            // ----- Local helpers -----

            void _ApplyZoneData (DockZoneViewModel zone, DockZoneSerializationData data)
            {
                zone.StorePassAlongUISize(data.SizeOnParent);
                zone.BuildFromState(data, _BuildDisplayAdapter);
            }

            DockingContentAdapterModel _BuildDisplayAdapter (DockDisplaySerializationData displayData)
            {
                IDockableDisplayElement display = null;

                if (TypeIdRegistrar.TryGetType(displayData.TypeId, out Type type))
                {
                    display = manager.BuildNewDisplayElement(type);
                }
                else
                {
                    type = Type.GetType(displayData.TypeId);
                    if (type != null)
                    {
                        display = manager.BuildNewDisplayElement(type);
                    }
                }

                if (display == null)
                {
                    Logger.LogError($"Dock Loading Error: Cannot find type for id '{displayData.TypeId}'");
                    return null;
                }

                if (displayData.State != null)
                {
                    display.ApplyState(displayData.State);
                }

                return display.DockingAdapter;
            }
        }

        // ===========[ Data Types ]============================================

        public class CoreStorageData
        {
            public Dictionary<string, DockZoneSerializationData> ZoneInfoByRoot { get; set; } = new();
        }

        public class WindowStorageData
        {
            public int WindowX { get; set; }
            public int WindowY { get; set; }
            public int WindowWidth { get; set; }
            public int WindowHeight { get; set; }
            public bool IsMaximized { get; set; }
            public bool IsFullscreen { get; set; }
            public DockZoneSerializationData State { get; set; }
        }

        public class SerializationInfo
        {
            public CoreStorageData Core { get; set; }
            public WindowStorageData[] Ancillary { get; set; }
        }
    }
}
