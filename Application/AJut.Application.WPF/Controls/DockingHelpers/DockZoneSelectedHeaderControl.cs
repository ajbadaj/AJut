namespace AJut.Application.Controls
{
    using System;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Input;
    using System.Windows.Media;
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
            var target = this.Target;
            var dragTracking = (ActiveDragTracking)e.Parameter;
            var castedSource = (UIElement)e.OriginalSource;

            var window = Window.GetWindow(castedSource);
            Point desktopMouseLocation = (Point)((Vector)window.PointToScreen(castedSource.TranslatePoint(dragTracking.StartPoint, window)) - (Vector)dragTracking.StartPoint);

            var result = target.DockingOwner.DoGroupTearoff(target.Location, desktopMouseLocation);
            if (result)
            {
                window = result.Value;
                this.Dispatcher.InvokeAsync(_DoDragMoveSafe);

                async void _DoDragMoveSafe ()
                {
                    if (MouseXT.GetPrimaryButtonState() == MouseButtonState.Pressed) 
                    {
                        await target.DockingOwner.RunDragSearch(result.Value, target.Location);
                    }
                }
            }
        }

        public static readonly DependencyProperty HeaderBackgroundProperty = DPUtils.Register(_ => _.HeaderBackground);
        public Brush HeaderBackground
        {
            get => (Brush)this.GetValue(HeaderBackgroundProperty);
            set => this.SetValue(HeaderBackgroundProperty, value);
        }

        public static readonly DependencyProperty HeaderHighlightBackgroundProperty = DPUtils.Register(_ => _.HeaderHighlightBackground);
        public Brush HeaderHighlightBackground
        {
            get => (Brush)this.GetValue(HeaderHighlightBackgroundProperty);
            set => this.SetValue(HeaderHighlightBackgroundProperty, value);
        }

        public static readonly DependencyProperty HeaderFocusedBackgroundProperty = DPUtils.Register(_ => _.HeaderFocusedBackground);
        public Brush HeaderFocusedBackground
        {
            get => (Brush)this.GetValue(HeaderFocusedBackgroundProperty);
            set => this.SetValue(HeaderFocusedBackgroundProperty, value);
        }

        public static readonly DependencyProperty TargetProperty = DPUtils.Register(_ => _.Target);
        public DockingContentAdapterModel Target
        {
            get => (DockingContentAdapterModel)this.GetValue(TargetProperty);
            set => this.SetValue(TargetProperty, value);
        }
    }
}
