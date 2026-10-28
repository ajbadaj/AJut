namespace AJut.UX.Controls
{
    using AJut.UX.Docking;
    using AJut.UX.Theming;
    using AppKit;
    using CoreGraphics;

    // ===========[ DockDropOverlayView ]========================================
    /// <summary>
    /// The drop overlay a <see cref="DockZoneView"/> shows while a docking drag is over it: five insertion widgets in the
    /// shared 84 by 60 grid (<see cref="DockDragRules"/>), Left, Top, Right and Bottom split the zone, and the center adds
    /// to its tabs. An empty zone shows only the center; a split shows the center dimmed and disabled, since it holds
    /// zones rather than panels.
    /// </summary>
    /// <remarks>
    /// The overlay is not hit-tested by AppKit: the docking manager's drag loop asks <see cref="FindWidgetAt"/> with a
    /// point in the overlay, which reads the grid through DockDragRules, so every stack agrees on where each widget is.
    /// </remarks>
    public sealed class DockDropOverlayView : ThemedView
    {
        private const double kDisabledAlpha = 0.5;

        private readonly InsertionWidget m_left;
        private readonly InsertionWidget m_top;
        private readonly InsertionWidget m_right;
        private readonly InsertionWidget m_bottom;
        private readonly InsertionWidget m_center;

        // ===========[ Setup/Construction/Teardown ]===========================
        /// <summary>Builds the overlay for a zone of <paramref name="zoneOrientation"/></summary>
        public DockDropOverlayView (eDockOrientation zoneOrientation)
        {
            double left = 0.0;
            double middleX = DockDragRules.kOverlayEdgeColumnWidth;
            double right = middleX + DockDragRules.kOverlayCenterColumnWidth;
            double top = 0.0;
            double middleY = DockDragRules.kOverlayEdgeRowHeight;
            double bottom = middleY + DockDragRules.kOverlayCenterRowHeight;

            m_left = this.AddWidget(eDockInsertionDirection.Left, left, middleY, DockDragRules.kOverlayEdgeColumnWidth, DockDragRules.kOverlayCenterRowHeight);
            m_top = this.AddWidget(eDockInsertionDirection.Top, middleX, top, DockDragRules.kOverlayCenterColumnWidth, DockDragRules.kOverlayEdgeRowHeight);
            m_right = this.AddWidget(eDockInsertionDirection.Right, right, middleY, DockDragRules.kOverlayEdgeColumnWidth, DockDragRules.kOverlayCenterRowHeight);
            m_bottom = this.AddWidget(eDockInsertionDirection.Bottom, middleX, bottom, DockDragRules.kOverlayCenterColumnWidth, DockDragRules.kOverlayEdgeRowHeight);
            m_center = this.AddWidget(eDockInsertionDirection.AddToTabbedDisplay, middleX, middleY, DockDragRules.kOverlayCenterColumnWidth, DockDragRules.kOverlayCenterRowHeight);

            bool showsSplits = DockDragRules.ShowsSplitWidgets(zoneOrientation);
            m_left.Hidden = !showsSplits;
            m_top.Hidden = !showsSplits;
            m_right.Hidden = !showsSplits;
            m_bottom.Hidden = !showsSplits;

            m_center.IsEnabled = DockDragRules.IsCenterWidgetEnabled(zoneOrientation);
            m_center.AlphaValue = m_center.IsEnabled ? (nfloat)1.0 : (nfloat)kDisabledAlpha;
        }

        // ===========[ Public Interface Methods ]==============================
        /// <summary>
        /// The widget a drop at <paramref name="pointInOverlay"/> would use (top-left origin, in this view's own points), or
        /// null over a corner, outside the overlay, or over a widget this zone doesn't take a drop on
        /// </summary>
        public InsertionWidget FindWidgetAt (CGPoint pointInOverlay)
        {
            eDockInsertionDirection? direction = DockDragRules.FindOverlayDirectionAt((double)pointInOverlay.X, (double)pointInOverlay.Y);
            InsertionWidget widget = direction switch
            {
                eDockInsertionDirection.Left => m_left,
                eDockInsertionDirection.Top => m_top,
                eDockInsertionDirection.Right => m_right,
                eDockInsertionDirection.Bottom => m_bottom,
                eDockInsertionDirection.AddToTabbedDisplay => m_center,
                _ => null,
            };

            return (widget != null) && !widget.Hidden && widget.IsEnabled ? widget : null;
        }

        // ===========[ Helper Methods ]========================================
        private InsertionWidget AddWidget (eDockInsertionDirection direction, double x, double y, double width, double height)
        {
            var widget = new InsertionWidget(direction)
            {
                Frame = new CGRect((nfloat)x, (nfloat)y, (nfloat)width, (nfloat)height),
            };

            this.AddSubview(widget);
            return widget;
        }

        // ===========[ Subclasses/structs ]====================================
        /// <summary>One insertion widget: a bordered cell with a chevron (or a plus for the center), drawn engaged while a drop would use it</summary>
        public sealed class InsertionWidget : ThemedView
        {
            private const double kGlyphPointSize = 11.0;

            private readonly NSImageView m_glyph;
            private bool m_isEngaged;

            internal InsertionWidget (eDockInsertionDirection direction)
            {
                this.Direction = direction;
                this.IsEnabled = true;
                this.BorderThickness = 1.0;

                m_glyph = new NSImageView
                {
                    Image = NSImage.GetSystemSymbol(GlyphNameFor(direction), null),
                    SymbolConfiguration = NSImageSymbolConfiguration.Create((nfloat)kGlyphPointSize, (double)NSFontWeight.Semibold),
                    ImageScaling = NSImageScale.ProportionallyDown,
                };
                m_glyph.TranslatesAutoresizingMaskIntoConstraints = false;
                this.AddSubview(m_glyph);
                NSLayoutConstraint.ActivateConstraints(new[]
                {
                    m_glyph.CenterXAnchor.ConstraintEqualTo(this.CenterXAnchor),
                    m_glyph.CenterYAnchor.ConstraintEqualTo(this.CenterYAnchor),
                });

                this.ApplyEngagement();
            }

            /// <summary>Where a drop on this widget puts what is dragged</summary>
            public eDockInsertionDirection Direction { get; }

            /// <summary>False for a widget shown but not taking drops (the center over a split)</summary>
            public bool IsEnabled { get; internal set; }

            /// <summary>True while a drop now would use this widget</summary>
            public bool IsEngaged
            {
                get => m_isEngaged;
                set
                {
                    if (m_isEngaged != value)
                    {
                        m_isEngaged = value;
                        this.ApplyEngagement();
                    }
                }
            }

            // The glyph's color is a tint, not a layer color, so it is set from the theme here and again on every
            // engagement change, which is often enough for a view that only lives while a drag is over its zone
            private void ApplyEngagement ()
            {
                this.BackgroundColorKey = m_isEngaged ? ThemedColorKeys.DockDropWidget_BackgroundEngaged : ThemedColorKeys.DockDropWidget_BackgroundIdle;
                this.BorderColorKey = m_isEngaged ? ThemedColorKeys.DockDropWidget_BorderEngaged : ThemedColorKeys.DockDropWidget_BorderIdle;
                m_glyph.ContentTintColor = ThemedColors.Get(m_isEngaged ? ThemedColorKeys.DockDropWidget_GlyphEngaged : ThemedColorKeys.DockDropWidget_GlyphIdle);
            }

            // SF Symbols names, the system's equivalents of the Segoe Fluent glyphs the WinUI3 widget shows
            private static string GlyphNameFor (eDockInsertionDirection direction)
            {
                switch (direction)
                {
                    case eDockInsertionDirection.Left: return "chevron.left";
                    case eDockInsertionDirection.Top: return "chevron.up";
                    case eDockInsertionDirection.Bottom: return "chevron.down";
                    case eDockInsertionDirection.AddToTabbedDisplay: return "plus";
                    default: return "chevron.right";
                }
            }
        }
    }
}
