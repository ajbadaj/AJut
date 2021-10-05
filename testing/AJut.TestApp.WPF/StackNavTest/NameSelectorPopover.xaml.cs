namespace AJut.TestApp.WPF.StackNavTest
{
    using System;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Input;
    using AJut;
    using AJut.Application;
    using AJut.Storage;
    using DPUtils = AJut.Application.DPUtils<NameSelectorPopover>;

    public partial class NameSelectorPopover : UserControl, IStackNavPopoverDisplay<string>
    {
        public NameSelectorPopover(string initialValue)
        {
            this.NameText = initialValue;
            this.Loaded += _OnLoaded;

            this.InitializeComponent();

            void _OnLoaded (object sender, RoutedEventArgs e)
            {
                this.NameTB.SelectAll();
                this.NameTB.Focus();
            }
        }

        protected override void OnKeyUp (KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                this.Raise(this.NameText);
            }

            base.OnKeyUp(e);
        }

        public event EventHandler<EventArgs<Result<string>>> ResultSet;

        public static readonly DependencyProperty NameTextProperty = DPUtils.Register(_ => _.NameText);
        public string NameText
        {
            get => (string)this.GetValue(NameTextProperty);
            set => this.SetValue(NameTextProperty, value);
        }

        public void Cancel (string cancelReason = null)
        {
            this.Raise(Result<string>.Error(cancelReason));
        }

        private void Raise (Result<string> result)
        {
            this.ResultSet?.Invoke(this, result);
        }

        private void Ok_OnClick (object sender, RoutedEventArgs e)
        {
            this.Raise(this.NameText);
        }

        private void Cancel_OnClick (object sender, RoutedEventArgs e)
        {
            this.Cancel();
        }
    }
}
