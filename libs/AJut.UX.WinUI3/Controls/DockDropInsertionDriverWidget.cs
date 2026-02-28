namespace AJut.UX.Controls
{
    using AJut.UX.Docking;
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;
    using Microsoft.UI.Xaml.Media;
    using System;
    using DPUtils = AJut.UX.DPUtils<DockDropInsertionDriverWidget>;

    // ===========[ DockDropInsertionDriverWidget ]===========================
    // WinUI3-specific: no WPF equivalent.
    // Directional drop-target arrow shown as part of the DockZone drop overlay.
    // Five of these are arranged in a 3x3 grid (Left/Top/Right/Bottom/Center)
    // centered over a DockZone when DockZone.IsDirectDropTarget = true during
    // a drag operation.
    //
    // Template parts:
    //   PART_Root  - Border; Background and BorderBrush driven by EngagementStates VSM
    //   PART_Glyph - Segoe Fluent Icons TextBlock; Text set from Direction DP in code
    //
    // VSM groups:
    //   EngagementStates - Idle / Engaged

    [TemplatePart(Name = nameof(PART_Root), Type = typeof(Border))]
    [TemplatePart(Name = nameof(PART_Glyph), Type = typeof(TextBlock))]
    [TemplateVisualState(Name = "Idle", GroupName = "EngagementStates")]
    [TemplateVisualState(Name = "Engaged", GroupName = "EngagementStates")]
    public class DockDropInsertionDriverWidget : Control
    {
        // ===========[ Fields ]===============================================
        private Border PART_Root;
        private TextBlock PART_Glyph;

        // ===========[ Setup/Construction/Teardown ]===========================
        public DockDropInsertionDriverWidget()
        {
            this.DefaultStyleKey = typeof(DockDropInsertionDriverWidget);
        }

        protected override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            this.PART_Root = this.GetTemplateChild(nameof(PART_Root)) as Border;
            this.PART_Glyph = this.GetTemplateChild(nameof(PART_Glyph)) as TextBlock;

            this.ApplyDirectionGlyph(this.Direction);
            VisualStateManager.GoToState(this, this.IsEngaged ? "Engaged" : "Idle", false);
        }

        // ===========[ Dependency Properties ]================================

        public static readonly DependencyProperty DirectionProperty = DPUtils.Register(_ => _.Direction, (d, e) => d.ApplyDirectionGlyph(e.NewValue));
        public eDockInsertionDirection Direction
        {
            get => (eDockInsertionDirection)this.GetValue(DirectionProperty);
            set => this.SetValue(DirectionProperty, value);
        }

        public static readonly DependencyProperty IsEngagedProperty = DPUtils.Register(_ => _.IsEngaged, (d, e) => d.OnIsEngagedChanged(e));
        public bool IsEngaged
        {
            get => (bool)this.GetValue(IsEngagedProperty);
            set => this.SetValue(IsEngagedProperty, value);
        }
        private void OnIsEngagedChanged(DependencyPropertyChangedEventArgs<bool> e)
        {
            VisualStateManager.GoToState(this, (bool)e.NewValue ? "Engaged" : "Idle", true);
        }


        public static readonly DependencyProperty InsertionZoneProperty = DPUtils.Register(_ => _.InsertionZone);
        public DockZone InsertionZone
        {
            get => (DockZone)this.GetValue(InsertionZoneProperty);
            set => this.SetValue(InsertionZoneProperty, value);
        }

        //public static readonly DependencyProperty BorderHighlightedBrushProperty = DPUtils.Register(_ => _.BorderHighlightedBrush);
        //public Brush BorderHighlightedBrush
        //{
        //    get => (Brush)this.GetValue(BorderHighlightedBrushProperty);
        //    set => this.SetValue(BorderHighlightedBrushProperty, value);
        //}

        //public static readonly DependencyProperty BackgroundHighlightedBrushProperty = DPUtils.Register(_ => _.BackgroundHighlightedBrush);
        //public Brush BackgroundHighlightedBrush
        //{
        //    get => (Brush)this.GetValue(BackgroundHighlightedBrushProperty);
        //    set => this.SetValue(BackgroundHighlightedBrushProperty, value);
        //}

        //public static readonly DependencyProperty GlyphHighlightedBrushProperty = DPUtils.Register(_ => _.GlyphHighlightedBrush);
        //public Brush GlyphHighlightedBrush
        //{
        //    get => (Brush)this.GetValue(GlyphHighlightedBrushProperty);
        //    set => this.SetValue(GlyphHighlightedBrushProperty, value);
        //}

        // ===========[ Private Helpers ]======================================

        private void ApplyDirectionGlyph(eDockInsertionDirection direction)
        {
            if (PART_Glyph == null)
            {
                return;
            }

            this.PART_Glyph.Text = direction switch
            {
                eDockInsertionDirection.Left => "\uE76B", // ChevronLeft
                eDockInsertionDirection.Top => "\uE70E", // ChevronUp
                eDockInsertionDirection.Right => "\uE76C", // ChevronRight
                eDockInsertionDirection.Bottom => "\uE70D", // ChevronDown
                eDockInsertionDirection.AddToTabbedDisplay => "\uE710", // Add / plus
                _ => "\uE76C",
            };
        }
    }
}
