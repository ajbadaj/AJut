namespace AJut.Storage
{
    public interface IStrataPropertyValueManipulator
    {
        void ClearBaselineValue ();
        void ClearOverrideValue (int layerIndex);
    }

    public interface IStrataPropertyValueManipulator<in TProperty> : IStrataPropertyValueManipulator
    {
        bool SetBaselineValue (TProperty value);
        bool SetOverrideValue (int layerIndex, TProperty value);
    }
}