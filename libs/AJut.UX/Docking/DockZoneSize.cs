namespace AJut.UX.Docking
{
    using AJut.Text.AJson;

    /// <summary>A zone's size on its parent, as a docking layout file keeps it</summary>
    [OptimizeAJson]
    public struct DockZoneSize
    {
        public static readonly DockZoneSize Empty = default;

        public DockZoneSize (double width, double height)
        {
            this.Width = width;
            this.Height = height;
        }

        public double Width { get; set; }
        public double Height { get; set; }
        public bool IsEmpty => this.Width == 0.0 && this.Height == 0.0;
    }
}
