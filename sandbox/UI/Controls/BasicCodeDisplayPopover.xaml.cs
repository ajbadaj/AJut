namespace TheAJutShowRoom.UI.Controls
{
    using System;
    using System.Windows;
    using System.Windows.Controls;
    using AJut;
    using AJut.Storage;
    using AJut.UX;

    public partial class BasicCodeDisplayPopover : UserControl, IStackNavPopoverDisplay
    {
        public BasicCodeDisplayPopover(string codeText)
        {
            this.CodeText = codeText;
            this.InitializeComponent();
        }

        public string CodeText { get; }

        public event EventHandler<EventArgs<Result>>? ResultSet;

        public void Cancel (string? cancelReason = null)
        {
            this.SetResult(Result.Error(cancelReason));
        }

        private void SetResult (Result result)
        {
            this.ResultSet?.Invoke(this, new EventArgs<Result>(result));
        }

        private void Close_OnClick (object sender, RoutedEventArgs e)
        {
            this.SetResult(Result.Success());
        }
    }
}
