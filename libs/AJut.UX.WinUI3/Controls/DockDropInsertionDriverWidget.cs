namespace AJut.UX.Controls
{
    using AJut.UX.Docking;
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;
    using Microsoft.UI.Xaml.Media;
    using Windows.UI;
    using DPUtils = AJut.UX.DPUtils<DockDropInsertionDriverWidget>;

    // ===========[ DockDropInsertionDriverWidget ]===========================
    // Directional drop-target arrow shown as part of the DockZone drop overlay.
    // Five of these are arranged in a 3×3 grid (Left/Top/Right/Bottom/Center)
    // centered over a DockZone when DockZone.IsDirectDropTarget = true during
    // a drag operation.
    //
    // Visual is code-built in OnApplyTemplate:
    //   PART_Root (Border) - background/border react to IsEngaged
    //   Child TextBlock    - Segoe Fluent Icons glyph keyed to Direction
    //
    // Template part:
    //   PART_Root - Border whose Background/BorderBrush change on IsEngaged

    [TemplatePart(Name = nameof(PART_Root), Type = typeof(Border))]
    public class DockDropInsertionDriverWidget : Control
    {
        // ===========[ Constants ]============================================
        private static readonly SolidColorBrush kNormalBackground  = new SolidColorBrush(Color.FromArgb(0xFF, 0xDD, 0xDD, 0xDD));
        private static readonly SolidColorBrush kNormalBorder       = new SolidColorBrush(Color.FromArgb(0xFF, 0x33, 0x33, 0x33));
        private static readonly SolidColorBrush kEngagedBackground  = new SolidColorBrush(Color.FromArgb(0xFF, 0x33, 0x99, 0xE4));
        private static readonly SolidColorBrush kEngagedBorder      = new SolidColorBrush(Color.FromArgb(0xFF, 0x13, 0x39, 0x54));
        private static readonly SolidColorBrush kNormalGlyph        = new SolidColorBrush(Color.FromArgb(0xFF, 0x00, 0x00, 0x00));
        private static readonly SolidColorBrush kEngagedGlyph       = new SolidColorBrush(Color.FromArgb(0xFF, 0x13, 0x39, 0x54));

        // ===========[ Fields ]===============================================
        private Border PART_Root;
        private TextBlock m_glyphBlock;

        // ===========[ Construction ]=========================================
        public DockDropInsertionDriverWidget ()
        {
            this.DefaultStyleKey = typeof(DockDropInsertionDriverWidget);
        }

        // ===========[ Template ]=============================================
        protected override void OnApplyTemplate ()
        {
            base.OnApplyTemplate();
            PART_Root = this.GetTemplateChild(nameof(PART_Root)) as Border;
            if (PART_Root == null)
            {
                return;
            }

            m_glyphBlock = new TextBlock
            {
                FontFamily = new FontFamily("Segoe Fluent Icons"),
                FontSize = 14,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Padding = new Thickness(2),
            };

            PART_Root.BorderThickness = new Thickness(1);
            PART_Root.Child = m_glyphBlock;

            // Apply initial state from current DP values
            this.ApplyEngagedVisual(this.IsEngaged);
            this.ApplyDirectionVisual(this.Direction);
        }

        // ===========[ Dependency Properties ]================================

        public static readonly DependencyProperty DirectionProperty = DPUtils.Register(
            _ => _.Direction,
            (d, e) => d.ApplyDirectionVisual(e.NewValue)
        );
        public eDockInsertionDirection Direction
        {
            get => (eDockInsertionDirection)this.GetValue(DirectionProperty);
            set => this.SetValue(DirectionProperty, value);
        }

        public static readonly DependencyProperty IsEngagedProperty = DPUtils.Register(
            _ => _.IsEngaged,
            (d, e) => d.ApplyEngagedVisual(e.NewValue)
        );
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

        // ===========[ Private Helpers ]======================================

        private void ApplyDirectionVisual (eDockInsertionDirection direction)
        {
            if (m_glyphBlock == null)
            {
                return;
            }

            m_glyphBlock.Text = direction switch
            {
                eDockInsertionDirection.Left              => "\uE76B", // ChevronLeft
                eDockInsertionDirection.Top               => "\uE70E", // ChevronUp
                eDockInsertionDirection.Right             => "\uE76C", // ChevronRight
                eDockInsertionDirection.Bottom            => "\uE70D", // ChevronDown
                eDockInsertionDirection.AddToTabbedDisplay => "\uE710", // Add / plus
                _ => "\uE76C",
            };
        }

        private void ApplyEngagedVisual (bool engaged)
        {
            if (PART_Root == null)
            {
                return;
            }

            PART_Root.Background  = engaged ? kEngagedBackground : kNormalBackground;
            PART_Root.BorderBrush = engaged ? kEngagedBorder     : kNormalBorder;

            if (m_glyphBlock != null)
            {
                m_glyphBlock.Foreground = engaged ? kEngagedGlyph : kNormalGlyph;
            }
        }
    }
}
