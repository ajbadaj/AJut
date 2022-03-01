namespace AJut.Storage
{
    using System.Collections.Generic;

    /// <summary>
    /// Represents an insertion into a list, used as a special way to manage using the layered approach <see cref="Stratabase"/> provides, see <see cref="StrataPropertyListAccess{TElement}"/>
    /// </summary>
    public class StratabaseListInsertion<TElement>
    {
        internal const int kBaseline = -1;
        public StratabaseListInsertion (int layerIndex, int index, TElement value)
        {
            this.SourceLayer = layerIndex;
            this.Index = index;
            this.Value = value;
        }

        // ===========================[ Properties ]===========================

        public bool IsBaseline => this.SourceLayer == kBaseline;
        public int SourceLayer { get; }
        public int Index { get; set; }
        public TElement Value { get; }

        // ===========================[ Methods ]==============================

        public void Increment ()
        {
            ++this.Index;
        }

        public void Decrement ()
        {
            --this.Index;
        }

        // ===========================[ Classes ]==============================

        public class InsertionBuilder
        {
            private StrataPropertyListAccess<TElement> m_owner;
            private int m_index;
            private TElement[] m_values;

            public InsertionBuilder (StrataPropertyListAccess<TElement> owner, int index, TElement[] values)
            {
                m_owner = owner;
                m_index = index;
                m_values = values;
            }

            public void StoreInBaseline ()
            {
                m_owner.GetBaselineChanges(generateIfNonExistant: true).AddEach(this.GenerateInsertions());
            }

            public void StoreInOverrideLayer (int layerIndex)
            {
                m_owner.GetOverrideChanges(layerIndex, generateIfNonExistant: true).AddEach(this.GenerateInsertions(layerIndex));
            }

            private List<StratabaseListInsertion<TElement>> GenerateInsertions (int layerIndex = -1)
            {
                int index = m_index;
                var toAdd = new List<StratabaseListInsertion<TElement>>();
                foreach (TElement value in m_values)
                {
                    toAdd.Add(new StratabaseListInsertion<TElement>(layerIndex, index++, value));
                }

                return toAdd;
            }

        }
    }

}
