namespace AJut.Text
{
    using AJut.Text;
    using System;
    using System.Collections.Generic;

    public class TrackedString : IComparable<TrackedString>
    {
        private string m_value;
        public event EventHandler ValueChanged;

        public string StringValue
        {
            get { return m_value; }
            set
            {
                // UpdateTrackedString will call SetValueInternal
                if (this.Source.UpdateTrackedString(this, value))
                {
                    this.ValueChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public int OffsetInSource { get; protected internal set; }

        public TrackedStringManager Source
        {
            get;
            protected set;
        }

        internal TrackedString(TrackedStringManager source)
        {
            this.Source = source;
            this.OffsetInSource = -1;
            m_value = null;
        }

        /// <summary>
        /// (Internal) Builds a TrackedString - To use in AppendNew (does not trigger any updates or lookup into source).
        /// </summary>
        internal TrackedString(TrackedStringManager source, int startIndex, string value)
        {
            this.Source = source;
            this.OffsetInSource = startIndex;
            m_value = value;
        }

        internal TrackedString(TrackedStringManager source, int startIndex, int endIndex)
        {
            this.Source = source;
            this.OffsetInSource = startIndex;

            if (!source.IsInPlaceholderMode)
            {
                // Note: The point of calling SetValueInternal is that derived instances
                //          can then process it. At this point, the derived instance constructor
                //          hasn't been called so it won't be used.
                m_value = source.Text.SubstringWithIndices(startIndex, endIndex);
            }
        }

        /// <summary>
        /// Resets the string value to the substring starting with the offset, and ending with the new index.
        /// No calls to Source are made.
        /// </summary>
        /// <param name="endIndex">The new end index of the substring</param>
        protected internal void ResetStringInternal(int endIndex)
        {
            SetValueInternal(this.Source.Text.SubstringWithIndices(this.OffsetInSource, endIndex));
        }

        /// <summary>
        /// Sets the value without triggering an update with the tracker source
        /// </summary>
        protected virtual internal void SetValueInternal(string value)
        {
            m_value = value;
        }

        public override string ToString()
        {
            return this.StringValue;
        }

        public int CompareTo(TrackedString other)
        {
            return this.OffsetInSource.CompareTo(other.OffsetInSource);
        }

        public static implicit operator string (TrackedString s)
        {
            return s.StringValue;
        }

        public static readonly OffsetComparer Comparer = new OffsetComparer();
    }

    public class OffsetComparer : IComparer<TrackedString>
    {
        public int Compare(TrackedString x, TrackedString y)
        {
            return x.CompareTo(y);
        }
    }

}
