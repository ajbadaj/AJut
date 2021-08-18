namespace AJut.Storage
{
    using System;
    using System.Collections;
    using System.Collections.Specialized;
    using AJut.Storage.ListOperations;

    public interface IObservableList : IList, INotifyCollectionChanged
    {
        event EventHandler<NotifyListCompositionChangedEventArgs> ListCompositionChanged;
    }
}
