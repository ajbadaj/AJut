namespace AJut.Text
{
    using AJut.Text;
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Text;
    using System.Diagnostics;
    using System.Linq;

    /// <summary>
    /// Tracks substrings within it's Text. 
    /// </summary>
    /// <remarks>
    /// There is a placeholder mode, whereby you can build a SubstringTracker and
    /// track items therein without starting off with the complete picture.
    /// </remarks>
    public class TrackedStringManager : IEquatable<string>, IEnumerable<TrackedString>
    {
        private List<TrackedString> m_trackedStrings = new List<TrackedString>();

        PlacholderData m_placeholderData;

        /// <summary>
        /// The source text being tracked (and updated)
        /// </summary>
        public string Text { get; private set; }

        /// <summary>
        /// Indicates if any changes have been made to the source text
        /// </summary>
        public bool HasChanges { get; internal protected set; }

        public int Count { get { return m_trackedStrings.Count; } }

        public bool IsInPlaceholderMode
        {
            get { return m_placeholderData != null; }
        }

        /// <summary>
        /// Main constructor
        /// </summary>
        public TrackedStringManager(string sourceText)
        {
            this.Text = sourceText;
        }

        /// <summary>
        /// Placeholder mode constructor
        /// </summary>
        public TrackedStringManager(IndexTrackingStringBuilder placeholderSource)
        {
            BeginPlaceholderMode(placeholderSource, true);
        }

        /// <summary>
        /// Begins tracking substring
        /// </summary>
        public TrackedString Track(int startIndex, int length)
        {
            this.HasChanges = true;

            int endIndex = startIndex + length - 1;
            if (this.IsInPlaceholderMode)
            {
                TrackedString s = new TrackedString(this, startIndex, endIndex);
                m_placeholderData.AddPlaceholderItem(s, endIndex);
                return s;
            }
            else
            {
                TrackedString s = new TrackedString(this, startIndex, endIndex);
                this.AddItem(s);
                return s;
            }
        }

        /// <summary>
        /// Track by start index &amp; length (convenience utility function)
        /// </summary>
        public TrackedString TrackByIndex(int startIndex, int endIndex)
        {
            return this.Track(startIndex, endIndex - startIndex + 1);
        }

        /// <summary>
        /// Updates the <see cref="TrackedString"/>'s value and updates indexing of all <see cref="TrackedString"/>s that are affected by the change.
        /// </summary>
        /// <param name="item">The item to update</param>
        /// <param name="newValue">The new string for the item to hold</param>
        public bool UpdateTrackedString(TrackedString item, string newValue)
        {
            if (item.StringValue == newValue)
            {
                return false;
            }

            DebugValidateTrackedStringBelongsToThisManager(item);

            int offsetLength = newValue.Length - item.StringValue.Length;

            int replaceStartIndex = item.OffsetInSource;
            int replaceEndIndex = item.OffsetInSource + item.StringValue.Length - 1;
            this.Text = this.Text.Replace(replaceStartIndex, replaceEndIndex, newValue);

            this.UpdateItemsInRange(item.OffsetInSource, offsetLength, item);

            this.HasChanges = true;
            item.SetValueInternal(newValue);
            return true;
        }

        public bool Equals(string other)
        {
            return this.Text == other;
        }

        public IEnumerator<TrackedString> GetEnumerator()
        {
            return m_trackedStrings.GetEnumerator();
        }
        IEnumerator IEnumerable.GetEnumerator()
        {
            return m_trackedStrings.GetEnumerator();
        }

        public int IndexOf(TrackedString trackedString)
        {
            return m_trackedStrings.IndexOf(trackedString);
        }

        internal void BeginPlaceholderMode(IndexTrackingStringBuilder placeholderSource, bool reset = false)
        {
            if (m_placeholderData == null)
            {
                m_placeholderData = new PlacholderData(placeholderSource, reset);
            }
            else if(m_placeholderData.Source != placeholderSource)
            {
                throw new Exception("Attempting to start placeholder mode on a TrackedStringManager that was already in placeholder mode with a different source.");
            }
        }

        /// <summary>
        /// If you built your SubstringTracker in placeholder mode (see placeholder mode constructor), then
        /// you must call this once your text is all setup.
        /// </summary>
        internal void PlaceholderSetupComplete()
        {
            string toInsert = m_placeholderData.Source.Builder.ToString();
            int insertIndex = m_placeholderData.Source.StartIndex;

            // Update the text to contain the string builder's result
            if (m_placeholderData.Reset)
            {
                this.Text = toInsert;

                // We're doing a hard reset
                this.m_trackedStrings.Clear();
            }
            else
            {
                this.Text = this.Text.Insert(insertIndex, toInsert);
            }

            // Insert the placeholders first.
            //  Note: This must be done first so that when we update the existing tracked strings
            TrackedString[] updateSkiplist = new TrackedString[m_placeholderData.PlaceholderItems.Count];
            for (int ind = 0; ind < m_placeholderData.PlaceholderItems.Count; ++ind)
            {
                Tuple<TrackedString, int> placeholderItem = m_placeholderData.PlaceholderItems[ind];
                placeholderItem.Item1.ResetStringInternal(placeholderItem.Item2);
                this.AddItem(placeholderItem.Item1);
                updateSkiplist[ind] = placeholderItem.Item1;
            }

            if (!m_placeholderData.Reset)
            {
                this.UpdateItemsInRange(insertIndex, toInsert.Length, updateSkiplist);
            }

            m_placeholderData = null;
            this.HasChanges = true;
        }

        /// <summary>
        /// (Internal) Begins tracking an item constructed outside of the other Track methods.
        /// </summary>
        protected internal void Track(TrackedString item)
        {
            this.AddItem(item);
        }

        internal void TrackForPlaceholder(TrackedString item, int endIndex)
        {
            if (this.IsInPlaceholderMode)
            {
                m_placeholderData.AddPlaceholderItem(item, endIndex);
            }
        }

        private void AddItem(TrackedString item)
        {
            //m_trackedStrings.InsertSorted(item);
            m_trackedStrings.Add(item);
        }

        private void UpdateItemsInRange(int startIndex, int offsetLength, params TrackedString[] skipList)
        {
            foreach (TrackedString ts in m_trackedStrings)
            {
                if (skipList.Contains(ts))
                {
                    continue;
                }

                // Change is before (increase offset, string should be the same)
                if (ts.OffsetInSource > startIndex)
                {
                    ts.OffsetInSource += offsetLength;
                    DebugValidateTextRemainsTheSameAfterOffsetChange(ts);
                }
                // Change is contained (reset, string will now contain changed/inserted values)
                else if (ts.OffsetInSource + ts.StringValue.Length > startIndex)
                {
                    ts.ResetStringInternal(ts.OffsetInSource + ts.StringValue.Length + offsetLength - 1);
                }
            }
        }

        [Conditional("DEBUG")]
        private void DebugValidateTrackedStringBelongsToThisManager(TrackedString ts)
        {
            if(!m_trackedStrings.Contains(ts))
            {
                throw new Exception("Attempting to use a TrackedString with a TrackedStringManager who is not managing said TrackedString.");
            }
        }
        [Conditional("DEBUG")]
        private void DebugValidateTextRemainsTheSameAfterOffsetChange(TrackedString ts)
        {
            string stringValueAfterOffsetChange = this.Text.Substring(ts.OffsetInSource, ts.StringValue.Length);
            if (ts.StringValue != stringValueAfterOffsetChange)
            {
                throw new Exception(
                    String.Format("After OffsetInotSource changed, the StringValue has erroneously changed from '{0}' to '{1}'",
                                    ts.StringValue, stringValueAfterOffsetChange));
            }
        }


        private class PlacholderData
        {
            public IndexTrackingStringBuilder Source { get; private set; }

            public List<Tuple<TrackedString,int>> PlaceholderItems { get; private set; }

            public bool Reset { get; private set; }

            public PlacholderData(IndexTrackingStringBuilder source, bool reset)
            {
                this.Reset = reset;
                this.Source = source;
                this.PlaceholderItems = new List<Tuple<TrackedString, int>>();
            }

            public void AddPlaceholderItem(TrackedString placeholderTrackedString, int end)
            {
                this.PlaceholderItems.Add(new Tuple<TrackedString, int>(placeholderTrackedString, end));
            }
        }
    }
}
