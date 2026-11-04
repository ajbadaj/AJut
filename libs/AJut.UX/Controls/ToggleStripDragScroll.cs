namespace AJut.UX.Controls
{
    using System;

    /// <summary>
    /// Tells a click-and-drag that scrolls a ToggleStrip in Scroll mode apart from a click that toggles an item, the same
    /// on every pointer stack. A press starts tracking; once the pointer has moved past <see cref="Threshold"/> along the
    /// strip, it is a drag: each move scrolls the strip, and the release must cancel the item's click. Positions are along
    /// the strip, x for a horizontal strip and y for a vertical one.
    /// </summary>
    public sealed class ToggleStripDragScroll
    {
        /// <summary>How far the pointer moves along the strip, by default, before a press becomes a drag.</summary>
        public const double kDefaultThreshold = 4.0;

        private double m_lastPosition;
        private double m_startPosition;
        private bool m_isTracking;

        public ToggleStripDragScroll (double threshold = kDefaultThreshold)
        {
            this.Threshold = threshold;
        }

        /// <summary>How far the pointer moves along the strip before a press becomes a drag.</summary>
        public double Threshold { get; }

        /// <summary>True once the press being tracked has moved past the threshold.</summary>
        public bool IsDragging { get; private set; }

        /// <summary>Starts tracking a press at <paramref name="position"/>.</summary>
        public void Begin (double position)
        {
            m_isTracking = true;
            this.IsDragging = false;
            m_startPosition = position;
            m_lastPosition = position;
        }

        /// <summary>
        /// The pointer moved to <paramref name="position"/>. Returns how far it moved along the strip since the last move
        /// once the press is a drag (the strip's content follows it, so subtract this from the scroll offset), and 0 before.
        /// </summary>
        public double Move (double position)
        {
            if (!m_isTracking)
            {
                return 0.0;
            }

            if (!this.IsDragging)
            {
                if (Math.Abs(position - m_startPosition) < this.Threshold)
                {
                    return 0.0;
                }

                this.IsDragging = true;
            }

            double delta = position - m_lastPosition;
            m_lastPosition = position;
            return delta;
        }

        /// <summary>Stops tracking the press. Returns true when it was a drag, so the release must not click the item under it.</summary>
        public bool End ()
        {
            bool wasDrag = this.IsDragging;
            m_isTracking = false;
            this.IsDragging = false;
            return wasDrag;
        }
    }
}
