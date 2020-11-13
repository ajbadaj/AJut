
namespace AJut.Interfaces
{
    /// <summary>
    /// Provides an interface by which an object can "match" another object
    /// and can then be updated by said other object. (See <see cref="AJut.ListXT.UpdateWith"/>)
    /// </summary>
    public interface IAmUpdatable
    {
        bool Matches(object other);
        void UpdateWith(object other);
    }
}
