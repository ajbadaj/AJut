﻿namespace TheAJutShowRoom.UI.Controls
{
    using System.Windows;
    using System.Windows.Controls;

    using DPUtils = AJut.UX.DPUtils<NumericEditorControlExample>;


    public partial class NumericEditorControlExample : UserControl
    {
        public NumericEditorControlExample ()
        {
            this.InitializeComponent();
        }

        public static readonly DependencyProperty FloatValueProperty = DPUtils.Register(_ => _.FloatValue);
        public float FloatValue
        {
            get => (float)this.GetValue(FloatValueProperty);
            set => this.SetValue(FloatValueProperty, value);
        }

        private void Normal_OnUserEditComplete (object sender, AJut.UX.UserEditAppliedEventArgs e)
        {
            MessageBox.Show($"Value changed from: {e.OldValue} → {e.NewValue}");
        }
    }
}
