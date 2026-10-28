namespace AJut.UX.Controls
{
    using System.Collections.Generic;
    using AppKit;

    /// <summary>
    /// Where a <see cref="FillStack"/> child sits across the stack (horizontally in a vertical stack,
    /// vertically in a horizontal one).
    /// </summary>
    public enum eFillStackCrossAlignment
    {
        /// <summary>Fills the cross axis, inside the stack's padding</summary>
        Stretch,

        /// <summary>Hugs the leading edge (vertical stack) or the top edge (horizontal stack), at its own size</summary>
        Start,

        /// <summary>Centered on the cross axis, at its own size</summary>
        Center,

        /// <summary>Hugs the trailing edge (vertical stack) or the bottom edge (horizontal stack), at its own size</summary>
        End,
    }

    // ===========[ FillStack ]==================================================
    // A stack panel that can fill, which NSStackView cannot.
    //
    // NSStackView has no fill alignment: NSLayoutAttribute.Width is not a valid alignment for a
    // vertical stack, and asking for it silently trailing-aligns every child. Its Fill distribution
    // picks which child takes the leftover space by hugging priority, and a nested NSStackView
    // reports its hugging through its own HuggingPriority(orientation), not through the view's
    // content hugging, so a nested stack quietly takes the leftover space instead of the child you
    // meant. Getting NSStackView to behave takes a width constraint per child plus hugging fixes on
    // every nested stack, which is the kind of thing that works until the next child is added.
    //
    // FillStack skips all of that and writes the constraint chain itself:
    //  - Every child is chained on the main axis: first child to the leading padding, each next one
    //    to the end of the previous one plus Spacing.
    //  - On the cross axis each child follows its eFillStackCrossAlignment, Stretch by default.
    //  - Children added with grows = true are "growers": they share the leftover main-axis space
    //    equally (equal size constraints between them), and the last child is pinned to the far
    //    padding. With no grower, the last child is only kept inside the far padding (<=), so the
    //    stack's fitting size hugs its content and extra space is left empty at the end.
    //  - A grower's main-axis hugging is dropped to 1 so it, and not a neighbor, gives way. The
    //    original hugging is restored when the child is removed. For a nested NSStackView this goes
    //    through the stack's own HuggingPriority, for the reason above.
    //
    // Use NSStackView when every child hugs its content and nothing needs to fill. Use FillStack
    // when something has to fill.
    public class FillStack : NSView
    {
        private const float kGrowerHuggingPriority = 1f;

        private readonly List<Entry> m_entries = new List<Entry>();
        private readonly List<NSLayoutConstraint> m_activeConstraints = new List<NSLayoutConstraint>();
        private NSUserInterfaceLayoutOrientation m_orientation;
        private double m_spacing;
        private NSEdgeInsets m_padding;

        // ===========[ Setup/Construction/Teardown ]===========================
        public FillStack () : this(NSUserInterfaceLayoutOrientation.Vertical)
        {
        }

        public FillStack (NSUserInterfaceLayoutOrientation orientation, double spacing = 0.0)
        {
            m_orientation = orientation;
            m_spacing = spacing;
        }

        // ===========[ Properties ]============================================
        /// <summary>Top-left origin, so a FillStack can be a scroll view's document without anchoring to the bottom</summary>
        public override bool IsFlipped => true;

        /// <summary>Which axis children are chained along: Vertical stacks top to bottom, Horizontal stacks leading to trailing</summary>
        public NSUserInterfaceLayoutOrientation Orientation
        {
            get => m_orientation;
            set
            {
                if (m_orientation != value)
                {
                    this.SwapGrowerHuggingAxis(m_orientation, value);
                    m_orientation = value;
                    this.RebuildConstraints();
                }
            }
        }

        /// <summary>Gap between neighboring children, in points</summary>
        public double Spacing
        {
            get => m_spacing;
            set
            {
                if (m_spacing != value)
                {
                    m_spacing = value;
                    this.RebuildConstraints();
                }
            }
        }

        /// <summary>Space between the stack's edges and its children, in points</summary>
        public NSEdgeInsets Padding
        {
            get => m_padding;
            set
            {
                m_padding = value;
                this.RebuildConstraints();
            }
        }

        /// <summary>The children, in stack order</summary>
        public IEnumerable<NSView> Children
        {
            get
            {
                foreach (Entry entry in m_entries)
                {
                    yield return entry.View;
                }
            }
        }

        /// <summary>How many children the stack holds</summary>
        public int ChildCount => m_entries.Count;

        // ===========[ Public Interface Methods ]==============================
        /// <summary>
        /// Adds a child to the end of the stack. A child added with <paramref name="grows"/> takes a share
        /// of the leftover main-axis space; everything else sits at its own size.
        /// </summary>
        public void Add (NSView view, bool grows = false, eFillStackCrossAlignment alignment = eFillStackCrossAlignment.Stretch)
        {
            this.Insert(m_entries.Count, view, grows, alignment);
        }

        /// <summary>Inserts a child at <paramref name="index"/> (clamped to the valid range)</summary>
        public void Insert (int index, NSView view, bool grows = false, eFillStackCrossAlignment alignment = eFillStackCrossAlignment.Stretch)
        {
            if (view == null || this.IndexOf(view) != -1)
            {
                return;
            }

            if (index < 0)
            {
                index = 0;
            }
            else if (index > m_entries.Count)
            {
                index = m_entries.Count;
            }

            var entry = new Entry(view, alignment);
            entry.OriginalMainAxisHugging = GetHugging(view, MainAxisOf(m_orientation));
            entry.Grows = grows;
            if (grows)
            {
                SetHugging(view, kGrowerHuggingPriority, MainAxisOf(m_orientation));
            }

            view.TranslatesAutoresizingMaskIntoConstraints = false;
            m_entries.Insert(index, entry);
            this.AddSubview(view);
            this.RebuildConstraints();
        }

        /// <summary>Removes a child, restoring the hugging it had before it was added. Returns false if it was not a child.</summary>
        public bool Remove (NSView view)
        {
            int index = this.IndexOf(view);
            if (index == -1)
            {
                return false;
            }

            Entry entry = m_entries[index];
            m_entries.RemoveAt(index);
            if (entry.Grows)
            {
                SetHugging(view, entry.OriginalMainAxisHugging, MainAxisOf(m_orientation));
            }

            // Drop the chain before the view leaves, so nothing in the active list still points at it
            this.ClearConstraints();
            view.RemoveFromSuperview();
            this.RebuildConstraints();
            return true;
        }

        /// <summary>Removes every child</summary>
        public void Clear ()
        {
            this.ClearConstraints();
            foreach (Entry entry in m_entries)
            {
                if (entry.Grows)
                {
                    SetHugging(entry.View, entry.OriginalMainAxisHugging, MainAxisOf(m_orientation));
                }

                entry.View.RemoveFromSuperview();
            }

            m_entries.Clear();
        }

        /// <summary>Changes whether an existing child takes a share of the leftover space</summary>
        public void SetGrows (NSView view, bool grows)
        {
            int index = this.IndexOf(view);
            if (index == -1 || m_entries[index].Grows == grows)
            {
                return;
            }

            Entry entry = m_entries[index];
            entry.Grows = grows;
            if (grows)
            {
                entry.OriginalMainAxisHugging = GetHugging(view, MainAxisOf(m_orientation));
                SetHugging(view, kGrowerHuggingPriority, MainAxisOf(m_orientation));
            }
            else
            {
                SetHugging(view, entry.OriginalMainAxisHugging, MainAxisOf(m_orientation));
            }

            this.RebuildConstraints();
        }

        /// <summary>Changes where an existing child sits on the cross axis</summary>
        public void SetCrossAlignment (NSView view, eFillStackCrossAlignment alignment)
        {
            int index = this.IndexOf(view);
            if (index == -1 || m_entries[index].Alignment == alignment)
            {
                return;
            }

            m_entries[index].Alignment = alignment;
            this.RebuildConstraints();
        }

        /// <summary>Is the given child a grower</summary>
        public bool GetGrows (NSView view)
        {
            int index = this.IndexOf(view);
            return index != -1 && m_entries[index].Grows;
        }

        // ===========[ Helper Methods ]========================================
        private int IndexOf (NSView view)
        {
            for (int i = 0; i < m_entries.Count; ++i)
            {
                if (m_entries[i].View == view)
                {
                    return i;
                }
            }

            return -1;
        }

        private void ClearConstraints ()
        {
            if (m_activeConstraints.Count > 0)
            {
                NSLayoutConstraint.DeactivateConstraints(m_activeConstraints.ToArray());
                m_activeConstraints.Clear();
            }
        }

        private void RebuildConstraints ()
        {
            this.ClearConstraints();
            if (m_entries.Count == 0)
            {
                return;
            }

            bool hasGrower = false;
            foreach (Entry entry in m_entries)
            {
                if (entry.Grows)
                {
                    hasGrower = true;
                    break;
                }
            }

            if (m_orientation == NSUserInterfaceLayoutOrientation.Vertical)
            {
                this.BuildVerticalChain(hasGrower);
            }
            else
            {
                this.BuildHorizontalChain(hasGrower);
            }

            NSLayoutConstraint.ActivateConstraints(m_activeConstraints.ToArray());
        }

        // The vertical and horizontal chains are written out separately rather than through a
        // shared anchor helper: X and Y anchors are different generic types in the bindings, and
        // keeping each one concrete keeps the constraint code plain to read and to check.
        private void BuildVerticalChain (bool hasGrower)
        {
            NSView previous = null;
            NSView firstGrower = null;
            foreach (Entry entry in m_entries)
            {
                NSView view = entry.View;

                // 1. Main axis: chain top to bottom
                if (previous == null)
                {
                    m_activeConstraints.Add(view.TopAnchor.ConstraintEqualTo(this.TopAnchor, (nfloat)m_padding.Top));
                }
                else
                {
                    m_activeConstraints.Add(view.TopAnchor.ConstraintEqualTo(previous.BottomAnchor, (nfloat)m_spacing));
                }

                // 2. Growers share the leftover equally
                if (entry.Grows)
                {
                    if (firstGrower == null)
                    {
                        firstGrower = view;
                    }
                    else
                    {
                        m_activeConstraints.Add(view.HeightAnchor.ConstraintEqualTo(firstGrower.HeightAnchor));
                    }
                }

                // 3. Cross axis
                switch (entry.Alignment)
                {
                    case eFillStackCrossAlignment.Stretch:
                        m_activeConstraints.Add(view.LeadingAnchor.ConstraintEqualTo(this.LeadingAnchor, (nfloat)m_padding.Left));
                        m_activeConstraints.Add(view.TrailingAnchor.ConstraintEqualTo(this.TrailingAnchor, -(nfloat)m_padding.Right));
                        break;

                    case eFillStackCrossAlignment.Start:
                        m_activeConstraints.Add(view.LeadingAnchor.ConstraintEqualTo(this.LeadingAnchor, (nfloat)m_padding.Left));
                        m_activeConstraints.Add(view.TrailingAnchor.ConstraintLessThanOrEqualTo(this.TrailingAnchor, -(nfloat)m_padding.Right));
                        break;

                    case eFillStackCrossAlignment.Center:
                        m_activeConstraints.Add(view.CenterXAnchor.ConstraintEqualTo(this.CenterXAnchor, (nfloat)((m_padding.Left - m_padding.Right) / 2.0)));
                        m_activeConstraints.Add(view.LeadingAnchor.ConstraintGreaterThanOrEqualTo(this.LeadingAnchor, (nfloat)m_padding.Left));
                        break;

                    case eFillStackCrossAlignment.End:
                        m_activeConstraints.Add(view.TrailingAnchor.ConstraintEqualTo(this.TrailingAnchor, -(nfloat)m_padding.Right));
                        m_activeConstraints.Add(view.LeadingAnchor.ConstraintGreaterThanOrEqualTo(this.LeadingAnchor, (nfloat)m_padding.Left));
                        break;
                }

                previous = view;
            }

            // 4. The far end: pinned when something grows, otherwise only kept inside
            if (hasGrower)
            {
                m_activeConstraints.Add(previous.BottomAnchor.ConstraintEqualTo(this.BottomAnchor, -(nfloat)m_padding.Bottom));
            }
            else
            {
                m_activeConstraints.Add(previous.BottomAnchor.ConstraintLessThanOrEqualTo(this.BottomAnchor, -(nfloat)m_padding.Bottom));
            }
        }

        private void BuildHorizontalChain (bool hasGrower)
        {
            NSView previous = null;
            NSView firstGrower = null;
            foreach (Entry entry in m_entries)
            {
                NSView view = entry.View;

                // 1. Main axis: chain leading to trailing
                if (previous == null)
                {
                    m_activeConstraints.Add(view.LeadingAnchor.ConstraintEqualTo(this.LeadingAnchor, (nfloat)m_padding.Left));
                }
                else
                {
                    m_activeConstraints.Add(view.LeadingAnchor.ConstraintEqualTo(previous.TrailingAnchor, (nfloat)m_spacing));
                }

                // 2. Growers share the leftover equally
                if (entry.Grows)
                {
                    if (firstGrower == null)
                    {
                        firstGrower = view;
                    }
                    else
                    {
                        m_activeConstraints.Add(view.WidthAnchor.ConstraintEqualTo(firstGrower.WidthAnchor));
                    }
                }

                // 3. Cross axis
                switch (entry.Alignment)
                {
                    case eFillStackCrossAlignment.Stretch:
                        m_activeConstraints.Add(view.TopAnchor.ConstraintEqualTo(this.TopAnchor, (nfloat)m_padding.Top));
                        m_activeConstraints.Add(view.BottomAnchor.ConstraintEqualTo(this.BottomAnchor, -(nfloat)m_padding.Bottom));
                        break;

                    case eFillStackCrossAlignment.Start:
                        m_activeConstraints.Add(view.TopAnchor.ConstraintEqualTo(this.TopAnchor, (nfloat)m_padding.Top));
                        m_activeConstraints.Add(view.BottomAnchor.ConstraintLessThanOrEqualTo(this.BottomAnchor, -(nfloat)m_padding.Bottom));
                        break;

                    case eFillStackCrossAlignment.Center:
                        m_activeConstraints.Add(view.CenterYAnchor.ConstraintEqualTo(this.CenterYAnchor, (nfloat)((m_padding.Top - m_padding.Bottom) / 2.0)));
                        m_activeConstraints.Add(view.TopAnchor.ConstraintGreaterThanOrEqualTo(this.TopAnchor, (nfloat)m_padding.Top));
                        break;

                    case eFillStackCrossAlignment.End:
                        m_activeConstraints.Add(view.BottomAnchor.ConstraintEqualTo(this.BottomAnchor, -(nfloat)m_padding.Bottom));
                        m_activeConstraints.Add(view.TopAnchor.ConstraintGreaterThanOrEqualTo(this.TopAnchor, (nfloat)m_padding.Top));
                        break;
                }

                previous = view;
            }

            // 4. The far end: pinned when something grows, otherwise only kept inside
            if (hasGrower)
            {
                m_activeConstraints.Add(previous.TrailingAnchor.ConstraintEqualTo(this.TrailingAnchor, -(nfloat)m_padding.Right));
            }
            else
            {
                m_activeConstraints.Add(previous.TrailingAnchor.ConstraintLessThanOrEqualTo(this.TrailingAnchor, -(nfloat)m_padding.Right));
            }
        }

        // When the stack turns, each grower's low hugging has to move to the new main axis, and
        // the old axis gets back what it had
        private void SwapGrowerHuggingAxis (NSUserInterfaceLayoutOrientation from, NSUserInterfaceLayoutOrientation to)
        {
            foreach (Entry entry in m_entries)
            {
                if (entry.Grows)
                {
                    SetHugging(entry.View, entry.OriginalMainAxisHugging, MainAxisOf(from));
                    entry.OriginalMainAxisHugging = GetHugging(entry.View, MainAxisOf(to));
                    SetHugging(entry.View, kGrowerHuggingPriority, MainAxisOf(to));
                }
            }
        }

        private static NSLayoutConstraintOrientation MainAxisOf (NSUserInterfaceLayoutOrientation orientation)
        {
            return orientation == NSUserInterfaceLayoutOrientation.Vertical
                ? NSLayoutConstraintOrientation.Vertical
                : NSLayoutConstraintOrientation.Horizontal;
        }

        // An NSStackView keeps its hugging separately from the view-level content hugging, see the
        // class comment
        private static float GetHugging (NSView view, NSLayoutConstraintOrientation axis)
        {
            if (view is NSStackView stack)
            {
                return stack.HuggingPriority(axis);
            }

            return view.GetContentHuggingPriorityForOrientation(axis);
        }

        private static void SetHugging (NSView view, float priority, NSLayoutConstraintOrientation axis)
        {
            if (view is NSStackView stack)
            {
                stack.SetHuggingPriority(priority, axis);
            }
            else
            {
                view.SetContentHuggingPriorityForOrientation(priority, axis);
            }
        }

        // ===========[ Subclasses/structs ]====================================
        private sealed class Entry
        {
            public Entry (NSView view, eFillStackCrossAlignment alignment)
            {
                this.View = view;
                this.Alignment = alignment;
            }

            public NSView View { get; }
            public eFillStackCrossAlignment Alignment { get; set; }
            public bool Grows { get; set; }
            public float OriginalMainAxisHugging { get; set; }
        }
    }
}
