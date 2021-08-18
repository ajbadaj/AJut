namespace AJut.Storage.ListOperations
{
    using System.Collections.Generic;
    using System.Linq;

    public class NotifyListCompositionChangedEventArgs
    {
        public IListOperation[] Operations { get; }

        public NotifyListCompositionChangedEventArgs (IEnumerable<IListOperation> operations)
        {
            this.Operations = operations.ToArray();
        }
        public NotifyListCompositionChangedEventArgs (params IListOperation[] operations)
        {
            this.Operations = operations;
        }
    }

    public class NotifyListCompositionChangedEventArgs<T> : NotifyListCompositionChangedEventArgs
    {
        new public IEnumerable<IListOperation<T>> Operations { get; }

        public NotifyListCompositionChangedEventArgs (IEnumerable<IListOperation<T>> operations) : base (operations)
        {
            this.Operations = operations.ToArray();
        }
        public NotifyListCompositionChangedEventArgs (params IListOperation<T>[] operations) : base(operations)
        {
            this.Operations = operations;
        }
    }
}
