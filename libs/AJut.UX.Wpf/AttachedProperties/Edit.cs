namespace AJut.UX.AttachedProperties
{
    using System;
    using System.Windows;

    public static class Edit
    {
        // =============[ Fields ]================
        private static readonly APUtilsRegistrationHelper APUtils = new APUtilsRegistrationHelper(typeof(Edit));

        /// <summary>
        /// A standardized way to specify readonly-ness in an inheritted high level. Set default control template/style for TextBox, Button, and other interactable
        /// elements to include this and then readonly can be specified from a high level!
        /// </summary>
        public static DependencyProperty IsReadOnlyProperty = APUtils.Register(GetIsReadOnly, SetIsReadOnly, new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.Inherits, OnIsReadOnlyChanged));
        public static bool GetIsReadOnly (DependencyObject obj) => (bool)obj.GetValue(IsReadOnlyProperty);
        public static void SetIsReadOnly (DependencyObject obj, bool value) => obj.SetValue(IsReadOnlyProperty, value);
        private static void OnIsReadOnlyChanged (DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            // This runs for every element an inherited IsReadOnly change reaches, so each one keeps its own IsEditable, and
            //  IsEditable doesn't inherit. It used to inherit as well, with this same callback on it. Then setting it here
            //  started a second inherited change in the middle of IsReadOnly's walk down the tree, and that walk didn't
            //  reach the far end of a deep tree (a PropertyGrid's rows) until IsReadOnly changed a second time. A second
            //  inherited change started from here doesn't reach the whole tree either, so nothing here may start one.
            SetIsEditable(d, !(bool)e.NewValue);
        }

        private static DependencyPropertyKey IsEditablePropertyKey = APUtils.RegisterReadOnly(GetIsEditable, SetIsEditable, new FrameworkPropertyMetadata(true));

        /// <summary>
        /// The inverse of <see cref="IsReadOnlyProperty"/>, this is a calculated attached property (which is why the property itself is readonly), it is only meant to simplify binding where IsEditable is prefered over IsReadOnly
        /// </summary>
        public static DependencyProperty IsEditableProperty = IsEditablePropertyKey.DependencyProperty;
        public static bool GetIsEditable (DependencyObject obj) => (bool)obj.GetValue(IsEditableProperty);
        private static void SetIsEditable (DependencyObject obj, bool value) => obj.SetValue(IsEditablePropertyKey, value);
    }
}
