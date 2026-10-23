namespace AJut.UX.Controls
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using AJut.UX.Theming;
    using AppKit;
    using Foundation;

    // ===========[ ThemedSplitView ]============================================
    // Drag-to-resize panes, the role GridSplitter fills on WinUI3. AppKit has no Grid to put a
    // splitter in, and it already has a native split view that handles dragging, the cursor,
    // keyboard access and autosaving divider positions, so this builds on NSSplitView rather than
    // reimplementing it. What it adds:
    //  - The divider drawn in a ThemedColors key, at a set thickness.
    //  - MinPanelSize: no pane can be dragged smaller than this, which NSSplitView does not do on
    //    its own (the WinUI3 GridSplitter's MinPanelSize, same default).
    //  - InitialSplitRatio: where the first divider starts, as a fraction of the split length.
    //    NSSplitView keeps a divider wherever the first layout pass happened to put it, so a
    //    position set before the view has a size is lost; the ratio is applied on the first
    //    layout that has a real size instead.
    //
    // Panes go in with AddArrangedSubview. With the default holding priorities every pane gives
    // way equally when the split view resizes; give a pane a higher holding priority
    // (SetHoldingPriority) to keep it its size, the way a sidebar usually should.
    //
    // Like NSWindow, NSSplitView's C# events go through an internal delegate, so this sets its own
    // Delegate and must not have another one set on it.
    public class ThemedSplitView : NSSplitView
    {
        private const double kDefaultMinPanelSize = 30.0;
        private const double kDefaultThickness = 1.0;

        private readonly SplitDelegate m_delegate;
        private string m_dividerColorKey = ThemedColorKeys.Splitter_Divider;
        private double m_thickness = kDefaultThickness;
        private double m_initialSplitRatio = 0.5;
        private bool m_hasPlacedInitialSplit;
        private double[] m_pendingPaneLengths;

        // ===========[ Setup/Construction/Teardown ]===========================
        /// <summary>Creates a split view; vertical dividers (panes side by side) unless <paramref name="isVertical"/> is false</summary>
        public ThemedSplitView (bool isVertical = true)
        {
            this.IsVertical = isVertical;
            this.DividerStyle = NSSplitViewDividerStyle.Thin;
            m_delegate = new SplitDelegate(this);
            this.Delegate = m_delegate;
        }

        // ===========[ Properties ]============================================
        /// <summary>The smallest a pane can be dragged to, in points</summary>
        public double MinPanelSize { get; set; } = kDefaultMinPanelSize;

        /// <summary>
        /// Where the first divider starts, from 0 (all the way leading or top) to 1. Applied once, on the
        /// first layout with a real size; after that the user's drag (or SetPositionOfDivider) decides.
        /// </summary>
        public double InitialSplitRatio
        {
            get => m_initialSplitRatio;
            set => m_initialSplitRatio = value < 0.0 ? 0.0 : (value > 1.0 ? 1.0 : value);
        }

        /// <summary>The <see cref="ThemedColors"/> key the divider is drawn in</summary>
        public string DividerColorKey
        {
            get => m_dividerColorKey;
            set
            {
                m_dividerColorKey = value;
                this.NeedsDisplay = true;
            }
        }

        /// <summary>Divider thickness in points, which is also how wide a target it is to grab</summary>
        public double Thickness
        {
            get => m_thickness;
            set
            {
                m_thickness = value;
                this.AdjustSubviews();
                this.NeedsDisplay = true;
            }
        }

        public override NSColor DividerColor => ThemedColors.Get(m_dividerColorKey);
        public override nfloat DividerThickness => (nfloat)m_thickness;

        // ===========[ Public Interface Methods ]==============================
        /// <summary>
        /// Sets every pane's length along the split, in pane order, on the next layout that has a real size. The lengths
        /// are scaled to fit the space the panes share (the split's length less its dividers), so proportions work as
        /// well as points. Replaces <see cref="InitialSplitRatio"/> when it arrives before the first layout.
        /// </summary>
        /// <remarks>
        /// NSSplitView only places a divider where it is told to, and a pane added later starts at whatever size it had,
        /// which for a new view is none. With three or more panes, only the first divider is placed by the initial
        /// ratio, so the rest need their lengths given here or a pane can start (and stay) zero length.
        /// </remarks>
        public void SetPaneLengths (IReadOnlyList<double> lengths)
        {
            m_pendingPaneLengths = lengths?.ToArray();
            this.NeedsLayout = true;
        }

        public override void Layout ()
        {
            base.Layout();
            int paneCount = this.ArrangedSubviews.Length;
            if (paneCount < 2)
            {
                return;
            }

            double length = this.IsVertical ? (double)this.Bounds.Width : (double)this.Bounds.Height;
            if (length <= 0.0)
            {
                return;
            }

            if (m_pendingPaneLengths != null)
            {
                this.PlacePanes(m_pendingPaneLengths, length);
                m_pendingPaneLengths = null;
                m_hasPlacedInitialSplit = true;
                return;
            }

            if (m_hasPlacedInitialSplit)
            {
                return;
            }

            m_hasPlacedInitialSplit = true;
            this.SetPositionOfDivider((nfloat)(length * m_initialSplitRatio), 0);
        }

        public override void ViewDidChangeEffectiveAppearance ()
        {
            base.ViewDidChangeEffectiveAppearance();
            this.NeedsDisplay = true;
        }

        // ===========[ Helper Methods ]========================================
        // Each divider goes after its pane, so every divider is placed, scaled so the panes fill what the dividers leave.
        // With nothing to scale by (all zero), the panes share it equally.
        //
        // NSSplitView never lets a divider pass its neighbors, so one pass in order is not enough when the panes start
        // somewhere else. New panes (every pane, on a layout load) can start piled up at one end: with dividers 0 and 1
        // both at the far left, divider 0 cannot move right past divider 1, divider 1 then moves, and pane 0 is left
        // zero wide. Each pass frees the next divider back, so as many passes as there are dividers always lands them.
        private void PlacePanes (double[] lengths, double splitLength)
        {
            int paneCount = this.ArrangedSubviews.Length;
            int dividerCount = paneCount - 1;
            double available = Math.Max(0.0, splitLength - (dividerCount * m_thickness));

            double given = 0.0;
            for (int i = 0; i < paneCount; ++i)
            {
                given += i < lengths.Length ? Math.Max(0.0, lengths[i]) : 0.0;
            }

            var positions = new double[dividerCount];
            double position = 0.0;
            for (int i = 0; i < dividerCount; ++i)
            {
                double pane = given > 0.0
                    ? available * ((i < lengths.Length ? Math.Max(0.0, lengths[i]) : 0.0) / given)
                    : available / paneCount;

                position += pane;
                positions[i] = position;
                position += m_thickness;
            }

            for (int pass = 0; pass < dividerCount; ++pass)
            {
                for (int i = 0; i < dividerCount; ++i)
                {
                    this.SetPositionOfDivider((nfloat)positions[i], i);
                }
            }
        }

        // ===========[ Subclasses/structs ]====================================
        // The delegate is asked how far each divider may go. The proposed values are the extremes
        // the panes themselves allow, so pulling each in by MinPanelSize is what keeps both panes
        // beside the divider at least that big.
        private sealed class SplitDelegate : NSSplitViewDelegate
        {
            private readonly ThemedSplitView m_owner;

            public SplitDelegate (ThemedSplitView owner)
            {
                m_owner = owner;
            }

            public override nfloat SetMinCoordinateOfSubview (NSSplitView splitView, nfloat proposedMinimumPosition, nint subviewDividerIndex)
            {
                return proposedMinimumPosition + (nfloat)m_owner.MinPanelSize;
            }

            public override nfloat SetMaxCoordinateOfSubview (NSSplitView splitView, nfloat proposedMaximumPosition, nint subviewDividerIndex)
            {
                return proposedMaximumPosition - (nfloat)m_owner.MinPanelSize;
            }
        }
    }
}
