namespace AJut.Storage.ListOperations
{
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;

    public class ListElementRemoveOperation : IListOperation
    {
        public IEnumerable Elements { get; }

        public ListElementRemoveOperation(IEnumerable elements)
        {
            this.Elements = elements;
        }

        public ListElementRemoveOperation(params object[] elements) : this((IEnumerable)elements) { }
    }

    public class ListElementRemoveOperation<T> : ListElementRemoveOperation, IListOperation<T>
    {
        new public IEnumerable<T> Elements => base.Elements.OfType<T>();
        public ListElementRemoveOperation (IEnumerable<T> elements) : base(elements) { }
        public ListElementRemoveOperation (params T[] elements) : base(elements) { }
    }
}
