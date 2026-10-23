namespace AJut.UX.Controls
{
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Media;
    using DPUtils = AJut.UX.DPUtils<DockPanelAddButton>;

    public class DockPanelAddButton : Button
    {
        static DockPanelAddButton ()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(DockPanelAddButton), new FrameworkPropertyMetadata(typeof(DockPanelAddButton)));
        }

        public static readonly DependencyProperty PlusOverlayForegroundProperty = DPUtils.Register(_ => _.PlusOverlayForeground);
        /// <summary>
        /// The "+" badge in the button's bottom right corner
        /// </summary>
        public Brush PlusOverlayForeground
        {
            get => (Brush)this.GetValue(PlusOverlayForegroundProperty);
            set => this.SetValue(PlusOverlayForegroundProperty, value);
        }
    }
}
