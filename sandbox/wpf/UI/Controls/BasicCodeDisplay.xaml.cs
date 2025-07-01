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

        public static readonly DependencyProperty CodeTextProperty = DPUtils.Register(_ => _.CodeText, (d,e)=>d.ResetDisplayText());
        public string CodeText
        {
            get => (string)this.GetValue(CodeTextProperty);
            set => this.SetValue(CodeTextProperty, value);
        }


        public static readonly DependencyProperty ShowLineNumbersProperty = DPUtils.Register(_ => _.ShowLineNumbers, true, (d, e) => d.ResetDisplayText());
        public bool ShowLineNumbers
        {
            get => (bool)this.GetValue(ShowLineNumbersProperty);
            set => this.SetValue(ShowLineNumbersProperty, value);
        }


        private static readonly DependencyPropertyKey FinalTransformedTextPropertyKey = DPUtils.RegisterReadOnly(_ => _.FinalTransformedText);
        public static readonly DependencyProperty FinalTransformedTextProperty = FinalTransformedTextPropertyKey.DependencyProperty;
        public string FinalTransformedText
        {
            get => (string)this.GetValue(FinalTransformedTextProperty);
            protected set => this.SetValue(FinalTransformedTextPropertyKey, value);
        }


        private void ResetDisplayText ()
        {
            if (this.ShowLineNumbers)
            {
                string[] allCodeText = this.CodeText.Replace("\r\n", "\n").Split("\n");
                for (int index = 0; index < allCodeText.Length; ++index)
                {
                    allCodeText[index] = $"{index:00}: {allCodeText[index]}";
                }

                this.FinalTransformedText = String.Join('\n', allCodeText);
            }
            else
            {
                this.FinalTransformedText = this.CodeText;
            }
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
