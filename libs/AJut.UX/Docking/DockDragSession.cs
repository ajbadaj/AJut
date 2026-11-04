namespace AJut.UX.Docking
{
    /// <summary>
    /// What a <see cref="DockDragSession{TZone, TWidget, TWindow}"/> asks of the toolkit running the drag: showing and
    /// hiding a zone's drop overlay, engaging an insertion widget, dimming the dragged window, and flashing and raising a
    /// window the pointer has rested on
    /// </summary>
    public interface IDockDragSessionHost<TZone, TWidget, TWindow>
        where TZone : class
        where TWidget : class
        where TWindow : class
    {
        /// <summary>Shows or hides <paramref name="zone"/>'s drop overlay</summary>
        void ShowDropTarget (TZone zone, bool isTarget);

        /// <summary>Draws <paramref name="widget"/> engaged (a drop now would use it) or not</summary>
        void EngageWidget (TWidget widget, bool isEngaged);

        /// <summary>Dims the dragged window so the zone under it shows through, or brings it back to full opacity</summary>
        void DimDragSource (bool isDimmed);

        /// <summary>Gets <paramref name="window"/> ready to flash, as the pointer has rested on it long enough to start raising it</summary>
        void BeginHoverFlash (TWindow window);

        /// <summary>Sets the opacity of a flashing window, from 0 to 1</summary>
        void SetHoverFlashOpacity (TWindow window, double opacity);

        /// <summary>Ends a flash, leaving <paramref name="window"/> at full opacity</summary>
        void EndHoverFlash (TWindow window);

        /// <summary>Raises <paramref name="window"/> to the front, with the dragged window kept above it</summary>
        void RaiseHoverWindow (TWindow window);
    }

    // ===========[ DockDragSession ]============================================
    /// <summary>
    /// The drop search of a docking drag, from the moment a tearoff starts following the pointer to the moment the drag
    /// ends, shared by every toolkit. The toolkit moves the dragged window and finds what is under the pointer; each tick
    /// it hands that to <see cref="Update"/> (the zone, the insertion widget if the pointer is on one, and the window it is
    /// resting on), and this decides what changes: which overlay shows, which widget is engaged, whether the dragged window
    /// is dimmed, and when a window the pointer rests on flashes and is raised. At the end, <see cref="EngagedWidget"/> is
    /// what a drop uses. Escape <see cref="Cancel"/>s instead: nothing drops, and the toolkit returns what was dragged to
    /// where the drag began (see <see cref="DockDragOrigin"/>).
    /// </summary>
    /// <remarks>
    /// Timing is counted in ticks, one per <see cref="Update"/>, the way the drag's loop runs: WinUI3 polls every 16ms, so
    /// 47 ticks is about three quarters of a second, 94 about a second and a half, and a flash phase of 15 about a quarter
    /// second. A toolkit with no poll of its own calls <see cref="Update"/> on a timer at the same rate.
    /// </remarks>
    public sealed class DockDragSession<TZone, TWidget, TWindow>
        where TZone : class
        where TWidget : class
        where TWindow : class
    {
        /// <summary>How many ticks the pointer rests on a window before it starts to flash</summary>
        public const int kArmingStartTicks = 47;

        /// <summary>How many ticks the pointer rests on a window before it is raised</summary>
        public const int kBringForwardTicks = 94;

        /// <summary>How many ticks each half of a flash lasts, dim then bright</summary>
        public const int kFlashPeriodTicks = 15;

        /// <summary>A flashing window's opacity in the dim half of a flash</summary>
        public const double kFlashDimOpacity = 0.70;

        private readonly IDockDragSessionHost<TZone, TWidget, TWindow> m_host;
        private TZone? m_dropTarget;
        private TWidget? m_engagedWidget;
        private TWindow? m_hoverWindow;
        private int m_hoverTicks;
        private bool m_isHoverArming;
        private bool m_isSourceDimmed;
        private bool m_isEnded;
        private bool m_isCanceled;

        // ===========[ Setup/Construction/Teardown ]===========================
        public DockDragSession (IDockDragSessionHost<TZone, TWidget, TWindow> host)
        {
            m_host = host;
        }

        // ===========[ Properties ]============================================
        /// <summary>The zone whose drop overlay shows, or null</summary>
        public TZone? DropTarget => m_dropTarget;

        /// <summary>The insertion widget under the pointer, which a drop now would use, or null</summary>
        public TWidget? EngagedWidget => m_engagedWidget;

        /// <summary>True once <see cref="End"/> or <see cref="Cancel"/> has run</summary>
        public bool IsEnded => m_isEnded;

        /// <summary>True when the drag ended by <see cref="Cancel"/>: nothing drops, and the toolkit puts the drag back where it began</summary>
        public bool IsCanceled => m_isCanceled;

        // ===========[ Public Interface Methods ]==============================
        /// <summary>
        /// One tick of the drag: what is under the pointer now. <paramref name="dropTarget"/> is the zone under it (null for
        /// none), <paramref name="widget"/> the insertion widget under it (null for none), and <paramref name="hoverWindow"/>
        /// the window it rests on that is a candidate for raising (null for none).
        /// </summary>
        public void Update (TZone? dropTarget, TWidget? widget, TWindow? hoverWindow)
        {
            if (m_isEnded)
            {
                return;
            }

            // 1. The engaged widget
            if (widget != m_engagedWidget)
            {
                if (m_engagedWidget != null)
                {
                    m_host.EngageWidget(m_engagedWidget, false);
                }

                m_engagedWidget = widget;
                if (m_engagedWidget != null)
                {
                    m_host.EngageWidget(m_engagedWidget, true);
                }
            }

            // 2. The zone whose overlay shows
            if (dropTarget != m_dropTarget)
            {
                if (m_dropTarget != null)
                {
                    m_host.ShowDropTarget(m_dropTarget, false);
                }

                m_dropTarget = dropTarget;
                if (m_dropTarget != null)
                {
                    m_host.ShowDropTarget(m_dropTarget, true);
                }
            }

            // 3. Dimmed over a drop target, so the zone shows through; full opacity over empty space
            bool shouldDim = dropTarget != null;
            if (shouldDim != m_isSourceDimmed)
            {
                m_isSourceDimmed = shouldDim;
                m_host.DimDragSource(shouldDim);
            }

            // 4. Resting on a window: flash it, then raise it
            this.UpdateHover(hoverWindow);
        }

        /// <summary>
        /// Ends the drag: undims the dragged window, stops any flash, and hides the overlay and widget. Read
        /// <see cref="EngagedWidget"/> and <see cref="DropTarget"/> before calling this to know what a drop uses. Safe to call
        /// more than once.
        /// </summary>
        public void End ()
        {
            if (m_isEnded)
            {
                return;
            }

            m_isEnded = true;
            if (m_isSourceDimmed)
            {
                m_isSourceDimmed = false;
                m_host.DimDragSource(false);
            }

            if (m_isHoverArming && m_hoverWindow != null)
            {
                m_isHoverArming = false;
                m_host.EndHoverFlash(m_hoverWindow);
            }

            if (m_engagedWidget != null)
            {
                m_host.EngageWidget(m_engagedWidget, false);
            }

            if (m_dropTarget != null)
            {
                m_host.ShowDropTarget(m_dropTarget, false);
            }
        }

        /// <summary>
        /// Cancels the drag (Escape): ends it as <see cref="End"/> does, and lets go of the drop target and engaged widget so
        /// nothing drops. Does nothing once the drag has ended.
        /// </summary>
        public void Cancel ()
        {
            if (m_isEnded)
            {
                return;
            }

            m_isCanceled = true;
            this.End();
            m_engagedWidget = null;
            m_dropTarget = null;
        }

        // ===========[ Helper Methods ]========================================
        private void UpdateHover (TWindow? hoverWindow)
        {
            // Moved to another window (or off every window): any flash in progress stops and the count restarts
            if (hoverWindow != m_hoverWindow)
            {
                if (m_isHoverArming && m_hoverWindow != null)
                {
                    m_host.EndHoverFlash(m_hoverWindow);
                    m_isHoverArming = false;
                }

                m_hoverWindow = hoverWindow;
                m_hoverTicks = 0;
                return;
            }

            if (m_hoverWindow == null)
            {
                return;
            }

            ++m_hoverTicks;
            if (m_hoverTicks == kArmingStartTicks)
            {
                m_isHoverArming = true;
                m_host.BeginHoverFlash(m_hoverWindow);
            }

            if (m_isHoverArming && m_hoverTicks < kBringForwardTicks)
            {
                int armedTicks = m_hoverTicks - kArmingStartTicks;
                bool isDimPhase = (armedTicks / kFlashPeriodTicks) % 2 == 0;
                m_host.SetHoverFlashOpacity(m_hoverWindow, isDimPhase ? kFlashDimOpacity : 1.0);
            }

            if (m_hoverTicks >= kBringForwardTicks)
            {
                if (m_isHoverArming)
                {
                    m_host.EndHoverFlash(m_hoverWindow);
                    m_isHoverArming = false;
                }

                m_host.RaiseHoverWindow(m_hoverWindow);

                // Counted again from zero, so resting on it longer doesn't raise it again straight away
                m_hoverTicks = 0;
            }
        }
    }
}
