namespace AJut.UX.Controls
{
    using System.Windows;
    using System.Windows.Controls;
    using DPUtils = AJut.UX.DPUtils<DockDropSiblingSplitSpannerWidget>;

    public class DockDropSiblingSplitSpannerWidget : Control
    {
        static DockDropSiblingSplitSpannerWidget ()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(DockDropSiblingSplitSpannerWidget), new FrameworkPropertyMetadata(typeof(DockDropSiblingSplitSpannerWidget)));
        }

        public static readonly DependencyProperty DirectionProperty = DPUtils.Register(_ => _.Direction);
        public eDockInsertionDirection Direction
        {
            get => (eDockInsertionDirection)this.GetValue(DirectionProperty);
            set => this.SetValue(DirectionProperty, value);
        }

    }
}
