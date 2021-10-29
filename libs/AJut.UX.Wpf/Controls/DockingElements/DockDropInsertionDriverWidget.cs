namespace AJut.UX.Controls
{
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Media;
    using DPUtils = AJut.UX.DPUtils<DockDropInsertionDriverWidget>;

    public enum eDockInsertionDirection
    {
        Left,
        Top,
        Right,
        Bottom,
        AddToTabbedDisplay,
    }

    public class DockDropInsertionDriverWidget : Control
    {
        static DockDropInsertionDriverWidget ()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(DockDropInsertionDriverWidget), new FrameworkPropertyMetadata(typeof(DockDropInsertionDriverWidget)));
            IsHitTestVisibleProperty.OverrideMetadata(typeof(DockDropInsertionDriverWidget), new FrameworkPropertyMetadata(false));
        }

        //internal DockDropInsertionDriverWidget (DockZone insertionSource, eDockInsertionDirection direction)
        //{
        //    this.InsertionSource = insertionSource;
        //    this.Direction = direction;
        //}

        //public DockZone InsertionSource { get; }
        //public eDockInsertionDirection Direction { get; }

        public static readonly DependencyProperty IsEngagedProperty = DPUtils.Register(_ => _.IsEngaged);
        public bool IsEngaged
        {
            get => (bool)this.GetValue(IsEngagedProperty);
            set => this.SetValue(IsEngagedProperty, value);
        }

        public static readonly DependencyProperty InsertionZoneProperty = DPUtils.Register(_ => _.InsertionZone);
        public DockZone InsertionZone
        {
            get => (DockZone)this.GetValue(InsertionZoneProperty);
            set => this.SetValue(InsertionZoneProperty, value);
        }

        public static readonly DependencyProperty DirectionProperty = DPUtils.Register(_ => _.Direction);
        public eDockInsertionDirection Direction
        {
            get => (eDockInsertionDirection)this.GetValue(DirectionProperty);
            set => this.SetValue(DirectionProperty, value);
        }

        public static readonly DependencyProperty GlyphBrushProperty = DPUtils.Register(_ => _.GlyphBrush);
        public Brush GlyphBrush
        {
            get => (Brush)this.GetValue(GlyphBrushProperty);
            set => this.SetValue(GlyphBrushProperty, value);
        }

        public static readonly DependencyProperty GlyphHighlightedBrushProperty = DPUtils.Register(_ => _.GlyphHighlightedBrush);
        public Brush GlyphHighlightedBrush
        {
            get => (Brush)this.GetValue(GlyphHighlightedBrushProperty);
            set => this.SetValue(GlyphHighlightedBrushProperty, value);
        }

        public static readonly DependencyProperty GlyphBorderBrushProperty = DPUtils.Register(_ => _.GlyphBorderBrush);
        public Brush GlyphBorderBrush
        {
            get => (Brush)this.GetValue(GlyphBorderBrushProperty);
            set => this.SetValue(GlyphBorderBrushProperty, value);
        }

        public static readonly DependencyProperty GlyphHighlightedBorderBrushProperty = DPUtils.Register(_ => _.GlyphHighlightedBorderBrush);
        public Brush GlyphHighlightedBorderBrush
        {
            get => (Brush)this.GetValue(GlyphHighlightedBorderBrushProperty);
            set => this.SetValue(GlyphHighlightedBorderBrushProperty, value);
        }

        public static readonly DependencyProperty BackgroundHighlightedProperty = DPUtils.Register(_ => _.BackgroundHighlighted);
        public Brush BackgroundHighlighted
        {
            get => (Brush)this.GetValue(BackgroundHighlightedProperty);
            set => this.SetValue(BackgroundHighlightedProperty, value);
        }

        public static readonly DependencyProperty BorderHighlightedProperty = DPUtils.Register(_ => _.BorderHighlighted);
        public Brush BorderHighlighted
        {
            get => (Brush)this.GetValue(BorderHighlightedProperty);
            set => this.SetValue(BorderHighlightedProperty, value);
        }
    }
}
