namespace AJut.UX.Docking
{
    public readonly struct DockZoneSize
    {
        public static readonly DockZoneSize Empty = default;

        public DockZoneSize (double width, double height)
        {
            this.Width = width;
            this.Height = height;
        }

        public double Width { get; }
        public double Height { get; }
        public bool IsEmpty => this.Width == 0.0 && this.Height == 0.0;
    }
}
