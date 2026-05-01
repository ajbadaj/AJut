namespace AJut.Bench.Models
{
    using System.Collections.Generic;

    // Docking layout shape - nested 3-4 deep with mixed primitives + arrays. Models the AJut docking
    // layout serialization case.
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
}
