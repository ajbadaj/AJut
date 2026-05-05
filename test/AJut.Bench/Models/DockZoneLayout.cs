namespace AJut.Bench.Models
{
    using System.Collections.Generic;
    using AJut.Text.AJson;

    // Docking layout shape - nested 3-4 deep with mixed primitives + arrays. Models the AJut docking
    // layout serialization case. [OptimizeAJson] is on the V2-source-gen variants below; the
    // unannotated types here back the V1 + V2-reflection rows.
    public enum eDockOrientation
    {
        Horizontal,
        Vertical,
    }

    public class DockZoneLayout
    {
        public string ZoneId { get; set; }
        public eDockOrientation Orientation { get; set; }
        public double SplitRatio { get; set; }
        public List<DockZoneLayout> Children { get; set; } = new List<DockZoneLayout>();
        public List<TabInfo> Tabs { get; set; } = new List<TabInfo>();
    }

    public class TabInfo
    {
        public string Title { get; set; }
        public string ContentTypeId { get; set; }
        public bool IsActive { get; set; }
        public Dictionary<string, string> Properties { get; set; } = new Dictionary<string, string>();
    }

    [OptimizeAJson]
    public class DockZoneLayoutOptimized
    {
        public string ZoneId { get; set; }
        public eDockOrientation Orientation { get; set; }
        public double SplitRatio { get; set; }
        public List<DockZoneLayoutOptimized> Children { get; set; } = new List<DockZoneLayoutOptimized>();
        public List<TabInfoOptimized> Tabs { get; set; } = new List<TabInfoOptimized>();
    }

    [OptimizeAJson]
    public class TabInfoOptimized
    {
        public string Title { get; set; }
        public string ContentTypeId { get; set; }
        public bool IsActive { get; set; }
        public Dictionary<string, string> Properties { get; set; } = new Dictionary<string, string>();
    }
}
