namespace AJut.UX.Docking
{
    using System.Collections.Generic;
    using AJut.Text.AJson;

    /// <summary>Platform-agnostic serialization state for a <see cref="DockZoneViewModel"/> and its subtree.</summary>
    [OptimizeAJson]
    public class DockZoneSerializationData
    {
        public DockZoneSerializationData () { }

        public DockZoneSerializationData (eDockOrientation orientation)
        {
            this.Orientation = orientation;
            this.ChildZones = new List<DockZoneSerializationData>();
        }

        public List<DockZoneSerializationData> ChildZones { get; set; }
        public DockZoneSize SizeOnParent { get; set; }
        public eDockOrientation Orientation { get; set; }
        public DockDisplaySerializationData[] DisplayState { get; set; }
        public int SelectedIndex { get; set; }
    }

    /// <summary>Platform-agnostic serialization state for a single docked display element.</summary>
    [OptimizeAJson]
    public class DockDisplaySerializationData
    {
        public string TypeId { get; set; }

        /// <summary>
        /// Panel-defined state bag. Decorated with [JsonRuntimeTypeEval] so AJson embeds
        /// "__type" during serialization and reconstructs the concrete type on load via
        /// TypeIdRegistrar, avoiding the object-typed property deserialization problem
        /// (where BuildObjectForJson would otherwise return an empty new object()).
        /// </summary>
        [JsonRuntimeTypeEval]
        public object State { get; set; }
    }
}
