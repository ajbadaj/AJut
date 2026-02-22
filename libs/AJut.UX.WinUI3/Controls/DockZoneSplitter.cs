namespace AJut.UX.Controls
{
    using System;
    using AJut.UX.Docking;
    using Microsoft.UI.Input;
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;
    using Microsoft.UI.Xaml.Input;

    // ===========[ DockZoneSplitter ]==========================================
    // Lightweight pointer-drag resizer placed between sibling DockZone panels
    // in a split layout. The owning DockZone adds this into the Grid via
    // Setup(); the splitter then adjusts the adjacent ColumnDefinition.Width
    // or RowDefinition.Height values as the user drags.
    //
    // Template: transparent hit-testable background with a thin visual stripe.

    public class DockZoneSplitter : Control
    {
        // ===========[ Fields ]===============================================
        private Grid m_parentGrid;
        private int m_splitIndex;                // 0 = gap between child[0] and child[1]
        private eDockOrientation m_splitOrientation;
        private bool m_isDragging;
        private double m_dragStart;             // pointer X or Y at drag-start
        private double m_beforeSize;            // adjacent "before" cell size at drag-start
        private double m_afterSize;             // adjacent "after" cell size at drag-start

        // ===========[ Construction ]=========================================
        public DockZoneSplitter ()
        {
            this.DefaultStyleKey = typeof(DockZoneSplitter);
        }

        // ===========[ Setup ]================================================

        /// <summary>
        /// Called by DockZone after creating this splitter. Stores the parent
        /// grid reference, gap index, and split direction, then sets the
        /// pointer cursor to match the drag axis.
        /// </summary>
        internal void Setup (Grid parentGrid, int splitIndex, eDockOrientation orientation)
        {
            m_parentGrid = parentGrid;
            m_splitIndex = splitIndex;
            m_splitOrientation = orientation;

            this.ProtectedCursor = orientation == eDockOrientation.Horizontal
                ? InputSystemCursor.Create(InputSystemCursorShape.SizeWestEast)
                : InputSystemCursor.Create(InputSystemCursorShape.SizeNorthSouth);
        }

        // ===========[ Pointer drag ]=========================================
        protected override void OnPointerPressed (PointerRoutedEventArgs e)
        {
            base.OnPointerPressed(e);
            if (m_parentGrid == null)
            {
                return;
            }

            var pt = e.GetCurrentPoint(m_parentGrid);
            m_isDragging = true;
            this.CapturePointer(e.Pointer);

            if (m_splitOrientation == eDockOrientation.Horizontal)
            {
                m_dragStart = pt.Position.X;
                m_beforeSize = m_parentGrid.ColumnDefinitions[m_splitIndex * 2].ActualWidth;
                m_afterSize = m_parentGrid.ColumnDefinitions[m_splitIndex * 2 + 2].ActualWidth;
            }
            else
            {
                m_dragStart = pt.Position.Y;
                m_beforeSize = m_parentGrid.RowDefinitions[m_splitIndex * 2].ActualHeight;
                m_afterSize = m_parentGrid.RowDefinitions[m_splitIndex * 2 + 2].ActualHeight;
            }

            e.Handled = true;
        }

        protected override void OnPointerMoved (PointerRoutedEventArgs e)
        {
            base.OnPointerMoved(e);
            if (!m_isDragging || m_parentGrid == null)
            {
                return;
            }

            var pt = e.GetCurrentPoint(m_parentGrid);

            if (m_splitOrientation == eDockOrientation.Horizontal)
            {
                double delta = pt.Position.X - m_dragStart;
                double newBefore = Math.Max(30, m_beforeSize + delta);
                double newAfter = Math.Max(30, m_afterSize - delta);
                m_parentGrid.ColumnDefinitions[m_splitIndex * 2].Width = new GridLength(newBefore, GridUnitType.Star);
                m_parentGrid.ColumnDefinitions[m_splitIndex * 2 + 2].Width = new GridLength(newAfter, GridUnitType.Star);
            }
            else
            {
                double delta = pt.Position.Y - m_dragStart;
                double newBefore = Math.Max(30, m_beforeSize + delta);
                double newAfter = Math.Max(30, m_afterSize - delta);
                m_parentGrid.RowDefinitions[m_splitIndex * 2].Height = new GridLength(newBefore, GridUnitType.Star);
                m_parentGrid.RowDefinitions[m_splitIndex * 2 + 2].Height = new GridLength(newAfter, GridUnitType.Star);
            }

            e.Handled = true;
        }

        protected override void OnPointerReleased (PointerRoutedEventArgs e)
        {
            base.OnPointerReleased(e);
            if (m_isDragging)
            {
                m_isDragging = false;
                this.ReleasePointerCapture(e.Pointer);
                e.Handled = true;
            }
        }

        protected override void OnPointerCaptureLost (PointerRoutedEventArgs e)
        {
            base.OnPointerCaptureLost(e);
            m_isDragging = false;
        }
    }
}
