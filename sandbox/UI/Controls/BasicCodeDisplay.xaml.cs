namespace TheAJutShowRoom.UI.Controls
{
    using System;
    using System.Windows;
    using System.Windows.Controls;
    using AJut;
    using DPUtils = AJut.UX.DPUtils<BasicCodeDisplay>;

    public partial class BasicCodeDisplay : UserControl
    {
        public BasicCodeDisplay()
        {
            this.InitializeComponent();
        }

        public static readonly DependencyProperty CodeTextProperty = DPUtils.Register(_ => _.CodeText);
        public string CodeText
        {
            get => (string)this.GetValue(CodeTextProperty);
            set => this.SetValue(CodeTextProperty, value);
        }

        private void CopyCode_OnClick (object sender, RoutedEventArgs e)
        {
            for (int retry = 5; retry > 0; --retry)
            {
                try
                {
                    Clipboard.SetText(this.CodeText);
                    return;
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex);
                }
            }
        }
    }
}
