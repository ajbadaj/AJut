namespace AJut.UX.Controls
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
            this.CommandBindings.Add(new CommandBinding(DragDropElement.DragInitiatedCommand, OnTearoffRequested, CanTearoff));
            this.PreviewMouseRightButtonUp += this.OnPreviewMouseRightButtonUp;
        }

        private void OnPreviewMouseRightButtonUp (object sender, MouseButtonEventArgs e)
        {
            if (this.Target == null)
            {
                return;
            }

            DependencyObject current = this;
            while (current != null)
            {
                if (current is DockZone parentZone)
                {
                    var menu = parentZone.BuildHeaderContextMenu(this.Target);
                    if (menu != null)
                    {
                        menu.IsOpen = true;
                        e.Handled = true;
                    }

                    return;
                }

                current = VisualTreeHelper.GetParent(current);
            }
        }

        // A header drag tears its zone off when every panel in it can tear off. WPF departs from the shared rule in one
        //  case: dragging the header of a tearoff's root zone drags the whole tearoff (the shared rule leaves that to the
        //  tearoff's title bar), and moving a tearoff that already exists tears nothing off, so what its panels allow
        //  doesn't matter there.
        private void CanTearoff (object sender, CanExecuteRoutedEventArgs e)
        {
            DockZoneViewModel zone = this.Target?.Location;
            if (zone == null)
            {
                return;
            }

            Window window = Window.GetWindow(this);
            bool isInTearoff = (window != null) && DockWindowConfig.GetIsDockingTearoffWindow(window);
            DockZoneViewModel tearoffRoot = isInTearoff ? DockWindowConfig.GetDockingTearoffWindowRootZone(window) : null;
            if (isInTearoff && (zone == tearoffRoot))
            {
                e.CanExecute = true;
                return;
            }

            e.CanExecute = DockDragRules.ChooseHeaderDragSubject(zone, isInTearoff, tearoffRoot) != eHeaderDragSubject.None;
        }

        private void OnTearoffRequested (object sender, ExecutedRoutedEventArgs e)
        {
            var target = this.Target;
            var dragTracking = (ActiveDragTracking)e.Parameter;
            var castedSource = (UIElement)e.OriginalSource;

            DockZoneSize zoneSize = target.Location.UI?.RenderSize ?? DockZoneSize.Empty;
            DockTearoffPlacement placement = DockingManager.PlaceTearoffFor(zoneSize, castedSource, dragTracking.StartPoint);

            var dockingManager = (DockingManager)target.DockingOwner;
            Window hostWindow = Window.GetWindow(castedSource);
            bool isExistingTearoffRoot = (hostWindow != null)
                && DockWindowConfig.GetIsDockingTearoffWindow(hostWindow)
                && (DockWindowConfig.GetDockingTearoffWindowRootZone(hostWindow) == target.Location);

            // Captured before the tearoff changes where the zone is, so Escape can put it back. Moving a tearoff that already
            //  exists tears nothing off, so Escape only stops that drag.
            DockDragOrigin origin = isExistingTearoffRoot ? null : DockDragOrigin.ForZone(target.Location);
            var result = dockingManager.DoGroupTearoff(target.Location, new Point(placement.Origin.X, placement.Origin.Y), new Size(placement.Width, placement.Height));
            if (result)
            {
                // A tearoff's root zone moves the tearoff it is already in, which keeps its place and size, so the pointer
                //  stays where the header was pressed in it rather than where a new tearoff would put it
                DockPoint cursorOffset = placement.ScreenCursorOffset;
                if (isExistingTearoffRoot)
                {
                    Point pressInWindow = castedSource.TranslatePoint(dragTracking.StartPoint, hostWindow);
                    cursorOffset = new DockPoint(pressInWindow.X, pressInWindow.Y);
                }

                // Wait a tick to ensure that we have had enough time for all new UI to populate
                this.Dispatcher.InvokeAsync(_DoDragMoveSafe);
                async void _DoDragMoveSafe ()
                {
                    if (MouseXT.GetPrimaryButtonState() == MouseButtonState.Pressed)
                    {
                        await dockingManager.RunDragSearch(result.Value, (DockZone)target.Location.UI, cursorOffset, origin).ConfigureAwait(false);
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
