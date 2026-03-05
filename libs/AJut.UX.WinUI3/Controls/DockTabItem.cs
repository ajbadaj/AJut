namespace AJut.UX.Controls
{
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;
    using Microsoft.UI.Xaml.Input;
    using System;
    using Windows.Foundation;
    using DPUtils = AJut.UX.DPUtils<DockTabItem>;

    // ===========[ DockTabItem ]================================================
    // Tab header item for DockZone's tab strip. Owns all per-tab drag logic
    // (pointer capture, threshold decision, reorder vs tearoff) and surfaces
    // high-level events so DockZone can use named handlers instead of
    // anonymous-closure subscriptions.
    //
    // Drag state lives here rather than in DockZone closures, eliminating the
    // anonymous-closure event-handler leak pattern.
    //
    // Template visual states (group: SelectionStates):
    //   Normal              - unselected, resting   (opacity 0.35, slight vertical inset)
    //   PointerOver         - unselected, hovered   (opacity 0.80, slightly raised)
    //   Selected            - selected              (opacity 1.00, -2px top margin bleeds into content)
    //   SelectedPointerOver - selected + hovered    (same as Selected)

    [TemplateVisualState(Name = "Normal",              GroupName = "SelectionStates")]
    [TemplateVisualState(Name = "PointerOver",         GroupName = "SelectionStates")]
    [TemplateVisualState(Name = "Selected",            GroupName = "SelectionStates")]
    [TemplateVisualState(Name = "SelectedPointerOver", GroupName = "SelectionStates")]
    [TemplateVisualState(Name = "NotDragging",         GroupName = "DraggingStates")]
    [TemplateVisualState(Name = "Dragging",            GroupName = "DraggingStates")]
    public sealed class DockTabItem : ContentControl
    {
        // ===========[ Constants ]============================================
        internal const double kDefaultDragThresholdPixels = 8.0;

        // ===========[ Fields ]===============================================
        private bool m_isPointerOver;
        private Border m_rootBorder;

        // Per-tab drag state - owned here rather than in DockZone anonymous closures
        private bool m_isPressedForDrag;
        private bool m_isDragModeDecided;
        private bool m_isReorderDrag;
        private Point m_dragStartPt;

        // ===========[ Construction ]==========================================
        public DockTabItem()
        {
            this.DefaultStyleKey = typeof(DockTabItem);
            this.UseSystemFocusVisuals = false;
        }

        protected override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            m_rootBorder = this.GetTemplateChild("Root") as Border;

            // BorderThickness is a local value set by DockZone before template application.
            // WinUI3 TemplateBinding does not backfill pre-template local values, so push manually.
            if (m_rootBorder != null)
            {
                m_rootBorder.BorderThickness = this.BorderThickness;
            }

            this.UpdateVisualState();
            this.UpdateDraggingState();
        }

        // ===========[ Events ]===============================================

        // Raised on a quick left-click (no drag threshold crossed) - switch to this tab.
        public event EventHandler<int> TabSelectionRequested;

        // Raised on middle-click. DockZone checks AllowMiddleMouseClose before acting.
        public event EventHandler<int> TabMiddleClickCloseRequested;

        // Raised when a vertical drag crosses DragThresholdPixels - initiate tearoff.
        public event EventHandler TabTearoffDragInitiated;

        // Raised continuously during a horizontal reorder drag. The PointerRoutedEventArgs
        // allows DockZone to compute the cursor position in the tabPanel via
        // e.GetCurrentPoint(tabPanel).Position.X without DockTabItem needing a reference to it.
        public event EventHandler<PointerRoutedEventArgs> TabReorderDragMoved;

        // Raised when the pointer is released after a reorder drag.
        // The int payload is this tab's source Index. DockZone uses its own tracked
        // target index to execute the reorder.
        public event EventHandler<int> TabReorderDropped;

        // Raised when pointer capture is lost mid-drag (e.g. window loses focus).
        public event EventHandler TabDragCancelled;

        // Raised on right-tap (context menu). DockZone uses this to show the header context menu.
        public event EventHandler<RightTappedRoutedEventArgs> TabRightTapped;

        // ===========[ Dependency Properties ]=================================

        public static readonly DependencyProperty IsSelectedProperty = DPUtils.Register(_ => _.IsSelected, (d, e) => d.UpdateVisualState());
        public bool IsSelected
        {
            get => (bool)this.GetValue(IsSelectedProperty);
            set => this.SetValue(IsSelectedProperty, value);
        }

        public static readonly DependencyProperty IsDraggingProperty = DPUtils.Register(_ => _.IsDragging, (d, e) => d.OnIsDraggingChanged());
        public bool IsDragging
        {
            get => (bool)this.GetValue(IsDraggingProperty);
            set => this.SetValue(IsDraggingProperty, value);
        }

        // ===========[ Properties ]============================================

        // Position of this tab in the strip. Set by DockZone before adding to the panel.
        public int Index { get; set; }

        // Drag threshold in pixels. Set by DockZone to match DockingManager.DragThresholdPixels.
        public double DragThresholdPixels { get; set; } = kDefaultDragThresholdPixels;

        // ===========[ Pointer event overrides ]===============================

        protected override void OnPointerEntered(PointerRoutedEventArgs e)
        {
            base.OnPointerEntered(e);
            m_isPointerOver = true;
            this.UpdateVisualState();
        }

        protected override void OnPointerExited(PointerRoutedEventArgs e)
        {
            base.OnPointerExited(e);
            m_isPointerOver = false;
            this.UpdateVisualState();
        }

        protected override void OnPointerPressed(PointerRoutedEventArgs e)
        {
            base.OnPointerPressed(e);

            // Middle-click → close request (DockZone checks AllowMiddleMouseClose before acting)
            if (e.GetCurrentPoint(this).Properties.IsMiddleButtonPressed)
            {
                e.Handled = true;
                this.TabMiddleClickCloseRequested?.Invoke(this, this.Index);
                return;
            }

            this.CapturePointer(e.Pointer);
            m_dragStartPt = e.GetCurrentPoint(this).Position;
            m_isPressedForDrag = true;
            m_isDragModeDecided = false;
            m_isReorderDrag = false;
        }

        protected override void OnPointerMoved(PointerRoutedEventArgs e)
        {
            base.OnPointerMoved(e);

            if (!m_isPressedForDrag)
            {
                return;
            }

            var curPt = e.GetCurrentPoint(this).Position;
            double dx = curPt.X - m_dragStartPt.X;
            double dy = curPt.Y - m_dragStartPt.Y;
            double threshold = this.DragThresholdPixels;

            if (!m_isDragModeDecided)
            {
                if ((dx * dx + dy * dy) < (threshold * threshold))
                {
                    return;
                }

                m_isDragModeDecided = true;
                m_isReorderDrag = Math.Abs(dx) > Math.Abs(dy);

                if (!m_isReorderDrag)
                {
                    // Vertical drag → tearoff. Reset state before releasing capture so
                    // OnPointerCaptureLost does not incorrectly fire TabDragCancelled.
                    this.ResetDragState();
                    this.ReleasePointerCapture(e.Pointer);
                    e.Handled = true;
                    this.TabTearoffDragInitiated?.Invoke(this, EventArgs.Empty);
                    return;
                }
            }

            if (m_isReorderDrag)
            {
                this.TabReorderDragMoved?.Invoke(this, e);
            }
        }

        protected override void OnPointerReleased(PointerRoutedEventArgs e)
        {
            base.OnPointerReleased(e);

            if (m_isPressedForDrag)
            {
                if (m_isDragModeDecided && m_isReorderDrag)
                {
                    this.TabReorderDropped?.Invoke(this, this.Index);
                }
                else if (!m_isDragModeDecided)
                {
                    // Quick click without threshold - select this tab
                    this.TabSelectionRequested?.Invoke(this, this.Index);
                }
            }

            this.ResetDragState();
        }

        protected override void OnPointerCaptureLost(PointerRoutedEventArgs e)
        {
            base.OnPointerCaptureLost(e);

            if (m_isPressedForDrag || m_isDragModeDecided)
            {
                this.TabDragCancelled?.Invoke(this, EventArgs.Empty);
            }

            this.ResetDragState();
        }

        protected override void OnRightTapped(RightTappedRoutedEventArgs e)
        {
            base.OnRightTapped(e);
            this.TabRightTapped?.Invoke(this, e);
        }

        // ===========[ Visual state management ]===============================

        private void UpdateVisualState()
        {
            if (this.IsSelected)
            {
                VisualStateManager.GoToState(this, m_isPointerOver ? "SelectedPointerOver" : "Selected", false);
            }
            else
            {
                VisualStateManager.GoToState(this, m_isPointerOver ? "PointerOver" : "Normal", false);
            }
        }

        private void UpdateDraggingState()
        {
            VisualStateManager.GoToState(this, this.IsDragging ? "Dragging" : "NotDragging", false);
        }

        private void OnIsDraggingChanged()
        {
            this.UpdateDraggingState();
        }

        private void ResetDragState()
        {
            m_isPressedForDrag = false;
            m_isDragModeDecided = false;
            m_isReorderDrag = false;
        }
    }
}
