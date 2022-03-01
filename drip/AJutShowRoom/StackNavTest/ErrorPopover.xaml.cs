namespace AJutShowRoom.StackNavTest
{
    using System;
    using System.Windows;
    using System.Windows.Controls;
    using AJut;
    using AJut.Storage;
    using AJut.UX;
    using DPUtils = AJut.UX.DPUtils<ErrorPopover>;

    public partial class ErrorPopover : UserControl, IStackNavPopoverDisplay
    {
        public ErrorPopover (string errorText)
        {
            this.InitializeComponent();
            this.ErrorText = errorText;
        }

        public event EventHandler<EventArgs<Result>> ResultSet;

        public static readonly DependencyProperty ErrorTextProperty = DPUtils.Register(_ => _.ErrorText);
        public string ErrorText
        {
            get => (string)this.GetValue(ErrorTextProperty);
            set => this.SetValue(ErrorTextProperty, value);
        }

        public void Cancel (string cancelReason = null)
        {
            this.ResultSet?.Invoke(this, Result.Error(cancelReason));
        }

        private void Ok_OnClick (object sender, RoutedEventArgs e)
        {
            this.ResultSet?.Invoke(this, Result.Success());
        }
    }
}
