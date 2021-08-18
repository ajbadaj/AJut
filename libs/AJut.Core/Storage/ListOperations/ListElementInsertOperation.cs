namespace AJut.Storage.ListOperations
{
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;

    public class ListElementInsertOperation : IListOperation
    {
        public int InsertIndex { get; }
        public IEnumerable Elements { get; }

        public ListElementInsertOperation(int insertIndex, IEnumerable elements)
        {
            this.InsertIndex = insertIndex;
            this.Elements = elements;
        }
    }

    public class ListElementInsertOperation<T> : ListElementInsertOperation, IListOperation<T>
    {
        new public IEnumerable<T> Elements => base.Elements.OfType<T>();

        public ListElementInsertOperation (int insertIndex, params T[] elements) : base(insertIndex, elements) { }
        public ListElementInsertOperation (int insertIndex, IEnumerable<T> elements) : base(insertIndex, elements) { }
    }

}
