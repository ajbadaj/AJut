namespace AJut.Application.Controls
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Collections.Specialized;
    using System.Diagnostics;
    using System.Globalization;
    using System.Linq;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Documents;
    using System.Windows.Markup;
    using System.Windows.Media;
    using System.Windows.Shapes;
    using DPUtils = AJut.Application.DPUtils<AJut.Application.Controls.PathFollowingContentDisplayer>;
    using FontFamily = System.Windows.Media.FontFamily;
    //using FontStyle = System.Windows.FontStyle;
    //using FontWeight = System.Windows.FontWeight;
    //using Point = System.Windows.Point;
    //using Size = System.Windows.Size;

    // https://docs.microsoft.com/en-us/archive/msdn-magazine/2008/december/foundations-render-text-on-a-path-with-wpf


    [ContentProperty(nameof(Children))]
    [TemplatePart(Name = nameof(PathFollowingContentDisplayer.PART_DisplayArea), Type = typeof(Canvas))]
    public class PathFollowingContentDisplayer : Control
    {
        private const double k180OverPi = 180.0 / Math.PI;

        private Canvas PART_DisplayArea;
        private readonly List<IDisplayItem> m_displayItems = new List<IDisplayItem>();
        private Path m_renderPath;

        static PathFollowingContentDisplayer ()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(PathFollowingContentDisplayer), new FrameworkPropertyMetadata(typeof(PathFollowingContentDisplayer)));
        }

        public PathFollowingContentDisplayer ()
        {
            this.Children = new ObservableCollection<object>();
        }
        public override void OnApplyTemplate ()
        {
            base.OnApplyTemplate();

            this.PART_DisplayArea = (Canvas)this.GetTemplateChild(nameof(PART_DisplayArea));
            if (this.PART_DisplayArea == null)
            {
                return;
            }

            // What about if visibility starts false... ?
            foreach (IDisplayItem item in m_displayItems.Where(i => i.IsDisplayed && i.RenderTarget.GetVisualParent() == null))
            {
                this.PART_DisplayArea.Children.Add(item.RenderTarget);
            }
        }

        public static readonly DependencyProperty ChildrenProperty = DPUtils.Register(_ => _.Children, (d,e)=>d.OnChildrenChanged(e.OldValue, e.NewValue));
        public ObservableCollection<object> Children
        {
            get => (ObservableCollection<object>)this.GetValue(ChildrenProperty);
            set => this.SetValue(ChildrenProperty, value);
        }

        public static readonly DependencyProperty ChildProperty = DPUtils.Register(_ => _.Child, (d,e)=>d.OnChildChanged());
        public object Child
        {
            get => (object)this.GetValue(ChildProperty);
            set => this.SetValue(ChildProperty, value);
        }
        private void OnChildChanged ()
        {
            this.ClearAllChildren();
            if (this.Child != null)
            {
                this.TrackChild(-1, this.Child);
            }
        }

        public static readonly DependencyProperty PathProperty = DPUtils.Register(_ => _.Path, (d,e)=>d.OnPathChanged(e.OldValue, e.NewValue));
        public PathGeometry Path
        {
            get => (PathGeometry)this.GetValue(PathProperty);
            set => this.SetValue(PathProperty, value);
        }

        private static readonly DependencyPropertyKey FlattenedPathLengthPropertyKey = DPUtils.RegisterReadOnly(_ => _.FlattenedPathLength);
        public static readonly DependencyProperty FlattenedPathLengthProperty = FlattenedPathLengthPropertyKey.DependencyProperty;
        public double FlattenedPathLength
        {
            get => (double)this.GetValue(FlattenedPathLengthProperty);
            protected set => this.SetValue(FlattenedPathLengthPropertyKey, value);
        }

        public static readonly DependencyProperty VariableItemWidthProportionalMinimumProperty = DPUtils.Register(_ => _.VariableItemWidthProportionalMinimum, 2.0, (d, e) => d.TriggerRedraw());
        public double VariableItemWidthProportionalMinimum
        {
            get => (double)this.GetValue(VariableItemWidthProportionalMinimumProperty);
            set => this.SetValue(VariableItemWidthProportionalMinimumProperty, value);
        }

        public static readonly DependencyProperty AdditionalKerningProperty = DPUtils.Register(_ => _.AdditionalKerning, (d,e)=>d.TriggerRedraw());
        public double AdditionalKerning
        {
            get => (double)this.GetValue(AdditionalKerningProperty);
            set => this.SetValue(AdditionalKerningProperty, value);
        }

        public static readonly DependencyProperty ShouldRenderPathProperty = DPUtils.Register(_ => _.ShouldRenderPath, (d,e)=>d.OnShouldRenderPathChanged());
        public bool ShouldRenderPath
        {
            get => (bool)this.GetValue(ShouldRenderPathProperty);
            set => this.SetValue(ShouldRenderPathProperty, value);
        }
        private void OnShouldRenderPathChanged ()
        {
            if (m_renderPath == null)
            {
                return;
            }

            if (this.ShouldRenderPath)
            {
                this.PART_DisplayArea.Children.Add(m_renderPath);
            }
            else
            {
                this.PART_DisplayArea.Children.Remove(m_renderPath);
            }
        }

        public static readonly DependencyProperty PathRenderThicknessProperty = DPUtils.Register(_ => _.PathRenderThickness, 3.0, (d, e) => d.InvalidateVisual());
        public double PathRenderThickness
        {
            get => (double)this.GetValue(PathRenderThicknessProperty);
            set => this.SetValue(PathRenderThicknessProperty, value);
        }

        public List<IDisplayItem> DEBUG_DisplayItems => m_displayItems;

        // ===================[ Property changed ]======================
        private void OnPathChanged (PathGeometry oldPath, PathGeometry newPath)
        {
            if (oldPath != null)
            {
                oldPath.Figures.Changed -= _OnPathFiguresChanged;
                if (m_renderPath != null)
                {
                    this.PART_DisplayArea.Children.Remove(m_renderPath);
                }
            }

            if (newPath != null)
            {
                //newPath.Figures.Changed += _OnPathFiguresChanged;
                /*
                m_renderPath = new Path { Data = newPath };
                m_renderPath.SetBinding(Shape.StrokeThicknessProperty, this.CreateBinding(PathRenderThicknessProperty));
                m_renderPath.SetBinding(Shape.StrokeProperty, this.CreateBinding(ForegroundProperty));
                if (this.ShouldRenderPath && this.PART_DisplayArea != null)
                {
                    this.PART_DisplayArea.Children.Add(m_renderPath);
                }
                */
            }

            this.CalculateAndCacheFlattenedPathLength();
            void _OnPathFiguresChanged (object sender, EventArgs e) => this.CalculateAndCacheFlattenedPathLength();
        }

        private void OnChildrenChanged (ObservableCollection<object> oldChildren, ObservableCollection<object> newChildren)
        {
            if (oldChildren != null)
            {
                oldChildren.CollectionChanged -= _Children_OnCollectionChanged;
            }

            if (newChildren != null)
            {
                newChildren.CollectionChanged += _Children_OnCollectionChanged;
            }

            _ResetItems();

            void _ResetItems ()
            {
                foreach (object child in this.Children)
                {
                    this.TrackChild(-1, child);
                }
            }
            void _Children_OnCollectionChanged (object _sender, NotifyCollectionChangedEventArgs _e)
            {
                _ResetItems();
            }
        }

        private void ClearAllChildren()
        {
            foreach (IDisplayItem item in m_displayItems.ToList())
            {
                this.StopTracking(item);
            }
        }

        private void TrackChild(int index, object child)
        {
            if (child is TextBlock tb)
            {
                var textParent = new TextParentItem(this, tb);
                this.Track(index, textParent);
                textParent.ResetChildren();

            }
            else if (child is string text)
            {
                var textParent = new TextParentItem(this, text);
                this.Track(index, textParent);
                textParent.ResetChildren();
            }
            else if (child is FrameworkElement feChild)
            {
                this.Track(index, new GeneralDisplayItem(feChild));
            }
        }

        // ===================[    Utilities     ]======================
        private void Track (int index, IDisplayItem item)
        {
            item.NeedsRender += this.DisplayItem_HandleNeedsRender;
            m_displayItems.Insert(index != -1 ? index : m_displayItems.Count, item);
            if (this.PART_DisplayArea == null)
            {
                return;
            }

            if (item.IsDisplayed)
            {
                this.PART_DisplayArea.Children.Add(item.RenderTarget);
            }
            else if (item.RenderTarget != null)
            {
                this.AddLogicalChild(item.RenderTarget);
                this.AddVisualChild(item.RenderTarget);
            }
        }

        private void StopTracking (IDisplayItem item)
        {
            item.NeedsRender -= this.DisplayItem_HandleNeedsRender;
            m_displayItems.Remove(item);

            if (this.PART_DisplayArea != null)
            {
                this.PART_DisplayArea.Children.Remove(item.RenderTarget);
            }

            this.RemoveLogicalChild(item.RenderTarget);
            this.RemoveVisualChild(item.RenderTarget);
        }

        private void TriggerRedraw ()
        {
            this.InvalidateMeasure();
            this.InvalidateVisual();
        }

        private void CalculateAndCacheFlattenedPathLength ()
        {
            if (this.Path.Figures.Count == 0)
            {
                this.FlattenedPathLength = 0.0;
                return;
            }

            this.FlattenedPathLength = this.Path.CalculateFlattenedLength();
            this.TriggerRedraw();
        }

        // ===================[  Event Handlers  ]======================
        private void DisplayItem_HandleNeedsRender (object sender, EventArgs e)
        {
            this.TriggerRedraw();
        }

        private bool OnParentItemUpdated(IDisplayItem parentItem, IEnumerable<IDisplayItem> oldItems, IEnumerable<IDisplayItem> newItems)
        {
            foreach(var old in oldItems)
            {
                this.StopTracking(old);
            }

            int index = m_displayItems.IndexOf(parentItem);
            if (index == -1)
            {
                return false;
            }

            ++index;
            int itemOffset = 0;
            foreach(IDisplayItem item in newItems)
            {
                this.Track(index + itemOffset++, item);
            }

            this.InvalidateMeasure();
            this.InvalidateVisual();
            return true;
        }

        // ===================[ Control Overrides ]=====================

        protected override Size MeasureOverride (Size constraint)
        {
            constraint = base.MeasureOverride(constraint);
            double pathLength = this.FlattenedPathLength;
            double fixedWidth = 0.0;
            int zeroWidthItems = 0;

            List<IDisplayItem> allRenderItems = m_displayItems.Where(i => i.IsDisplayed).ToList();
            foreach (IDisplayItem item in allRenderItems)
            {
                if (item.HorizontalExtent == 0.0)
                {
                    ++zeroWidthItems;
                }
                else
                {
                    fixedWidth += item.HorizontalExtent;
                }
            }

            double diff = pathLength - fixedWidth;
            double variableItemWidths = zeroWidthItems == 0 ? 0 : (diff / zeroWidthItems);

            if (variableItemWidths < this.VariableItemWidthProportionalMinimum)
            {
                variableItemWidths = this.VariableItemWidthProportionalMinimum;
            }

            // ======== Determine the operating scale ===========
            double estimatedTotalWidth = ((m_displayItems.Count - 1) * this.AdditionalKerning) + fixedWidth + (variableItemWidths * this.VariableItemWidthProportionalMinimum);
            double widthDiff = this.Path.Bounds.Width - constraint.Width;
            double heightDiff = this.Path.Bounds.Height - constraint.Height;

            if (widthDiff > heightDiff)
            {
                m_operatingScale = constraint.Width / this.Path.Bounds.Width;
            }
            else
            {
                m_operatingScale = constraint.Height / this.Path.Bounds.Height;
            }

            Rect bounds = new Rect(0.0, 0.0, this.Path.Bounds.Width * m_operatingScale, this.Path.Bounds.Height * m_operatingScale);
            double kerning = this.AdditionalKerning * m_operatingScale;
            double progress = 0.0;// scale / 2;
            if (estimatedTotalWidth < pathLength)
            {
                progress = ((pathLength - estimatedTotalWidth) / 2) / pathLength;
            }

            double offsetX = 0.0;
            if (this.Path.Bounds.Left < 0.0)
            {
                offsetX = -this.Path.Bounds.Left;
            }

            double offsetY = 0.0;
            if (this.Path.Bounds.Top < 0.0)
            {
                offsetY = -this.Path.Bounds.Top;
            }
            foreach (IDisplayItem item in m_displayItems)
            {
                double itemWidth = (item.HorizontalExtent == 0.0 ? variableItemWidths : item.HorizontalExtent) * m_operatingScale;
                double baseline = item.VerticalExtent;
                bool isLast = m_displayItems.Last() == item;

                double itemHalfWidth = itemWidth * 0.5;

                // To center the items, move the progress by half width first and after
                progress += (m_operatingScale * itemHalfWidth) / pathLength;
                this.Path.GetPointAtFractionLength(1.0 - progress, out Point spotOnPath, out Point pathTangent);
                spotOnPath.X += offsetX;
                spotOnPath.Y += offsetY;
                spotOnPath.X -= itemHalfWidth;
                spotOnPath.Y -= item.VerticalExtent;
                item.Place(spotOnPath, Math.Atan2(pathTangent.Y, pathTangent.X) * 180 / Math.PI);
                //item.Transform(new ScaleTransform(-m_operatingScale, -m_operatingScale),
                //                new RotateTransform(Math.Atan2(pathTangent.Y, pathTangent.X) * k180OverPi, itemHalfWidth, item.Baseline),
                //                new TranslateTransform(spotOnPath.X - itemHalfWidth - (isLast ? 0 : kerning), spotOnPath.Y - (item.Baseline))
                //);

                bounds.Union(new Rect(spotOnPath, new Size(item.HorizontalExtent, item.VerticalExtent)));
                progress += itemHalfWidth / pathLength;
            }

            //if (this.IsPathReltiveToBounds)
            //{
            //    return arrangeBounds;
            //}

            return constraint;// bounds.Size;
        }

        private double m_operatingScale;

#if false
        protected override Size ArrangeOverride (Size arrangeBounds)
        {
            // https://docs.microsoft.com/en-us/archive/msdn-magazine/2008/december/foundations-render-text-on-a-path-with-wpf
            double pathLength = this.FlattenedPathLength;
            double fixedWidth = 0.0;
            int zeroWidthItems = 0;

            List<IDisplayItem> allRenderItems = m_displayItems.Where(i => i.IsDisplayed).ToList();
            foreach (IDisplayItem item in allRenderItems)
            {
                if (item.Width == 0.0)
                {
                    ++zeroWidthItems;
                }
                else
                {
                    fixedWidth += item.Width;
                }
            }

            double diff = pathLength - fixedWidth;
            double variableItemWidths = zeroWidthItems == 0 ? 0 : (diff / zeroWidthItems);

            if (variableItemWidths < this.VariableItemWidthProportionalMinimum)
            {
                variableItemWidths = this.VariableItemWidthProportionalMinimum;
            }

            double estimatedTotalWidth = ((m_displayItems.Count - 1) * this.AdditionalKerning) + fixedWidth + (variableItemWidths * this.VariableItemWidthProportionalMinimum);
            //double scale = estimatedTotalWidth / pathLength;
            m_operatingScale = 1.0;
            if (this.IsPathReltiveToBounds)
            {
                double widthDiff = arrangeBounds.Width - this.Path.Bounds.Width;
                double heightDiff = arrangeBounds.Height - this.Path.Bounds.Height;

                if (widthDiff < heightDiff)
                {
                    m_operatingScale = arrangeBounds.Width / this.Path.Bounds.Width;
                }
                else
                {
                    m_operatingScale = arrangeBounds.Height / this.Path.Bounds.Height;
                }
            }
            else
            {
                m_operatingScale = estimatedTotalWidth / pathLength;
            }

            Rect bounds = new Rect(0.0, 0.0, this.Path.Bounds.Width, this.Path.Bounds.Height);
            double kerning = this.AdditionalKerning * m_operatingScale;
            double progress = 0.0;// scale / 2;
            if (estimatedTotalWidth < pathLength)
            {
                progress = ((pathLength * 0.5) - (estimatedTotalWidth * m_operatingScale * 0.5)) / pathLength;
            }

            double offsetX = 0.0;
            if (this.Path.Bounds.Left < 0.0)
            {
                offsetX = -this.Path.Bounds.Left;
            }

            double offsetY = 0.0;
            if (this.Path.Bounds.Top < 0.0)
            {
                offsetY = -this.Path.Bounds.Top;
            }
            foreach (IDisplayItem item in m_displayItems)
            {
                double itemWidth = (item.HorizontalExtent == 0.0 ? variableItemWidths : item.HorizontalExtent) * m_operatingScale;
                double baseline = item.VerticalExtent;
                bool isLast = m_displayItems.Last() == item;
                //if (item is FrameworkElementDisplayItem fe)
                //{
                //    fe.Element.Arrange(
                //        new Rect(
                //            new Size(itemWidth, fe.Element.DesiredSize.Height * scale)
                //        )
                //    );
                //}
                //else
                //{
                //    fe = null;
                //}

                double itemHalfWidth = itemWidth * 0.5;
                // To center the items, move the progress by half width first and after
                progress += (m_operatingScale * itemHalfWidth) / pathLength;
                this.Path.GetPointAtFractionLength(1.0 - progress, out Point point, out Point tangent);
                point.X += offsetX;
                point.Y += offsetY;
                point.X -= itemHalfWidth;
                point.Y -= item.VerticalExtent;
                item.Transform( new ScaleTransform(-m_operatingScale, -m_operatingScale),
                                new RotateTransform(Math.Atan2(tangent.Y, tangent.X) * k180OverPi, itemHalfWidth, item.Baseline),
                                new TranslateTransform(point.X - itemHalfWidth - (isLast ? 0 : kerning), point.Y - (item.Baseline))
                );

                if (item is GeneralDisplayItem fe)
                {
                    bounds.Union(new Rect(point, fe.Element.RenderSize));
                }
                progress += itemHalfWidth / pathLength;
            }

            if (this.IsPathReltiveToBounds)
            {
                return arrangeBounds;
            }

            return bounds.Size;
            //return base.ArrangeOverride(bounds.Size);
            //double scale = this.FlattenedPathLength / arrange
#if false
            List<DisplayItem> renderTargets = m_displayItems.Where(i => !(i is TextParentItem)).ToList();
            double totalItemsWidth = renderTargets.Sum(_ => _.Width);
            if (totalItemsWidth >= 0.0)
            {
                double progress = 0.0;
                foreach (DisplayItem item in renderTargets)
                {
                    double itemHalfWidth = item.Width * 0.5;
                    // To center the items, move the progress by half width first and after
                    progress += itemHalfWidth / totalItemsWidth;
                    this.Path.GetPointAtFractionLength(progress, out Point point, out Point tangent);

                    item.Transform(new TranslateTransform(point.X - itemHalfWidth, point.Y - item.Baseline)
                                    , new RotateTransform(Math.Atan2(tangent.Y, tangent.X) * k180OverPi, itemHalfWidth, item.Baseline));
                    progress += itemHalfWidth / totalItemsWidth;
                }
            }

            return base.ArrangeOverride(arrangeBounds);
#endif
        }

        // 2nd way
        protected override void OnRender (DrawingContext dc)
        {
            // Draw the text
            foreach (TextDisplayItem textItem in m_displayItems.OfType<TextDisplayItem>())
            {
                dc.PushTransform(textItem.RenderTransform);
                dc.DrawText(textItem.TextDisplay, new Point());
                dc.Pop();
            }

            if (this.ShouldRenderPath)
            {
                bool transformed = false;
                if (this.Path.Bounds.Left != 0.0)
                {
                    transformed = true;

                    if (this.Path.Bounds.Top != 0.0)
                    {
                        dc.PushTransform(new TranslateTransform(-this.Path.Bounds.Left, -this.Path.Bounds.Top));
                    }
                }
                else if (this.Path.Bounds.Top != 0.0)
                {
                    transformed = true;
                    dc.PushTransform(new TranslateTransform(0.0, -this.Path.Bounds.Top));
                }
                dc.PushTransform(new ScaleTransform(m_operatingScale, m_operatingScale));
                dc.DrawGeometry(null, new Pen(this.Foreground, this.PathRenderThickness), this.Path);
                dc.Pop();
                if (transformed)
                {
                    dc.Pop();
                }
            }
        }
#endif

        // ==================================================================================
        // =================== [ Display Item Utility Classes ] =============================
        // ==================================================================================

        public class SimpleTextDisplay : UIElement
        {
            public SimpleTextDisplay (FormattedText text)
            {
                this.Text = text;
            }
            public FormattedText Text { get; }
            protected override void OnRender (DrawingContext dc)
            {
                dc.PushTransform(this.RenderTransform);
                dc.DrawText(this.Text, new Point());
                dc.Pop();
            }
        }

        public interface IDisplayItem
        {
            event EventHandler<EventArgs> NeedsRender;
            bool IsDisplayed { get; }
            UIElement RenderTarget { get; }
            void Place (Point location, double angle);
            double HorizontalExtent { get; }
            double VerticalExtent { get; }
        }

        public abstract class DisplayItem<TDisplay> : IDisplayItem
            where TDisplay : UIElement
        {
            public event EventHandler<EventArgs> NeedsRender;
            private readonly PathFollowingContentDisplayer m_owner;

            public DisplayItem(TDisplay renderTarget)
            {
                this.RenderTarget = renderTarget;

                // TODO - watch visibility
            }

            public TDisplay RenderTarget { get; }
            UIElement IDisplayItem.RenderTarget => this.RenderTarget;
            bool IDisplayItem.IsDisplayed => this.RenderTarget.Visibility != Visibility.Collapsed;

            public virtual double HorizontalExtent { get; private set; }
            public virtual double VerticalExtent { get; private set; }

            public void Place (Point location, double angle)
            {
                Canvas.SetLeft(this.RenderTarget, location.X);
                Canvas.SetTop(this.RenderTarget, location.Y);
                this.RenderTarget.RenderTransform = new RotateTransform(angle, this.HorizontalExtent / 2, this.VerticalExtent / 2);
            }

            protected void TriggerNeedsRender()
            {
                this.NeedsRender?.Invoke(this, EventArgs.Empty);
            }
        }

        private class TextParentItem : IDisplayItem
        {
            private readonly PathFollowingContentDisplayer m_parent;
            private readonly DPWatcher m_watcher;
            private readonly List<TextDisplayItem> m_children = new List<TextDisplayItem>();
            private Typeface m_foundTypeface;
            private double m_foundFontSize;
            private string m_dataSource;
            private TextBlock m_renderTarget;

            public TextParentItem (PathFollowingContentDisplayer parent, TextBlock source)
            {
                m_renderTarget = source;
                m_parent = parent;
                m_dataSource = source.Text;
                m_watcher = new DPWatcher(source);

                TrackAndGetValue(source, TextBlock.FontFamilyProperty, TextElement.FontFamilyProperty, out FontFamily fontFamily);
                TrackAndGetValue(source, TextBlock.FontStretchProperty, TextElement.FontStretchProperty, out FontStretch fontStretch);
                TrackAndGetValue(source, TextBlock.FontStyleProperty, TextElement.FontStyleProperty, out FontStyle fontStyle);
                TrackAndGetValue(source, TextBlock.FontWeightProperty, TextElement.FontWeightProperty, out FontWeight fontWeight);
                TrackAndGetValue(source, TextBlock.FontSizeProperty, TextElement.FontSizeProperty, out m_foundFontSize);

                m_watcher.Watch(TextBlock.TextProperty);
                m_foundTypeface = new Typeface(fontFamily, fontStyle, fontWeight, fontStretch);

                m_watcher.WatchedValueChanged += _DoNeedsRender;
                void _DoNeedsRender (object _sender, EventArgs<object> _e)
                {
                    // The text itself updated
                    if (_e.Value is string text)
                    {
                        m_dataSource = text;
                        this.ResetChildren();
                    }

                    this.TriggerNeedsRender();
                }
            }

            public TextParentItem (PathFollowingContentDisplayer source, string text)
            {
                m_parent = source;
                m_dataSource = text;
                m_watcher = new DPWatcher(source);

                TrackAndGetValue(source, Control.FontFamilyProperty, TextElement.FontFamilyProperty, out FontFamily fontFamily);
                TrackAndGetValue(source, Control.FontStretchProperty, TextElement.FontStretchProperty, out FontStretch fontStretch);
                TrackAndGetValue(source, Control.FontStyleProperty, TextElement.FontStyleProperty, out FontStyle fontStyle);
                TrackAndGetValue(source, Control.FontWeightProperty, TextElement.FontWeightProperty, out FontWeight fontWeight);
                TrackAndGetValue(source, Control.FontSizeProperty, TextElement.FontSizeProperty, out m_foundFontSize);
                m_foundTypeface = new Typeface(fontFamily, fontStyle, fontWeight, fontStretch);

                m_watcher.WatchedValueChanged += _DoNeedsRender;
                void _DoNeedsRender (object _sender, EventArgs _e) => this.TriggerNeedsRender();
            }

            bool IDisplayItem.IsDisplayed => false;
            UIElement IDisplayItem.RenderTarget => m_renderTarget;
            void IDisplayItem.Place (Point location, double angle) { }
            public event EventHandler<EventArgs> NeedsRender;
            double IDisplayItem.HorizontalExtent { get; }
            double IDisplayItem.VerticalExtent { get; }

            private void TriggerNeedsRender ()
            {
                this.NeedsRender?.Invoke(this, EventArgs.Empty);
            }

            public void ResetChildren ()
            {
                var oldChildren = m_children.ToList();
                m_children.Clear();
                foreach (char letter in m_dataSource)
                {
                    m_children.Add(new TextDisplayItem(this, new FormattedText(letter.ToString(), CultureInfo.CurrentCulture, FlowDirection.LeftToRight, m_foundTypeface, m_foundFontSize, m_parent.Foreground, VisualTreeHelper.GetDpi(m_parent).PixelsPerDip)));
                }

                m_parent.OnParentItemUpdated(this, oldChildren, m_children);
            }

            private void TrackAndGetValue<T> (FrameworkElement source, DependencyProperty localProp, DependencyProperty textElementAttachedProp, out T foundValue)
            {
                if (source.ReadLocalValue(localProp) != DependencyProperty.UnsetValue)
                {
                    foundValue = (T)source.GetValue(localProp);
                }
                else if (source.ReadLocalValue(textElementAttachedProp) != DependencyProperty.UnsetValue)
                {
                    foundValue = (T)source.GetValue(textElementAttachedProp);
                }
                else
                {
                    foundValue = (T)source.GetValue(localProp);
                }

                m_watcher.Watch(localProp);
                m_watcher.Watch(textElementAttachedProp);
            }
        }

        [DebuggerDisplay("{TextDisplay.Text} - W({Width})")]
        private class TextDisplayItem : DisplayItem<SimpleTextDisplay>
        {
            public TextDisplayItem (TextParentItem parent, FormattedText part) : base(new SimpleTextDisplay(part))
            {
            }

            public override double HorizontalExtent => this.RenderTarget.Text.WidthIncludingTrailingWhitespace;
            public override double VerticalExtent => this.RenderTarget.Text.Baseline;
        }

        [DebuggerDisplay("DisplayItem: {ElementTypeFriendly}")]
        private class GeneralDisplayItem : DisplayItem<FrameworkElement>
        {
            public GeneralDisplayItem (FrameworkElement element) : base(element)
            {
            }

            public string ElementTypeFriendly => this.RenderTarget.GetType().Name;
            public override double HorizontalExtent => this.RenderTarget.DesiredSize.Width;
            public override double VerticalExtent => this.RenderTarget.ActualHeight;
        }

#if false
        public abstract class DisplayItem : NotifyPropertyChanged
        {
            public DisplayItem (object source)
            {
                this.Source = source;
            }

            public event EventHandler<EventArgs> NeedsRender;

            public virtual double Width { get; }
            public virtual double Baseline { get; }
            public object Source { get; }

            private Transform m_renderTransform;
            public Transform RenderTransform
            {
                get => m_renderTransform;
                private set => this.SetAndRaiseIfChanged(ref m_renderTransform, value);
            }

            public virtual void TriggerMeasure (Size size) { }


            public void Transform (params Transform[] items)
            {
                var group = new TransformGroup();
                group.Children.AddEach(items);
                this.RenderTransform = group;
            }

            protected void TriggerNeedsRender ()
            {
                this.NeedsRender?.Invoke(this, EventArgs.Empty);
            }
        }

        private class TextParentItem : DisplayItem
        {
            private readonly PathFollowingContentDisplayer m_parent;
            private readonly DPWatcher m_watcher;
            private readonly List<TextDisplayItem> m_children = new List<TextDisplayItem>();
            private Typeface m_foundTypeface;
            private double m_foundFontSize;
            private string m_dataSource;

            public TextParentItem (PathFollowingContentDisplayer parent, TextBlock source) : base(source)
            {
                m_parent = parent;
                m_dataSource = source.Text;
                m_watcher = new DPWatcher(source);
                
                TrackAndGetValue(source, TextBlock.FontFamilyProperty,  TextElement.FontFamilyProperty,  out FontFamily fontFamily);
                TrackAndGetValue(source, TextBlock.FontStretchProperty, TextElement.FontStretchProperty, out FontStretch fontStretch);
                TrackAndGetValue(source, TextBlock.FontStyleProperty,   TextElement.FontStyleProperty,   out FontStyle fontStyle);
                TrackAndGetValue(source, TextBlock.FontWeightProperty,  TextElement.FontWeightProperty,  out FontWeight fontWeight);
                TrackAndGetValue(source, TextBlock.FontSizeProperty,    TextElement.FontSizeProperty,    out m_foundFontSize);

                m_watcher.Watch(TextBlock.TextProperty);
                m_foundTypeface = new Typeface(fontFamily, fontStyle, fontWeight, fontStretch);

                m_watcher.WatchedValueChanged += _DoNeedsRender;
                void _DoNeedsRender (object _sender, EventArgs<object> _e)
                {
                    // The text itself updated
                    if (_e.Value is string text)
                    {
                        m_dataSource = text;
                        this.ResetChildren();
                    }

                    this.TriggerNeedsRender();
                }
            }

            public TextParentItem (PathFollowingContentDisplayer source, string text) : base(text)
            {
                m_parent = source;
                m_dataSource = text;
                m_watcher = new DPWatcher(source);

                TrackAndGetValue(source, Control.FontFamilyProperty,  TextElement.FontFamilyProperty,  out FontFamily fontFamily);
                TrackAndGetValue(source, Control.FontStretchProperty, TextElement.FontStretchProperty, out FontStretch fontStretch);
                TrackAndGetValue(source, Control.FontStyleProperty,   TextElement.FontStyleProperty,   out FontStyle fontStyle);
                TrackAndGetValue(source, Control.FontWeightProperty,  TextElement.FontWeightProperty,  out FontWeight fontWeight);
                TrackAndGetValue(source, Control.FontSizeProperty,    TextElement.FontSizeProperty,    out m_foundFontSize);
                m_foundTypeface = new Typeface(fontFamily, fontStyle, fontWeight, fontStretch);

                m_watcher.WatchedValueChanged += _DoNeedsRender;
                void _DoNeedsRender (object _sender, EventArgs _e) => this.TriggerNeedsRender();
            }

            public void ResetChildren()
            {
                var oldChildren = m_children.ToList();
                m_children.Clear();
                foreach (char letter in m_dataSource)
                {
                    m_children.Add(new TextDisplayItem(this, new FormattedText(letter.ToString(), CultureInfo.CurrentCulture, FlowDirection.LeftToRight, m_foundTypeface, m_foundFontSize, m_parent.Foreground, VisualTreeHelper.GetDpi(m_parent).PixelsPerDip)));
                }

                m_parent.OnParentItemUpdated(this, oldChildren, m_children);
            }

            private void TrackAndGetValue<T> (FrameworkElement source, DependencyProperty localProp, DependencyProperty textElementAttachedProp, out T foundValue)
            {
                if (source.ReadLocalValue(localProp) != DependencyProperty.UnsetValue)
                {
                    foundValue = (T)source.GetValue(localProp);
                }
                else if (source.ReadLocalValue(textElementAttachedProp) != DependencyProperty.UnsetValue)
                {
                    foundValue = (T)source.GetValue(textElementAttachedProp);
                }
                else
                {
                    foundValue = (T)source.GetValue(localProp);
                }

                m_watcher.Watch(localProp);
                m_watcher.Watch(textElementAttachedProp);
            }
        }

        [DebuggerDisplay("{TextDisplay.Text} - W({Width})")]
        private class TextDisplayItem : DisplayItem
        {
            public TextDisplayItem (DisplayItem parent, FormattedText part) : base (parent.Source)
            {
                this.TextDisplay = part;
            }

            public FormattedText TextDisplay { get; }

            public override double Width => this.TextDisplay.WidthIncludingTrailingWhitespace;
            public override double Baseline => this.TextDisplay.Baseline;
        }

        [DebuggerDisplay("DisplayItem: {ElementTypeFriendly}")]
        private class FrameworkElementDisplayItem : DisplayItem
        {
            public string ElementTypeFriendly => this.Element.GetType().Name;
            public FrameworkElement Element { get; }
            public override double Width => this.Element.DesiredSize.Width;
            public override double Baseline => this.Element.ActualHeight;
            public FrameworkElementDisplayItem (FrameworkElement element) : base(element)
            {
                this.Element = element;
                this.Element.SetBinding(FrameworkElement.RenderTransformProperty, this.CreateBinding(nameof(RenderTransform)));
            }

            public override void TriggerMeasure (Size size)
            {
                this.Element.Measure(size);
            }
        }
#endif
    }
}