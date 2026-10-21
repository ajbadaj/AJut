namespace AJutShowRoomWinUI.UI
{
    using System;
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;
    using Windows.ApplicationModel.DataTransfer;
    using DPUtils = AJut.UX.DPUtils<ShowroomCodeDisplay>;

    /// <summary>
    /// A read-only code box with optional line numbers and a copy button, for showing the markup or code behind a
    /// demo right beside it. Set <see cref="CodeText"/> and nothing else.
    /// </summary>
    public sealed partial class ShowroomCodeDisplay : UserControl
    {
        public ShowroomCodeDisplay ()
        {
            this.InitializeComponent();
        }

        public static readonly DependencyProperty CodeTextProperty = DPUtils.Register(_ => _.CodeText, string.Empty, (d, e) => d.ResetDisplayText());
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

        public static readonly DependencyProperty FinalTransformedTextProperty = DPUtils.Register(_ => _.FinalTransformedText, string.Empty);
        public string FinalTransformedText
        {
            get => (string)this.GetValue(FinalTransformedTextProperty);
            private set => this.SetValue(FinalTransformedTextProperty, value);
        }

        private void ResetDisplayText ()
        {
            string code = this.CodeText ?? string.Empty;
            if (!this.ShowLineNumbers)
            {
                this.FinalTransformedText = code;
                return;
            }

            string[] lines = code.Replace("\r\n", "\n").Split('\n');
            for (int index = 0; index < lines.Length; ++index)
            {
                lines[index] = $"{index + 1:00}: {lines[index]}";
            }

            this.FinalTransformedText = String.Join('\n', lines);
        }

        private void CopyCode_OnClick (object sender, RoutedEventArgs e)
        {
            DataPackage package = new DataPackage();
            package.SetText(this.CodeText ?? string.Empty);
            Clipboard.SetContent(package);
        }
    }
}
