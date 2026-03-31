namespace AJut.UX.Controls
{
    using AJut.UX;
    using Microsoft.UI.Input;
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;
    using Microsoft.UI.Xaml.Input;
    using System;
    using DPUtils = AJut.UX.DPUtils<GridSplitter>;

    public enum eGridSplitterOrientation
    {
        /// <summary>Auto-detect from RowSpan, ColumnSpan, and alignment at DetectAndApply time.</summary>
        Auto,

        /// <summary>Resize adjacent columns - splitter appears as a vertical bar.</summary>
        Columns,

        /// <summary>Resize adjacent rows - splitter appears as a horizontal bar.</summary>
        Rows
    }

    // ===========[ GridSplitter ]===============================================
    // General-purpose drag-to-resize splitter placed between sibling cells in a
    // Grid. Orientation is auto-detected from RowSpan, ColumnSpan, and alignment
    // properties, or set authoritatively via the Orientation DP or by passing
    // an explicit value to DetectAndApply().
    //
    // Typical usage in XAML: place in a Grid cell between two content cells,
    // set Orientation if auto-detection would be ambiguous.
    //
    // Programmatic usage (e.g. DockZone): add to the grid first, then call
    // DetectAndApply(eGridSplitterOrientation.Columns/Rows).
    //
    // Template: transparent hit-testable background with a toggling 1px stripe.
    // VSM group OrientationStates: ColumnResize (vertical stripe) / RowResize (horizontal stripe).

    public class GridSplitter : Control
    {
        // ===========[ Const/Static ]==========================================
        private const double kDefaultMinPanelSize = 30.0;
        private const string kState_ColumnResize = "ColumnResize";
        private const string kState_RowResize = "RowResize";

        // ===========[ Fields ]===============================================
        private Grid m_parentGrid;
        private int m_anteriorDefinitionIndex = -1;
        private int m_posteriorDefinitionIndex = -1;
        private bool m_resizesColumns;
        private bool m_isDragging;
        private double m_dragStart;
        private double m_beforePixels;
        private double m_afterPixels;
        private double m_totalStar;

        // ===========[ Construction ]=========================================
        public GridSplitter()
        {
            this.DefaultStyleKey = typeof(GridSplitter);
            this.Loaded += this.OnLoaded;
        }

        // ===========[ Dependency Properties ]================================
        public static readonly DependencyProperty OrientationProperty = DPUtils.Register(_ => _.Orientation, eGridSplitterOrientation.Auto);
        public eGridSplitterOrientation Orientation
        {
            get => (eGridSplitterOrientation)this.GetValue(OrientationProperty);
            set => this.SetValue(OrientationProperty, value);
        }

        public static readonly DependencyProperty MinPanelSizeProperty = DPUtils.Register(_ => _.MinPanelSize, kDefaultMinPanelSize);
        public double MinPanelSize
        {
            get => (double)this.GetValue(MinPanelSizeProperty);
            set => this.SetValue(MinPanelSizeProperty, value);
        }

        public static readonly DependencyProperty ThicknessProperty = DPUtils.Register(_ => _.Thickness);
        public double Thickness
        {
            get => (double)this.GetValue(ThicknessProperty);
            set => this.SetValue(ThicknessProperty, value);
        }

        // ===========[ Events ]===============================================
        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (m_parentGrid == null)
            {
                this.DetectAndApply();
            }
        }

        // ===========[ Public Interface Methods ]=============================
        protected override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            if (m_parentGrid != null)
            {
                this.ApplyOrientationState();
            }
        }

        protected override void OnPointerPressed(PointerRoutedEventArgs e)
        {
            base.OnPointerPressed(e);
            if (m_parentGrid == null || m_anteriorDefinitionIndex < 0)
            {
                return;
            }

            var pt = e.GetCurrentPoint(m_parentGrid);
            m_isDragging = true;
            this.CapturePointer(e.Pointer);

            if (m_resizesColumns)
            {
                m_dragStart = pt.Position.X;
                m_beforePixels = m_parentGrid.ColumnDefinitions[m_anteriorDefinitionIndex].ActualWidth;
                m_afterPixels = m_parentGrid.ColumnDefinitions[m_posteriorDefinitionIndex].ActualWidth;
                m_totalStar = m_parentGrid.ColumnDefinitions[m_anteriorDefinitionIndex].Width.Value
                            + m_parentGrid.ColumnDefinitions[m_posteriorDefinitionIndex].Width.Value;
            }
            else
            {
                m_dragStart = pt.Position.Y;
                m_beforePixels = m_parentGrid.RowDefinitions[m_anteriorDefinitionIndex].ActualHeight;
                m_afterPixels = m_parentGrid.RowDefinitions[m_posteriorDefinitionIndex].ActualHeight;
                m_totalStar = m_parentGrid.RowDefinitions[m_anteriorDefinitionIndex].Height.Value
                            + m_parentGrid.RowDefinitions[m_posteriorDefinitionIndex].Height.Value;
            }

            e.Handled = true;
        }

        protected override void OnPointerMoved(PointerRoutedEventArgs e)
        {
            base.OnPointerMoved(e);
            if (!m_isDragging || m_parentGrid == null)
            {
                return;
            }

            var pt = e.GetCurrentPoint(m_parentGrid);
            double minSize = this.MinPanelSize;

            if (m_resizesColumns)
            {
                double delta = pt.Position.X - m_dragStart;
                double desiredBefore = Math.Max(minSize, m_beforePixels + delta);
                double desiredAfter = Math.Max(minSize, m_afterPixels - delta);
                double desiredTotal = desiredBefore + desiredAfter;
                double ratio = desiredBefore / desiredTotal;
                m_parentGrid.ColumnDefinitions[m_anteriorDefinitionIndex].Width = new GridLength(m_totalStar * ratio, GridUnitType.Star);
                m_parentGrid.ColumnDefinitions[m_posteriorDefinitionIndex].Width = new GridLength(m_totalStar * (1.0 - ratio), GridUnitType.Star);
            }
            else
            {
                double delta = pt.Position.Y - m_dragStart;
                double desiredBefore = Math.Max(minSize, m_beforePixels + delta);
                double desiredAfter = Math.Max(minSize, m_afterPixels - delta);
                double desiredTotal = desiredBefore + desiredAfter;
                double ratio = desiredBefore / desiredTotal;
                m_parentGrid.RowDefinitions[m_anteriorDefinitionIndex].Height = new GridLength(m_totalStar * ratio, GridUnitType.Star);
                m_parentGrid.RowDefinitions[m_posteriorDefinitionIndex].Height = new GridLength(m_totalStar * (1.0 - ratio), GridUnitType.Star);
            }

            e.Handled = true;
        }

        protected override void OnPointerReleased(PointerRoutedEventArgs e)
        {
            base.OnPointerReleased(e);
            if (m_isDragging)
            {
                m_isDragging = false;
                this.ReleasePointerCapture(e.Pointer);
                e.Handled = true;
            }
        }

        protected override void OnPointerCaptureLost(PointerRoutedEventArgs e)
        {
            base.OnPointerCaptureLost(e);
            m_isDragging = false;
        }

        // ===========[ Internal Interface Methods ]=========================

        /// <summary>
        /// Resolves the parent grid, orientation, and adjacent definition indices,
        /// then applies cursor and VSM state. Must be called after the element has
        /// been added to a Grid. Pass an explicit orientation to override auto-detect
        /// (overrides the Orientation DP as well). Pass null to use the Orientation
        /// DP value, falling back to auto-detect if it is Auto.
        /// </summary>
        internal void DetectAndApply(eGridSplitterOrientation? orientation = null)
        {
            var parentGrid = this.GetFirstParentOf<Grid>();
            if (parentGrid == null)
            {
                Logger.LogInfo("[WARNING] GridSplitter.DetectAndApply: no parent Grid found", eLogVerbosity.Detailed);
                return;
            }

            m_parentGrid = parentGrid;

            // 1. Resolve orientation - explicit arg wins, then DP, then auto-detect
            var resolved = orientation ?? this.Orientation;
            if (resolved == eGridSplitterOrientation.Auto)
            {
                if (!this.TryDetectOrientation(out bool detected))
                {
                    Logger.LogInfo("[WARNING] GridSplitter.DetectAndApply: could not auto-detect orientation", eLogVerbosity.Detailed);
                    m_parentGrid = null;
                    return;
                }

                m_resizesColumns = detected;
            }
            else
            {
                m_resizesColumns = (resolved == eGridSplitterOrientation.Columns);
            }

            // 2. Resolve anterior/posterior definition indices from grid position + alignment.
            //    Alignment indicates which edge of the cell the splitter sits at:
            //      Left/Top   → near edge → resizes the cell before this one and this cell
            //      Right/Bottom → far edge → resizes this cell and the cell after it
            //      Stretch/Center → dedicated splitter cell → resizes the cells on either side
            if (m_resizesColumns)
            {
                int col = Grid.GetColumn(this);
                switch (this.HorizontalAlignment)
                {
                    case HorizontalAlignment.Left:
                        m_anteriorDefinitionIndex = col - 1;
                        m_posteriorDefinitionIndex = col;
                        break;
                    case HorizontalAlignment.Right:
                        m_anteriorDefinitionIndex = col;
                        m_posteriorDefinitionIndex = col + 1;
                        break;
                    default: // Stretch or Center - dedicated splitter column
                        m_anteriorDefinitionIndex = col - 1;
                        m_posteriorDefinitionIndex = col + 1;
                        break;
                }
            }
            else
            {
                int row = Grid.GetRow(this);
                switch (this.VerticalAlignment)
                {
                    case VerticalAlignment.Top:
                        m_anteriorDefinitionIndex = row - 1;
                        m_posteriorDefinitionIndex = row;
                        break;
                    case VerticalAlignment.Bottom:
                        m_anteriorDefinitionIndex = row;
                        m_posteriorDefinitionIndex = row + 1;
                        break;
                    default: // Stretch or Center - dedicated splitter row
                        m_anteriorDefinitionIndex = row - 1;
                        m_posteriorDefinitionIndex = row + 1;
                        break;
                }
            }

            this.UpdateCursor();
            this.ApplyOrientationState();
        }

        // ===========[ Private helpers ]=======================================
        private bool TryDetectOrientation(out bool resizesColumns)
        {
            int rowSpan = Grid.GetRowSpan(this);
            int colSpan = Grid.GetColumnSpan(this);

            // Spans more rows than columns - tall and narrow - column splitter
            if (rowSpan > colSpan)
            {
                resizesColumns = true;
                return true;
            }

            // Spans more columns than rows - wide and flat - row splitter
            if (colSpan > rowSpan)
            {
                resizesColumns = false;
                return true;
            }

            // Equal spans - use alignment as tiebreaker
            bool hStretch = (this.HorizontalAlignment == HorizontalAlignment.Stretch);
            bool vStretch = (this.VerticalAlignment == VerticalAlignment.Stretch);

            if (vStretch && !hStretch)
            {
                resizesColumns = true;
                return true;
            }

            if (hStretch && !vStretch)
            {
                resizesColumns = false;
                return true;
            }

            // Still ambiguous - use rendered size as final tiebreaker
            if (this.ActualHeight > this.ActualWidth)
            {
                resizesColumns = true;
                return true;
            }

            if (this.ActualWidth > this.ActualHeight)
            {
                resizesColumns = false;
                return true;
            }

            resizesColumns = false;
            return false;
        }

        private void UpdateCursor()
        {
            this.ProtectedCursor = m_resizesColumns
                ? InputSystemCursor.Create(InputSystemCursorShape.SizeWestEast)
                : InputSystemCursor.Create(InputSystemCursorShape.SizeNorthSouth);
        }

        private void ApplyOrientationState()
        {
            VisualStateManager.GoToState(this, m_resizesColumns ? kState_ColumnResize : kState_RowResize, false);
        }
    }
}
