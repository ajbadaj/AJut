namespace AJutShowRoom
{
    using System.Windows;
    using DPUtils = AJut.UX.DPUtils<TestWindow>;

    public partial class TestWindow : Window
    {
        public TestWindow ()
        {
            this.WindowStyle = WindowStyle.ToolWindow;
            this.InitializeComponent();
        }

        public static readonly DependencyProperty TextProperty = DPUtils.Register(_ => _.Text);
        public string Text
        {
            get => (string)this.GetValue(TextProperty);
            set => this.SetValue(TextProperty, value);
        }
    }
}
