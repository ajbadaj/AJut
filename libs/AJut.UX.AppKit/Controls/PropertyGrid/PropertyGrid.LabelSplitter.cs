namespace AJut.UX.Controls
{
    using System.Collections.Generic;
    using AJut.UX.PropertyInteraction;
    using AJut.UX.Theming;
    using AppKit;
    using CoreGraphics;

    // ===========[ PropertyGrid: label splitter ]==============================
    // With the pointer on a row's label within PropertyGridRowRules.kLabelSplitterHitDistance of the
    // boundary between the label and its editor, a line shows on the boundary. Dragging it sets
    // LabelColumnWidth, and double-clicking it fits LabelColumnWidth to the labels on screen.
    //
    // The grid follows the pointer through a tracking area of its own, which AppKit reports to it
    // whatever view is under the pointer, editors included. The line is a view over the rows,
    // reaching the hit distance back into the label from the boundary, with the line along its
    // right edge, and only there while the pointer is near, so a press on it drags; AppKit then
    // sends the drag and the release to that same view. Nothing covers the editor side, which takes
    // its own clicks.
    public partial class PropertyGrid
    {
        private readonly LabelSplitterView m_labelSplitter;
        private readonly NSLayoutConstraint m_labelSplitterLeading;
        private NSTrackingArea m_pointerTrackingArea;

        // In the grid's coordinates: where the boundary under the pointer is, the label column width that
        // boundary stands for, and where the drag in progress started
        private double m_labelSplitterBoundaryX;
        private double m_labelSplitterColumnWidth = double.NaN;
        private double m_labelSplitterDragStartX;
        private double m_labelSplitterDragStartBoundaryX;
        private double m_labelSplitterDragStartWidth;
        private bool m_isDraggingLabelSplitter;

        // ===========[ Events ]================================================
        public override void UpdateTrackingAreas ()
        {
            base.UpdateTrackingAreas();
            if (m_pointerTrackingArea != null)
            {
                this.RemoveTrackingArea(m_pointerTrackingArea);
            }

            m_pointerTrackingArea = new NSTrackingArea(
                this.Bounds,
                NSTrackingAreaOptions.MouseMoved | NSTrackingAreaOptions.MouseEnteredAndExited | NSTrackingAreaOptions.ActiveInKeyWindow | NSTrackingAreaOptions.InVisibleRect,
                this,
                null
            );
            this.AddTrackingArea(m_pointerTrackingArea);
        }

        // Shows the splitter over the boundary of the row under the pointer when the pointer is near it, and
        // hides it otherwise
        public override void MouseMoved (NSEvent theEvent)
        {
            base.MouseMoved(theEvent);
            if (m_isDraggingLabelSplitter)
            {
                return;
            }

            CGPoint pointer = this.ConvertPointFromView(theEvent.LocationInWindow, null);
            foreach (RowView row in m_liveRows)
            {
                if (row.TryGetLabelBoundaryAt(pointer, this, out double boundaryX, out double shownWidth))
                {
                    if (PropertyGridRowRules.IsNearLabelBoundary((double)pointer.X, boundaryX))
                    {
                        // A set width is the same boundary on every row, so a drag starts from it rather than
                        // from a row whose label is held wider than it
                        m_labelSplitterColumnWidth = double.IsNaN(this.LabelColumnWidth) ? shownWidth : this.LabelColumnWidth;
                        this.PlaceLabelSplitter(boundaryX);
                        m_labelSplitter.Hidden = false;
                        return;
                    }

                    break;
                }
            }

            m_labelSplitter.Hidden = true;
        }

        public override void MouseExited (NSEvent theEvent)
        {
            base.MouseExited(theEvent);
            if (!m_isDraggingLabelSplitter)
            {
                m_labelSplitter.Hidden = true;
            }
        }

        // ===========[ Helper Methods ]========================================
        private double PointerXIn (NSEvent theEvent) => (double)this.ConvertPointFromView(theEvent.LocationInWindow, null).X;

        private void BeginLabelSplitterDrag (NSEvent theEvent)
        {
            if (double.IsNaN(m_labelSplitterColumnWidth))
            {
                return;
            }

            m_labelSplitterDragStartX = this.PointerXIn(theEvent);
            m_labelSplitterDragStartBoundaryX = m_labelSplitterBoundaryX;
            m_labelSplitterDragStartWidth = m_labelSplitterColumnWidth;
            m_isDraggingLabelSplitter = true;
        }

        // Every move is measured from where the drag started. The line follows the width actually set,
        // which stops at the limits even when the pointer doesn't.
        private void DragLabelSplitter (NSEvent theEvent)
        {
            if (!m_isDraggingLabelSplitter)
            {
                return;
            }

            double pointerDelta = this.PointerXIn(theEvent) - m_labelSplitterDragStartX;
            double width = PropertyGridRowRules.ComputeDraggedLabelColumnWidth(m_labelSplitterDragStartWidth, pointerDelta, (double)this.Bounds.Width);
            this.LabelColumnWidth = width;
            this.PlaceLabelSplitter(m_labelSplitterDragStartBoundaryX + (width - m_labelSplitterDragStartWidth));
        }

        // A drag that moved has already set its width; a press with no move sets nothing, so a click on
        // the splitter doesn't fix an auto sized column
        private void EndLabelSplitterDrag ()
        {
            m_isDraggingLabelSplitter = false;
        }

        private void FitLabelColumnWidthToShownLabels ()
        {
            var widths = new List<double>();
            foreach (RowView row in m_liveRows)
            {
                if (row.IsShown)
                {
                    widths.Add(row.MeasureFittingLabelColumnWidth());
                }
            }

            double fit = PropertyGridRowRules.FitLabelColumnWidth(widths, (double)this.Bounds.Width);
            if (!double.IsNaN(fit))
            {
                this.LabelColumnWidth = fit;
            }

            m_labelSplitter.Hidden = true;
        }

        private void PlaceLabelSplitter (double boundaryX)
        {
            m_labelSplitterBoundaryX = boundaryX;
            m_labelSplitterLeading.Constant = (nfloat)(boundaryX - PropertyGridRowRules.kLabelSplitterHitDistance);
            this.Window?.InvalidateCursorRectsForView(m_labelSplitter);
        }

        // ===========[ Subclasses/structs ]====================================
        // The splitter's line and hit area. It holds no state: presses, drags and releases go to the grid.
        private sealed class LabelSplitterView : NSView
        {
            private const double kLineWidth = 2.0;

            private readonly PropertyGrid m_owner;

            public LabelSplitterView (PropertyGrid owner)
            {
                m_owner = owner;
                this.Hidden = true;
                this.TranslatesAutoresizingMaskIntoConstraints = false;
            }

            public override bool IsFlipped => true;
            public override bool AcceptsFirstMouse (NSEvent theEvent) => true;

            public override void DrawRect (CGRect dirtyRect)
            {
                CGRect bounds = this.Bounds;
                ThemedColors.Get(ThemedColorKeys.PropertyGrid_LabelSplitter).SetFill();
                NSBezierPath.FillRect(new CGRect(bounds.Right - (nfloat)kLineWidth, bounds.Y, (nfloat)kLineWidth, bounds.Height));
            }

            public override void ResetCursorRects ()
            {
                this.AddCursorRect(this.Bounds, NSCursor.ResizeLeftRightCursor);
            }

            public override void MouseDown (NSEvent theEvent)
            {
                if (theEvent.ClickCount == 2)
                {
                    m_owner.FitLabelColumnWidthToShownLabels();
                    return;
                }

                m_owner.BeginLabelSplitterDrag(theEvent);
            }

            public override void MouseDragged (NSEvent theEvent) => m_owner.DragLabelSplitter(theEvent);
            public override void MouseUp (NSEvent theEvent) => m_owner.EndLabelSplitterDrag();
        }
    }
}
