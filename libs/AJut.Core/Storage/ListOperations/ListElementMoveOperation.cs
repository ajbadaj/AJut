namespace AJut.Storage.ListOperations
{
    public class ListElementMoveOperation : IListOperation
    {
        public int OriginalLocationIndex { get; }
        public int NewLocationIndex { get; }

        public object MovedElement { get; }

        public ListElementMoveOperation (object movedElement, int originalLocationIndex, int newLocationIndex)
        {
            this.MovedElement = movedElement;
            this.OriginalLocationIndex = originalLocationIndex;
            this.NewLocationIndex = newLocationIndex;
        }

        public override string ToString () => $"Move {this.MovedElement} from [{this.OriginalLocationIndex}] → [{this.NewLocationIndex}]";
    }

    public class ListElementMoveOperation<T> : ListElementMoveOperation, IListOperation<T>
    {
        new public T MovedElement => (T)base.MovedElement;

        public ListElementMoveOperation (T movedElement, int originalLocationIndex, int newLocationIndex)
            : base(movedElement, originalLocationIndex, newLocationIndex)
        {
        }
    }
}
