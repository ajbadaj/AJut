using System.Collections.Generic;
using System.Collections.Specialized;

namespace AJut.Storage.ListOperations
{
    public interface IListOperation { }
    public interface IListOperation<T> : IListOperation { }

    public static class ListOperationXT
    {
        /// <summary>
        /// For ease of building <see cref="NotifyListCompositionChangedEventArgs"/> from list operations
        /// </summary>
        public static IEnumerable<NotifyCollectionChangedEventArgs> ToCollectionChanged (this IListOperation operation)
        {
            if (operation is ListElementInsertOperation adds)
            {
                foreach (object element in adds.Elements)
                {
                    yield return new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, element, adds.InsertIndex);
                }
            }
            else if (operation is ListElementRemoveOperation removes)
            {
                foreach (object element in removes.Elements)
                {
                    yield return new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, element);
                }
            }
            else if (operation is ListElementMoveOperation move)
            {
                yield return new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Move, move.MovedElement, move.NewLocationIndex, move.OriginalLocationIndex);
            }
        }

        public static IEnumerable<IListOperation> GetInsertRemoveOnlyVersion (this IListOperation operation)
        {
            if (operation is ListElementInsertOperation)
            {
                yield return operation;
            }
            else if (operation is ListElementRemoveOperation)
            {
                yield return operation;
            }
            else if (operation is ListElementMoveOperation move)
            {
                yield return new ListElementRemoveOperation(move.MovedElement);
                yield return new ListElementInsertOperation(move.NewLocationIndex, new[] { move.MovedElement });
            }
        }

        public static IEnumerable<IListOperation<T>> GetInsertRemoveOnlyVersion<T> (this IListOperation<T> operation)
        {
            if (operation is ListElementInsertOperation<T> adds)
            {
                yield return adds;
            }
            else if (operation is ListElementRemoveOperation<T>)
            {
                yield return operation;
            }
            else if (operation is ListElementMoveOperation<T> move)
            {
                yield return new ListElementRemoveOperation<T>(move.MovedElement);
                yield return new ListElementInsertOperation<T>(move.NewLocationIndex, new[] { move.MovedElement });
            }
        }
    }
}
