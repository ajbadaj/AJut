﻿namespace AJut.UX.Controls
{
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Input;
    using System.Windows.Media;
    using System.Windows.Threading;
    using AJut.UX.Docking;
    using DPUtils = AJut.UX.DPUtils<DockZoneSelectedHeaderControl>;

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

                // Wait a tick to ensure that we have had enough time for all new UI to populate
                this.Dispatcher.InvokeAsync(_DoDragMoveSafe);
                async void _DoDragMoveSafe ()
                {
                    if (MouseXT.GetPrimaryButtonState() == MouseButtonState.Pressed) 
                    {
                        await target.DockingOwner.RunDragSearch(result.Value, target.Location.UI).ConfigureAwait(false);
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


        public static readonly DependencyProperty HeaderHighlightedForegroundProperty = DPUtils.Register(_ => _.HeaderHighlightedForeground);
        public Brush HeaderHighlightedForeground
        {
            get => (Brush)this.GetValue(HeaderHighlightedForegroundProperty);
            set => this.SetValue(HeaderHighlightedForegroundProperty, value);
        }

        public static readonly DependencyProperty HeaderFocusedForegroundProperty = DPUtils.Register(_ => _.HeaderFocusedForeground);
        public Brush HeaderFocusedForeground
        {
            get => (Brush)this.GetValue(HeaderFocusedForegroundProperty);
            set => this.SetValue(HeaderFocusedForegroundProperty, value);
        }

        public static readonly DependencyProperty TargetProperty = DPUtils.Register(_ => _.Target);
        public DockingContentAdapterModel Target
        {
            get => (DockingContentAdapterModel)this.GetValue(TargetProperty);
            set => this.SetValue(TargetProperty, value);
        }
    }
}
