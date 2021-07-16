namespace AJut.Application.Controls
{
    using System;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Input;
    using AJut.Application.Docking;
    using DPUtils = AJut.Application.DPUtils<DockZoneSelectedHeaderControl>;

    public class DockZoneSelectedHeaderControl : Control
    {
        static DockZoneSelectedHeaderControl ()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(DockZoneSelectedHeaderControl), new FrameworkPropertyMetadata(typeof(DockZoneSelectedHeaderControl)));
        }

        public DockZoneSelectedHeaderControl ()
        {
            this.CommandBindings.Add(new CommandBinding(DragDropElement.DragInitiatedCommand, OnTearoffRequested));
        }

        private void OnTearoffRequested (object sender, ExecutedRoutedEventArgs e)
        {
            var initial = (Point)e.Parameter;
            var castedSource = (UIElement)e.OriginalSource;

            var window = Window.GetWindow(castedSource);
            Point desktopMouseLocation = (Point)((Vector)window.PointToScreen(castedSource.TranslatePoint(initial, window)) - (Vector)initial);

            var result = this.Target.DockingOwner.DoGroupTearOff(this.Target.Location, desktopMouseLocation);
            if (result)
            {
                window = result.Value;
                this.Dispatcher.InvokeAsync(_DoDragMoveSafe);

                void _DoDragMoveSafe ()
                {
                    if (MouseXT.GetPrimaryButtonState() == MouseButtonState.Pressed) 
                    {
                        window.DragMove();
                    }
                }
            }
        }

        public static readonly DependencyProperty TargetProperty = DPUtils.Register(_ => _.Target);
        public DockingContentAdapterModel Target
        {
            get => (DockingContentAdapterModel)this.GetValue(TargetProperty);
            set => this.SetValue(TargetProperty, value);
        }
    }
}
