namespace TheAJutShowRoom.UI.Controls
{
    using System.Windows;
    using System.Windows.Controls;
    using DPUtils = AJut.UX.DPUtils<EditableTextBlockControlExample>;

    public partial class EditableTextBlockControlExample : UserControl
    {
        public EditableTextBlockControlExample ()
        {
            this.InitializeComponent();
        }

        public static readonly DependencyProperty EditTextBlockTextProperty = DPUtils.Register(_ => _.EditTextBlockText);
        public string EditTextBlockText
        {
            get => (string)this.GetValue(EditTextBlockTextProperty);
            set => this.SetValue(EditTextBlockTextProperty, value);
        }
    }
}
