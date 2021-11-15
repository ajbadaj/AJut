namespace AJut.UX.Controls
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Input;
    using System.Windows.Media;
    using AJut.UX.AttachedProperties;
    using AJut.UX.Docking;
    using DPUtils = AJut.UX.DPUtils<DockWholeWindowDragStartControl>;

    public class DockWholeWindowDragStartControl : Control
    {
        static DockWholeWindowDragStartControl ()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(DockWholeWindowDragStartControl), new FrameworkPropertyMetadata(typeof(DockWholeWindowDragStartControl)));
        }

        public DockWholeWindowDragStartControl ()
        {
            DragWatch.SetIsEnabled(this, true);
            this.CommandBindings.Add(new CommandBinding(DragDropElement.DragInitiatedCommand, OnDragInitiated));
        }

        public static readonly DependencyProperty DragRootZoneProperty = DPUtils.Register(_ => _.DragRootZone);
        public DockZone DragRootZone
        {
            get => (DockZone)this.GetValue(DragRootZoneProperty);
            set => this.SetValue(DragRootZoneProperty, value);
        }

        public static readonly DependencyProperty BackgroundBrushHighlightedProperty = DPUtils.Register(_ => _.BackgroundBrushHighlighted);
        public Brush BackgroundBrushHighlighted
        {
            get => (Brush)this.GetValue(BackgroundBrushHighlightedProperty);
            set => this.SetValue(BackgroundBrushHighlightedProperty, value);
        }

        public static readonly DependencyProperty BorderBrushHighlightedProperty = DPUtils.Register(_ => _.BorderBrushHighlighted);
        public Brush BorderBrushHighlighted
        {
            get => (Brush)this.GetValue(BorderBrushHighlightedProperty);
            set => this.SetValue(BorderBrushHighlightedProperty, value);
        }

        public static readonly DependencyProperty GlyphBrushProperty = DPUtils.Register(_ => _.GlyphBrush);
        public Brush GlyphBrush
        {
            get => (Brush)this.GetValue(GlyphBrushProperty);
            set => this.SetValue(GlyphBrushProperty, value);
        }

        public static readonly DependencyProperty GlyphBrushHighlightedProperty = DPUtils.Register(_ => _.GlyphBrushHighlighted);
        public Brush GlyphBrushHighlighted
        {
            get => (Brush)this.GetValue(GlyphBrushHighlightedProperty);
            set => this.SetValue(GlyphBrushHighlightedProperty, value);
        }

        private async void OnDragInitiated (object sender, ExecutedRoutedEventArgs e)
        {
            try
            {
                var window = Window.GetWindow(this);
                await this.DragRootZone.Manager.RunDragSearch(window, this.DragRootZone).ConfigureAwait(false);
            }
            catch (Exception exc)
            {
                Logger.LogError($"Error trying to drag from {nameof(DockWholeWindowDragStartControl)}", exc);
            }
        }
    }
}
