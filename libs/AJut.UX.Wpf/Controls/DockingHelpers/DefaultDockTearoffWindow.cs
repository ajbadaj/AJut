namespace AJut.UX.Controls
{
    using System.Windows;
    using AJut.UX.Docking;
    using DPUtils = AJut.UX.DPUtils<DefaultDockTearoffWindow>;

    public class DefaultDockTearoffWindow : Window
    {
        static DefaultDockTearoffWindow ()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(DefaultDockTearoffWindow), new FrameworkPropertyMetadata(typeof(DefaultDockTearoffWindow)));
        }

        public DefaultDockTearoffWindow (DockingManager manager)
        {
            this.Manager = manager;
        }

        public DockingManager Manager { get; }

        public static readonly DependencyProperty TitleBarFontSizeProperty = DPUtils.Register(_ => _.TitleBarFontSize);
        public double TitleBarFontSize
        {
            get => (double)this.GetValue(TitleBarFontSizeProperty);
            set => this.SetValue(TitleBarFontSizeProperty, value);
        }
    }
}
